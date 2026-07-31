using UnityEngine;

/// <summary>
/// 单帧快照（T-016）
///
/// 回溯系统的最小数据单元：记录一个可回溯对象在某一物理帧的完整状态。
/// 设计为 struct 而非 class：
/// - 值类型，在 RingBuffer 数组中连续存储，无 GC 压力，缓存友好
/// - 只存恢复所需的最小状态集，不做序列化
///
/// 技术决策（依据：项目规划/项目时间规划.md §1）：
/// - 决策 1：录制与回放固定在 FixedUpdate（50Hz），缓冲区 300 帧/对象
///   （50Hz × 6s，覆盖 5 秒回溯上限 + 余量），10 对象 ≈ 72KB
/// - 决策 2：回放速度 1:1，每 FixedUpdate 退一帧，5 秒回溯现实中持键 5 秒，
///   与能量消耗 20%/s 一致
///
/// 字段与现有代码对应：
/// - position   ↔ transform.position（Vector3，2D 下 z 保持）
/// - velocity   ↔ Rigidbody2D.linearVelocity（回放后物理状态连续）
/// - health     ↔ PlayerHealth.currentHP / EnemyHealth.GetCurrentHP()
///   （int 绝对值，决策 7：无治疗道具，绝对值回滚效果等价且更简单）
/// - isGrounded ↔ PlayerController.isGrounded（回放后落地状态正确，Coyote/跳跃判定可用）
/// </summary>
public struct FrameSnapshot
{
    public Vector3 position;    // 位置（12B）
    public Vector2 velocity;    // 速度（8B）
    public int health;          // 血量绝对值（4B）
    public bool isGrounded;     // 是否在地面（4B，含 padding 共约 28B）

    /// <summary>便捷构造：录制侧用当前状态直接装箱</summary>
    public FrameSnapshot(Vector3 position, Vector2 velocity, int health, bool isGrounded)
    {
        this.position = position;
        this.velocity = velocity;
        this.health = health;
        this.isGrounded = isGrounded;
    }
}
