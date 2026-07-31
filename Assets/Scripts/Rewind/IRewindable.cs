/// <summary>
/// 可回溯对象接口（T-019）
///
/// 时间回溯的统一契约：凡是能"录制 + 回放"的对象（玩家、可回溯型敌人）都实现此接口，
/// 由 RewindManager 统一驱动（注册/注销 + 录制/回放循环）。
///
/// 调用时机（对应管线任务）：
/// - CaptureSnapshot：RewindManager 每 FixedUpdate 录制时调用（T-020 RecordStep）
/// - ApplySnapshot：回溯每步调用，用快照驱动 MovePosition 等恢复（T-023 RewindStep）
/// - OnRewindStart / OnRewindEnd：StartRewind / StopRewind 时调用（T-023 / T-028）
/// - IsRewinding：RewindManager 维护的全局回溯标记，供各组件查询
///   （如 PlayerHealth 判断"回溯中免疫一切伤害"，T-029 验收项）
///
/// 实现者（决策 8 / 14）：
/// - PlayerRewind：玩家，回溯时切光球形态（isKinematic + isTrigger）
/// - EnemyRewind：可回溯型敌人（蓝色），位置+血量完全回滚
/// - 定身型敌人（EnemyFreeze）不实现此接口：不参与回溯，监听 SO 事件冻结 AI
///
/// 设计说明：与 v1 旧接口相比去掉了 RewindableId（对象名即可调试）与
/// OnRewindComplete(targetSnapshot)（停止帧 = 最后一次 ApplySnapshot 的状态，无需再传快照）。
/// </summary>
public interface IRewindable
{
    /// <summary>录制当前帧快照（位置/速度/血量/地面状态）</summary>
    FrameSnapshot CaptureSnapshot();

    /// <summary>回放一步：应用一帧快照，恢复到该帧的历史状态</summary>
    void ApplySnapshot(FrameSnapshot snapshot);

    /// <summary>回溯开始：切换光球形态 / 冻结 AI 等准备工作</summary>
    void OnRewindStart();

    /// <summary>回溯结束：恢复控制 / 恢复 AI 等收尾工作</summary>
    void OnRewindEnd();

    /// <summary>全局回溯标记：RewindManager 设置，各组件只读查询</summary>
    bool IsRewinding { get; set; }
}
