using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家血量与受击反馈（T-013）+ 死亡重生（T-014）
///
/// 实现 IDamageable 接口，供 EnemyStateMachine 调用。
///
/// 职责：
/// - 血量管理（maxHP=100）
/// - 受击无敌（冲刺无敌 + 受击后 0.5s 无敌）
/// - 击退反馈（后上方弹飞）
/// - 受击/死亡动画触发
/// - 死亡画面 UI（占位版）+ 重生到关卡起点
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("配置")]
    [SerializeField] private PlayerConfig config;

    // 组件缓存
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private PlayerAnimator playerAnimator;
    private PlayerDash playerDash;
    private PlayerController playerController;
    private PlayerJump playerJump;
    private PlayerAttack playerAttack;

    // 状态
    private int currentHP;
    private float invincibilityTimer;
    private float hitStunTimer;
    private bool isDead;

    /// <summary>重生点位置（初始 = 场景起点，检查点可更新）</summary>
    private Vector3 spawnPoint;

    public int CurrentHP => currentHP;
    public int MaxHP => config != null ? config.maxHP : 100;
    public bool IsStunned => hitStunTimer > 0f;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerAnimator = GetComponent<PlayerAnimator>();
        playerDash = GetComponent<PlayerDash>();
        playerController = GetComponent<PlayerController>();
        playerJump = GetComponent<PlayerJump>();
        playerAttack = GetComponent<PlayerAttack>();

        if (config == null)
            Debug.LogError("PlayerConfig 未赋值！请在 Inspector 拖拽配置", this);

        currentHP = config != null ? config.maxHP : 100;
    }

    private void Start()
    {
        // 重生点：优先查找场景中的 SpawnPoint 对象，找不到则用玩家初始位置
        GameObject spawnGO = GameObject.Find("SpawnPoint");
        if (spawnGO != null)
        {
            spawnPoint = spawnGO.transform.position;
            Debug.Log($"SpawnPoint 已设置: {spawnPoint}");
        }
        else
        {
            spawnPoint = transform.position;
            Debug.Log($"未找到 SpawnPoint，使用玩家初始位置: {spawnPoint}");
        }
    }

    private void Update()
    {
        // 无敌计时递减（用 unscaledDeltaTime 防止暂停时计时停止）
        if (invincibilityTimer > 0f)
            invincibilityTimer -= Time.unscaledDeltaTime;

        // 硬直计时递减
        if (hitStunTimer > 0f)
            hitStunTimer -= Time.unscaledDeltaTime;
    }

    // ============================================================
    // IDamageable
    // ============================================================

    public void TakeDamage(int damage, Vector2 knockbackDirection)
    {
        if (isDead || config == null) return;

        // 冲刺无敌 → 免疫
        if (playerDash != null && playerDash.IsInvincible) return;

        // 受击后无敌 → 免疫
        if (invincibilityTimer > 0f) return;

        // 扣血
        currentHP -= damage;
        bool isLethal = currentHP <= 0;
        Debug.Log($"玩家受到 {damage} 伤害（HP: {currentHP}/{MaxHP}）");

        // 立即锁定移动输入
        var controller = GetComponent<PlayerController>();
        controller?.ForceStopMovement();

        // 击退
        if (rb != null)
        {
            rb.linearVelocity = knockbackDirection;
        }

        // 受伤动画（仅非致死伤害，避免死亡时先闪受伤帧）
        if (!isLethal)
            playerAnimator?.TriggerHurt();

        // 无敌计时 + 硬直计时（仅非致死）
        if (!isLethal)
        {
            invincibilityTimer = config.invincibilityDuration;
            hitStunTimer = config.hitStunDuration;
        }

        // 死亡
        if (isLethal)
        {
            Die();
        }
    }

    // ============================================================
    // 死亡流程（T-014 完整）
    // ============================================================

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("玩家死亡");

        // 冻结控制组件（不禁用 PlayerHealth 自身，因为协程需要它运行）
        if (playerController != null) playerController.enabled = false;
        if (playerJump != null) playerJump.enabled = false;
        if (playerDash != null) playerDash.enabled = false;
        if (playerAttack != null) playerAttack.enabled = false;

        // 停止物理
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // 死亡动画
        playerAnimator?.TriggerDeath();

        // 启动协程：等待死亡动画播完 → 显示死亡画面
        StartCoroutine(ShowDeathScreenAfterAnimation());
    }

    /// <summary>等待死亡动画结束，然后显示死亡画面 UI</summary>
    private IEnumerator ShowDeathScreenAfterAnimation()
    {
        // 读取死亡动画时长
        float deathAnimLength = GetDeathAnimationLength();

        // 用 unscaledDeltaTime 等待，不受 TimeScale 影响
        float elapsed = 0f;
        while (elapsed < deathAnimLength)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 死亡动画播完 → 隐藏角色 Sprite
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        // 显示死亡画面 UI
        DeathScreenUI.Show(this);
    }

    /// <summary>从 Animator 中读取死亡动画片段时长</summary>
    private float GetDeathAnimationLength()
    {
        if (playerAnimator == null) return 1.5f;

        Animator animator = playerAnimator.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
            return 1.5f;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.ToLower().Contains("death"))
                return clip.length;
        }
        return 1.5f;
    }

    // ============================================================
    // 重生
    // ============================================================

    /// <summary>
    /// 重生到 SpawnPoint，重置血量/状态，重新启用控制组件。
    /// 由 DeathScreenUI 的"重新开始"按钮调用。
    /// </summary>
    public void Revive()
    {
        if (!isDead) return;

        // 决策 5：重生清空全部回溯缓冲（重生后按 R 无反应，验收项）
        RewindManager.Instance?.ClearAll();

        // 搬回重生点
        transform.position = spawnPoint;

        // 重置血量
        currentHP = MaxHP;
        invincibilityTimer = 0f;
        isDead = false;

        // 恢复角色可见
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        // 重置 Animator 回 Idle（死亡动画已播完，停在死亡状态）
        if (playerAnimator != null)
        {
            Animator animator = playerAnimator.GetComponent<Animator>();
            if (animator != null)
                animator.Play("PlayerIdle", 0, 0f);
        }

        // 重新启用物理
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        // 重新启用控制组件
        if (playerController != null) playerController.enabled = true;
        if (playerJump != null) playerJump.enabled = true;
        if (playerDash != null) playerDash.enabled = true;
        if (playerAttack != null) playerAttack.enabled = true;

        // TODO (T-036): 重生时重置场景内所有敌人
        Debug.Log("玩家重生");
    }

    // ============================================================
    // 公开接口
    // ============================================================

    /// <summary>供 DeathScreenUI 或检查点更新重生位置</summary>
    public void SetSpawnPoint(Vector3 position)
    {
        spawnPoint = position;
    }
}
