using UnityEngine;

/// <summary>
/// 敌人状态机 — 巡逻兵基底（T-012）
/// 
/// 五状态：Idle → Patrol → Chase → Attack → Cooldown
/// 
/// 设计要点：
/// - 组合而非继承，为 D14 双类型敌人留扩展
/// - 边缘 raycast（决策 11）：巡逻/追击时检测前方地面，无地面则掉头/停住
/// - 攻击通过 IDamageable 接口作用于玩家（空安全，T-013 做好自动生效）
/// - D14 时通过 SetState() 接口被 EnemyFreeze/EnemyRewind 控制
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public class EnemyStateMachine : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private EnemyConfig config;

    [Header("巡逻中心（留空=初始位置）")]
    [SerializeField] private Transform patrolCenterOverride;

    [Header("调试")]
    [SerializeField] private bool showGizmo = true;

    /// <summary>当前状态</summary>
    public EnemyState CurrentState { get; private set; }

    // 组件缓存
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D cachedCollider;
    private Animator animator;
    private EnemyHealth enemyHealth;

    // 状态计时
    private float stateTimer;

    // 巡逻
    private Vector2 patrolCenter;
    private int patrolDirection = 1; // 1=右, -1=左
    private float lastEdgeTurnTime;  // 防边缘反复横跳

    // 玩家引用
    private Transform playerTransform;

    // 边缘等待
    private float edgeWaitTimer;

    // 击退硬直
    private float stunTimer;

    // 死亡标记
    private bool isDead;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        cachedCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (config == null)
            Debug.LogError("EnemyConfig 未赋值！请在 Inspector 拖拽配置", this);

        // 巡逻中心 = 初始位置（未覆盖时）
        patrolCenter = patrolCenterOverride != null
            ? (Vector2)patrolCenterOverride.position
            : (Vector2)transform.position;
    }

    private void Start()
    {
        TransitionToState(EnemyState.Idle);
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        // 击退硬直中：不更新 FSM 逻辑，让物理自由运动，硬直结束后恢复
        if (stunTimer > 0f)
        {
            stunTimer -= Time.fixedDeltaTime;
            ApplyAnimator();
            return;
        }

        FindPlayer();

        switch (CurrentState)
        {
            case EnemyState.Idle:        UpdateIdle();        break;
            case EnemyState.Patrol:      UpdatePatrol();      break;
            case EnemyState.Chase:       UpdateChase();       break;
            case EnemyState.Attack:      UpdateAttack();      break;
            case EnemyState.Cooldown:    UpdateCooldown();    break;
        }

        ApplyAnimator();
    }

    // ============================================================
    // 状态切换
    // ============================================================

    private void TransitionToState(EnemyState newState)
    {
        CurrentState = newState;
        stateTimer = 0f;

        // 进入攻击态时触发一次动画 trigger（不持续占用 bool）
        if (newState == EnemyState.Attack && animator != null)
            animator.SetTrigger("isAttacking");
    }

    // ============================================================
    // Idle — 站定等待，检测到玩家立即 Chase
    // ============================================================

    private void UpdateIdle()
    {
        stateTimer += Time.fixedDeltaTime;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (playerTransform != null)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        if (stateTimer >= config.idleDuration)
        {
            TransitionToState(EnemyState.Patrol);
        }
    }

    // ============================================================
    // Patrol — 两点往返 + 边缘检测
    // ============================================================

    private void UpdatePatrol()
    {
        if (playerTransform != null)
        {
            TransitionToState(EnemyState.Chase);
            return;
        }

        // 边缘检测：前方无地面 → 掉头（有冷却防振荡）
        if (!HasGroundAhead(patrolDirection) && Time.time - lastEdgeTurnTime > config.edgeTurnCooldown)
        {
            patrolDirection *= -1;
            lastEdgeTurnTime = Time.time;
        }

        // 巡逻范围边界掉头
        float distanceFromCenter = transform.position.x - patrolCenter.x;
        if (Mathf.Abs(distanceFromCenter) >= config.patrolRadius)
        {
            patrolDirection = (int)-Mathf.Sign(distanceFromCenter);
        }

        rb.linearVelocity = new Vector2(patrolDirection * config.patrolSpeed, rb.linearVelocity.y);
        FlipSprite(patrolDirection);
    }

    // ============================================================
    // Chase — 追击玩家 + 边缘检测停住
    // ============================================================

    private void UpdateChase()
    {
        if (playerTransform == null)
        {
            TransitionToState(EnemyState.Patrol);
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // 近身 → Attack
        if (distanceToPlayer <= config.attackRange)
        {
            TransitionToState(EnemyState.Attack);
            return;
        }

        // 超出检测范围 → 退回 Patrol（带 hysteresis 防反复横跳）
        if (distanceToPlayer > config.detectionRange * config.chaseHysteresis)
        {
            TransitionToState(EnemyState.Patrol);
            return;
        }

        int chaseDirection = (int)Mathf.Sign(playerTransform.position.x - transform.position.x);
        FlipSprite(chaseDirection);

        // 边缘检测（决策 11）：前方无地面 → 停住
        if (!HasGroundAhead(chaseDirection))
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            edgeWaitTimer += Time.fixedDeltaTime;

            // 卡在边缘太久 → 退回 Patrol
            if (edgeWaitTimer >= config.edgeWaitTimeout)
            {
                edgeWaitTimer = 0f;
                TransitionToState(EnemyState.Patrol);
            }
            return;
        }

        edgeWaitTimer = 0f;
        rb.linearVelocity = new Vector2(chaseDirection * config.chaseSpeed, rb.linearVelocity.y);
    }

    // ============================================================
    // Attack — 近战攻击
    // ============================================================

    private void UpdateAttack()
    {
        stateTimer += Time.fixedDeltaTime;

        // 在 hitDelay 时刻施加一次伤害
        if (stateTimer >= config.attackHitDelay && stateTimer - Time.fixedDeltaTime < config.attackHitDelay)
        {
            ApplyDamageToPlayer();
        }

        if (stateTimer >= config.attackInterval)
        {
            TransitionToState(EnemyState.Cooldown);
        }

        // 攻击期间不移动
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    // ============================================================
    // Cooldown — 攻击后短暂停顿
    // ============================================================

    private void UpdateCooldown()
    {
        stateTimer += Time.fixedDeltaTime;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (stateTimer >= config.cooldownDuration)
        {
            // 玩家还在检测范围内 → Chase，否则 Patrol
            if (playerTransform != null)
            {
                float distance = Vector2.Distance(transform.position, playerTransform.position);
                if (distance <= config.detectionRange)
                {
                    TransitionToState(EnemyState.Chase);
                    return;
                }
            }
            TransitionToState(EnemyState.Patrol);
        }
    }

    // ============================================================
    // 攻击玩家
    // ============================================================

    private void ApplyDamageToPlayer()
    {
        if (playerTransform == null) return;

        // 距离检查（带缓冲区，防贴身不出伤）
        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance > config.attackRange + config.attackRangeBuffer) return;

        IDamageable damageable = playerTransform.GetComponent<IDamageable>();
        if (damageable != null)
        {
            float facing = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
            Vector2 knockback = new Vector2(facing * config.knockbackForce, config.knockbackUpwardForce);
            damageable.TakeDamage(config.attackDamage, knockback);
        }
    }

    // ============================================================
    // 边缘检测（决策 11）
    // ============================================================

    private bool HasGroundAhead(int direction)
    {
        if (cachedCollider == null || config == null) return true;

        // 从碰撞体前边缘底部向下发射射线
        // 注意使用 config.groundLayer，这需要在 Inspector 中设置
        float frontX = cachedCollider.bounds.center.x
                       + direction * cachedCollider.bounds.extents.x;
        float footY = cachedCollider.bounds.min.y;

        Debug.DrawRay(
            new Vector2(frontX, footY),
            Vector2.down * config.groundCheckDistance,
            Color.cyan
        );

        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(frontX, footY),
            Vector2.down,
            config.groundCheckDistance,
            config.groundLayer
        );

        return hit.collider != null;
    }

    // ============================================================
    // 检测玩家
    // ============================================================

    private void FindPlayer()
    {
        // OverlapCircle 返回范围内第一个命中的 Collider
        Collider2D hit = Physics2D.OverlapCircle(
            transform.position,
            config.detectionRange,
            config.playerLayer
        );

        playerTransform = hit != null ? hit.transform : null;
    }

    // ============================================================
    // 翻转精灵
    // ============================================================

    private void FlipSprite(int direction)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = direction < 0;
    }

    // ============================================================
    // 动画驱动
    // ============================================================

    private void ApplyAnimator()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
    }

    // ============================================================
    // 外部接口
    // ============================================================

    /// <summary>
    /// 敌人死亡时由 EnemyHealth 调用。冻结状态机、禁用碰撞体。
    /// </summary>
    public void OnDeath()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (cachedCollider != null)
            cachedCollider.enabled = false;
    }

    /// <summary>
    /// 受击硬直：被击中时由 EnemyHealth 调用，暂停 FSM 移动让物理击退生效
    /// </summary>
    public void OnHitStun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration); // 取最大值，防连续受击重置
    }

    /// <summary>
    /// 供 D14 EnemyFreeze/EnemyRewind 强制切换状态
    /// </summary>
    public void SetState(EnemyState state)
    {
        if (!isDead)
            TransitionToState(state);
    }

    // ============================================================
    // Gizmo 调试可视化
    // ============================================================

    private void OnDrawGizmos()
    {
        if (config == null || !showGizmo) return;

        // ============================================================
        // 攻击范围（始终显示）
        // ============================================================
        Vector3 pos = transform.position;
        float radius = config.attackRange;

        // 实心半透明圆盘
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawSphere(pos, radius);

        // 外圈实线
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(pos, radius);

        // 方向指示器（指向精灵朝向）
        if (spriteRenderer != null)
        {
            float facing = spriteRenderer.flipX ? -1f : 1f;
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Vector3 tip = pos + new Vector3(facing * radius, 0f, 0f);
            Gizmos.DrawLine(pos, tip);
            // 箭头小三角
            Vector3 up = new Vector3(facing * (radius - 0.3f), 0.2f, 0f);
            Vector3 down = new Vector3(facing * (radius - 0.3f), -0.2f, 0f);
            Gizmos.DrawLine(tip, pos + up);
            Gizmos.DrawLine(tip, pos + down);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (config == null || !showGizmo) return;

        // 检测范围（选中时显示）
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, config.detectionRange);

        // 巡逻范围（选中时显示）
        Vector3 center = patrolCenterOverride != null
            ? patrolCenterOverride.position
            : transform.position;

        Gizmos.color = Color.cyan;
        Vector3 left = center + Vector3.left * config.patrolRadius;
        Vector3 right = center + Vector3.right * config.patrolRadius;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawSphere(left, 0.15f);
        Gizmos.DrawSphere(right, 0.15f);
    }
}

/// <summary>
/// 敌人状态枚举
/// </summary>
public enum EnemyState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Cooldown
}
