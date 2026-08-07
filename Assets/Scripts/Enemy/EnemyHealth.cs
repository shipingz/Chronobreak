using UnityEngine;

/// <summary>
/// 敌人血量与受击（T-012）
/// 
/// 实现 IDamageable 接口，供 PlayerAttack 调用。
/// 
/// 职责：
/// - 血量管理（扣血/死亡）
/// - 受击反馈（动画由 Animator 负责，脚本只触发 trigger）
/// - 死亡流程（动画 → 延迟销毁）
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("配置")]
    [SerializeField] private EnemyConfig config;

    // 组件缓存
    private Rigidbody2D rb;
    private EnemyStateMachine stateMachine;
    private Animator animator;

    // 状态
    private int currentHP;
    private bool isDead;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stateMachine = GetComponent<EnemyStateMachine>();
        animator = GetComponent<Animator>();

        if (config == null)
            Debug.LogError("EnemyConfig 未赋值！请在 Inspector 拖拽配置", this);

        currentHP = config != null ? config.maxHP : 60;
    }

    // ============================================================
    // IDamageable
    // ============================================================

    public void TakeDamage(int damage, Vector2 knockbackDirection)
    {
        if (isDead || config == null) return;

        // 扣血
        currentHP -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害（HP: {currentHP}/{GetMaxHP()}）", this);

        // 击退（无论致死还是非致死，保留最后一次击退）
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            Vector2 adjustedKnockback = knockbackDirection * (1f - config.knockbackResistance);
            rb.AddForce(adjustedKnockback, ForceMode2D.Impulse);
        }

        // 致死 → 跳过受伤动画，直接死亡
        if (currentHP <= 0)
        {
            Die();
            return;
        }

        // 非致死 → 播放受伤动画 + 击退硬直
        if (animator != null)
            animator.SetTrigger("isHurt");

        // 暂定 FSM 移动，让物理击退效果可见
        stateMachine?.OnHitStun(0.25f);
    }

    // ============================================================
    // 死亡
    // ============================================================

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} 死亡", this);

        // 通知状态机冻结
        if (stateMachine != null)
            stateMachine.OnDeath();

        // 播放死亡动画
        if (animator != null)
            animator.SetTrigger("isDead");

        // 读取死亡动画时长，播完后稍作停留再销毁，保证最后一帧完全定格
        // （+0.1s 缓冲：避免动画最后一帧刚播完就被销毁，产生"截断"感）
        float destroyDelay = 0.8f; // 默认保底（0.7 + 0.1）
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.ToLower().Contains("death"))
                {
                    destroyDelay = clip.length + 0.1f;
                    break;
                }
            }
        }
        Destroy(gameObject, destroyDelay);
    }

    // ============================================================
    // 公开接口
    // ============================================================

    public int GetCurrentHP() => currentHP;
    public int GetMaxHP() => config != null ? config.maxHP : 60;
}
