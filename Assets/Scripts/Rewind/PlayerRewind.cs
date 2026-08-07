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

    /// <summary>全局回溯标记：RewindManager 设置，各组件查询（T-023 起使用）</summary>
    public bool IsRewinding { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerHealth = GetComponent<PlayerHealth>();
        playerController = GetComponent<PlayerController>();

        RewindManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        // 用 ?. 防反初始化顺序问题：不因注销而误创建管理器实例
        RewindManager.Instance?.Unregister(this);
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
    // 以下为 T-023 回放管线实现，先占位
    // ============================================================

    /// <summary>回放一步：应用一帧快照（TODO T-023：MovePosition 倒退）</summary>
    public void ApplySnapshot(FrameSnapshot snapshot) { }

    /// <summary>回溯开始（TODO T-023：切光球形态 isKinematic + isTrigger）</summary>
    public void OnRewindStart() { }

    /// <summary>回溯结束（TODO T-023：恢复控制 + 截断缓冲）</summary>
    public void OnRewindEnd() { }
}
