using UnityEngine;

/// <summary>
/// 玩家回溯组件（T-021）
///
/// 实现 IRewindable，负责采集玩家每帧状态供 RewindManager 录制。
/// 回放（ApplySnapshot / 光球形态 / 恢复控制）在 T-023 回放管线中实现。
///
/// 录制数据（对应 FrameSnapshot 字段）：
/// - position    ← transform.position（2D 下 z 保持）
/// - velocity    ← Rigidbody2D.linearVelocity（回溯停止后物理连续性，T-028 用）
/// - health      ← PlayerHealth.CurrentHP（int，maxHP=100）
/// - isGrounded  ← PlayerController.IsGrounded（缓存值，与录制同帧一致）
///
/// 生命周期：
/// - Awake：Register 到 RewindManager（懒加载单例，场景无需手动挂管理器）
/// - OnDestroy：Unregister，避免幽灵引用（? 防止反初始化时误创建实例）
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerHealth))]
public class PlayerRewind : MonoBehaviour, IRewindable
{
    // 组件缓存
    private Rigidbody2D rb;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private PlayerJump playerJump;
    private PlayerDash playerDash;
    private PlayerAttack playerAttack;

    /// <summary>全局回溯标记：RewindManager 设置，各组件查询（T-029 免疫伤害用）</summary>
    public bool IsRewinding { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerHealth = GetComponent<PlayerHealth>();
        playerController = GetComponent<PlayerController>();
        playerJump = GetComponent<PlayerJump>();
        playerDash = GetComponent<PlayerDash>();
        playerAttack = GetComponent<PlayerAttack>();

        RewindManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        // 注销安全：先判 Exists（不触发 Instance getter，也就不会在退出流程中重建管理器）
        if (RewindManager.Exists)
            RewindManager.Instance.Unregister(this);
    }

    /// <summary>
    /// 录制当前帧（T-021）：位置 / 速度 / 血量 / 地面状态 → 快照。
    /// 由 RewindManager.RecordStep 每 FixedUpdate 调用。
    /// </summary>
    public FrameSnapshot CaptureSnapshot()
    {
        return new FrameSnapshot(
            transform.position,
            rb.linearVelocity,
            playerHealth.CurrentHP,
            playerController != null && playerController.IsGrounded
        );
    }

    // ============================================================
    // 回放管线实现（T-023）
    // ============================================================

    /// <summary>
    /// 回放一步（决策 3）：用 MovePosition 沿路径倒退，避免直赋 transform
    /// （MovePosition 走刚体路径，插值/Trigger 时序/物理同步正常）。
    /// 同时写回快照速度，保证停止后物理连续（T-028 依赖）。
    /// </summary>
    public void ApplySnapshot(FrameSnapshot snapshot)
    {
        rb.MovePosition(snapshot.position);
        // 同步内在速度：回溯期间是 dynamic body，不写回则重力持续累积并与 MovePosition 打架
        // （穿地/抖动）。停止后的归零由 OnRewindEnd 负责；T-024 切 isKinematic 后此行走可去。
        rb.linearVelocity = snapshot.velocity;
    }

    /// <summary>
    /// 回溯开始：冻结玩家控制组件（复用死亡流程 PlayerHealth.Die() 的模式，
    /// 用户确认的方案）。组件禁用后 Update/FixedUpdate 停摆，输入与移动彻底冻结。
    /// 光球形态切换（isKinematic + isTrigger）属 T-024。
    /// </summary>
    public void OnRewindStart()
    {
        if (playerController != null) playerController.enabled = false;
        if (playerJump != null) playerJump.enabled = false;
        if (playerDash != null) playerDash.enabled = false;
        if (playerAttack != null) playerAttack.enabled = false;
    }

    /// <summary>
    /// 回溯结束：速度归零（时间断点处完全静止，重新获取控制），然后恢复控制组件。
    /// 注：快照仍录制 velocity（回放用），仅停止时不再写回——玩家手感决策。
    /// </summary>
    public void OnRewindEnd()
    {
        // 速度归零：回溯停止是一个时间断点，从静止重新控制更干脆
        rb.linearVelocity = Vector2.zero;

        if (playerController != null) playerController.enabled = true;
        if (playerJump != null) playerJump.enabled = true;
        if (playerDash != null) playerDash.enabled = true;
        if (playerAttack != null) playerAttack.enabled = true;
    }
}
