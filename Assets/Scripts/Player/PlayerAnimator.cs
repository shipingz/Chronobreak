using UnityEngine;

/// <summary>
/// 玩家动画驱动 — 把逻辑状态映射成 Animator 参数（T-010）
/// 依赖：PlayerAnimator.controller、PlayerJump.IsGrounded()、PlayerDash.IsDashing
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private Rigidbody2D rb;
    private PlayerJump playerJump;
    private PlayerDash playerDash;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        playerJump = GetComponent<PlayerJump>();
        playerDash = GetComponent<PlayerDash>();
    }

    private void Update()
    {
        // 把物理/逻辑状态映射成 Animator 参数
        // 更新频率：每帧（60fps），Animator 参数变化时自动过渡
        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        animator.SetFloat("verticalSpeed", rb.linearVelocity.y);

        if (playerJump != null)
            animator.SetBool("isGrounded", playerJump.IsGrounded());

        if (playerDash != null)
            animator.SetBool("isDashing", playerDash.IsDashing);
    }

    // ============================================================
    // 触发器（供其他组件调用）
    // ============================================================

    /// <summary>触发攻击动画（由 PlayerAttack 在攻击时调用）</summary>
    public void TriggerAttack()
    {
        animator.SetTrigger("Attack");
    }

    /// <summary>触发受伤动画（由 PlayerHealth 在受击时调用）</summary>
    public void TriggerHurt()
    {
        animator.SetTrigger("Hurt");
    }

    /// <summary>触发死亡动画（由 PlayerHealth 在 HP=0 时调用）</summary>
    public void TriggerDeath()
    {
        animator.SetTrigger("Death");
    }

    /// <summary>触发跳跃动画（由 PlayerJump 在跳跃时调用）</summary>
    public void TriggerJump()
    {
        animator.SetTrigger("Jump");
    }
}
