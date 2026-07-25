using UnityEngine;

/// <summary>
/// 玩家血量与受击反馈（T-013）
///
/// 实现 IDamageable 接口，供 EnemyStateMachine 调用。
///
/// 职责：
/// - 血量管理（maxHP=100）
/// - 受击无敌（冲刺无敌 + 受击后 0.5s 无敌）
/// - 击退反馈（后上方弹飞）
/// - 受击/死亡动画触发
///
/// 依赖 T-014 做完整的死亡流程（死亡画面、重生），这里只触发死亡动画。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("配置")]
    [SerializeField] private PlayerConfig config;

    // 组件缓存
    private Rigidbody2D rb;
    private PlayerAnimator playerAnimator;
    private PlayerDash playerDash;

    // 状态
    private int currentHP;
    private float invincibilityTimer;
    private float hitStunTimer;
    private bool isDead;

    public int CurrentHP => currentHP;
    public int MaxHP => config != null ? config.maxHP : 100;
    public bool IsStunned => hitStunTimer > 0f;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerAnimator = GetComponent<PlayerAnimator>();
        playerDash = GetComponent<PlayerDash>();

        if (config == null)
            Debug.LogError("PlayerConfig 未赋值！请在 Inspector 拖拽配置", this);

        currentHP = config != null ? config.maxHP : 100;
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
        Debug.Log($"玩家受到 {damage} 伤害（HP: {currentHP}/{MaxHP}）");

        // 立即锁定移动输入，清除当前速度（不等下一帧 Update）
        var controller = GetComponent<PlayerController>();
        controller?.ForceStopMovement();

        // 击退：在清除当前速度之后再设击退 velocity，不受残留输入影响
        if (rb != null)
        {
            rb.linearVelocity = knockbackDirection;
        }

        // 受伤动画
        playerAnimator?.TriggerHurt();

        // 无敌计时 + 硬直计时
        invincibilityTimer = config.invincibilityDuration;
        hitStunTimer = config.hitStunDuration;

        // 死亡
        if (currentHP <= 0)
        {
            Die();
        }
    }

    // ============================================================
    // 死亡（T-014 接完整的死亡流程）
    // ============================================================

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("玩家死亡");

        // 冻结控制（防止死亡后还能移动/攻击）
        enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // 死亡动画
        playerAnimator?.TriggerDeath();

        // 禁用攻击组件（防止死后还在跑判定检测）
        var attack = GetComponent<PlayerAttack>();
        if (attack != null) attack.enabled = false;
    }

    // ============================================================
    // 公开接口（供 T-014 死亡重生用）
    // ============================================================

    public void Revive()
    {
        isDead = false;
        currentHP = MaxHP;
        invincibilityTimer = 0f;
        enabled = true;
        if (rb != null)
            rb.simulated = true;
    }
}
