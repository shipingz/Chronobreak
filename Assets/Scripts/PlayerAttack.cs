using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 基础攻击 — 判定框 + 生效窗口 + 后摇可被冲刺取消（T-011）
/// 
/// 时序：
///   按下攻击键 → TriggerAttack()
///               → hitDelay 后激活判定框
///               → hitActiveDuration 后关闭判定框
///               → cooldown 后恢复可攻击
///               后摇期间可按冲刺提前取消
/// </summary>
[RequireComponent(typeof(PlayerAnimator), typeof(PlayerDash))]
public class PlayerAttack : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private PlayerAttackConfig config;

    [Header("Gizmo")]
    [SerializeField] private Color gizmoIdleColor = new Color(1f, 0.6f, 0f, 0.25f);
    [SerializeField] private Color gizmoActiveColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private bool showGizmo = true;

    // ============================================================
    // 缓存引用
    // ============================================================

    private PlayerAnimator playerAnimator;
    private PlayerDash playerDash;
    private SpriteRenderer spriteRenderer;
    private Collider2D cachedCollider;
    private InputSystem_Actions input;

    // ============================================================
    // 状态
    // ============================================================

    private bool isAttacking;
    private float stateTimer;         // 攻击状态计时
    private float cooldownTimer;      // 冷却计时
    private bool hitboxActive;        // 当前帧判定框是否生效
    private HashSet<Collider2D> hitTargets; // 同次攻击已命中的敌人
    private bool canCancelToDash;     // 后摇期允许冲刺取消
    private bool attackQueued;        // 冷却中输入攻击→结束后自动再打一次

    public bool IsAttacking => isAttacking;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        playerDash = GetComponent<PlayerDash>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        cachedCollider = GetComponent<Collider2D>();

        input = new InputSystem_Actions();

        if (config == null)
            Debug.LogError("PlayerAttackConfig 未赋值！请在 Inspector 拖拽配置", this);

        hitTargets = new HashSet<Collider2D>();
    }

    private void OnEnable()
    {
        input.Player.Attack.started += OnAttackStarted;
        input.Player.Enable();
    }

    private void OnDisable()
    {
        input.Player.Attack.started -= OnAttackStarted;
        input.Player.Disable();
    }

    private void OnDestroy() => input?.Dispose();

    private void Update()
    {
        // 冲刺取消检测（放在 Update 中，响应更及时）
        if (canCancelToDash && playerDash.IsDashing)
        {
            CancelAttack();
        }
    }

    private void FixedUpdate()
    {
        // 冷却递减
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.fixedDeltaTime;

        // 队列攻击：冷却结束自动触发
        if (!isAttacking && attackQueued && cooldownTimer <= 0f)
        {
            attackQueued = false;
            StartAttack();
            return;
        }

        if (!isAttacking) return;

        stateTimer += Time.fixedDeltaTime;

        // ---- 阶段 1: hitDelay 前 ----
        if (stateTimer < config.hitDelay)
        {
            // 什么都不做，等生效窗口
            return;
        }

        // ---- 阶段 2: 判定框激活窗口 ----
        float elapsedInHitWindow = stateTimer - config.hitDelay;
        if (elapsedInHitWindow < config.hitActiveDuration)
        {
            if (!hitboxActive)
            {
                hitboxActive = true;
                hitTargets.Clear();
            }
            PerformHitDetection();
            return;
        }

        // ---- 阶段 3: 后摇 ----
        if (hitboxActive)
        {
            hitboxActive = false;
            canCancelToDash = true;
        }

        // 后摇持续到总时长耗尽
        float totalAttackDuration = config.hitDelay + config.hitActiveDuration + config.cooldown;
        if (stateTimer >= totalAttackDuration)
        {
            EndAttack();
        }
    }

    // ============================================================
    // 触发
    // ============================================================

    private void OnAttackStarted(InputAction.CallbackContext ctx)
    {
        // 可攻击 → 立刻打
        if (!isAttacking && cooldownTimer <= 0f && !playerDash.IsDashing)
        {
            StartAttack();
            return;
        }

        // 不可攻击且不是在冲刺中 → 入队，冷却结束后自动打
        if (!playerDash.IsDashing)
            attackQueued = true;
    }

    private void StartAttack()
    {
        isAttacking = true;
        stateTimer = 0f;
        hitboxActive = false;
        canCancelToDash = false;
        hitTargets.Clear();

        Debug.Log($"[PlayerAttack] playerAnimator={(playerAnimator != null ? "OK" : "NULL")}", this);
        if (playerAnimator != null)
        {
            playerAnimator.TriggerAttack();
            Debug.Log("[PlayerAttack] TriggerAttack called", this);
        }
        else
        {
            Debug.LogError("[PlayerAttack] playerAnimator is NULL!", this);
        }
    }

    private void EndAttack()
    {
        isAttacking = false;
        hitboxActive = false;
        canCancelToDash = false;
        cooldownTimer = config.cooldown;
        hitTargets.Clear();
    }

    // ============================================================
    // 冲刺取消
    // ============================================================

    private void CancelAttack()
    {
        isAttacking = false;
        hitboxActive = false;
        canCancelToDash = false;
        cooldownTimer = 0f; // 冲刺取消不罚冷却
        hitTargets.Clear();
    }

    // ============================================================
    // 判定检测
    // ============================================================

    private void PerformHitDetection()
    {
        Vector2 center = GetHitboxCenter();
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, config.hitboxSize, 0f, config.enemyLayer);

        foreach (Collider2D hit in hits)
        {
            // 跳过自己
            if (hit.gameObject == gameObject) continue;

            // 同次攻击不重复伤害同一目标
            if (hitTargets.Contains(hit)) continue;
            hitTargets.Add(hit);

            // 命中反馈
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float facing = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
                Vector2 knockback = new Vector2(facing * config.knockbackForce, 1f);
                damageable.TakeDamage(config.damage, knockback);
            }
        }
    }

    // ============================================================
    // 判定框位置计算
    // ============================================================

    private Vector2 GetHitboxCenter()
    {
        float facing = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;

        if (cachedCollider != null)
        {
            Vector2 center = cachedCollider.bounds.center;
            center.x += facing * config.hitboxForwardOffset;
            center.y += config.hitboxVerticalOffset;
            return center;
        }

        // 无碰撞体时回到 transform
        Vector2 fallback = transform.position;
        fallback.x += facing * config.hitboxForwardOffset;
        fallback.y += config.hitboxVerticalOffset;
        return fallback;
    }

    // ============================================================
    // Gizmo（Scene 视图实时显示判定范围）
    // ============================================================

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        // Edit Mode 下获取引用
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (cachedCollider == null) cachedCollider = GetComponent<Collider2D>();

        Vector2 center = GetHitboxCenter();

        // 攻击激活时用红色，平时用橙色
        Gizmos.color = hitboxActive ? gizmoActiveColor : gizmoIdleColor;
        Gizmos.DrawCube(center, config.hitboxSize);

        // 边框用同色实线
        Gizmos.color = hitboxActive
            ? new Color(1f, 0f, 0f, 0.8f)
            : new Color(1f, 0.6f, 0f, 0.5f);
        Gizmos.DrawWireCube(center, config.hitboxSize);
    }
}
