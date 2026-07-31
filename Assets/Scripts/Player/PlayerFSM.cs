using UnityEngine;

/// <summary>
/// 玩家有限状态机 — 状态转换 + 朝向锁定（T-FSM）
///
/// 职责：
/// - 维护当前状态
/// - 提供状态查询（IsFacingLocked / CanMove / CanJump / CanAttack / CanDash）
/// - 处理状态转换的进入/退出副作用（如自动切换朝向锁定）
///
/// 各组件通过 Find 或 GetComponent 获取此引用，在适当时调用 TransitionTo。
/// FSM 不直接控制输入或物理，只做状态决策。
/// </summary>
[RequireComponent(typeof(PlayerJump))]
public class PlayerFSM : MonoBehaviour
{
    // ============================================================
    // 当前状态
    // ============================================================

    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    // ============================================================
    // 状态查询接口
    // ============================================================

    /// <summary>当前是否锁定精灵朝向（不可操作的状态下锁定）</summary>
    public bool IsFacingLocked =>
        CurrentState is PlayerState.Dash or PlayerState.Attack
            or PlayerState.Hurt or PlayerState.Death;

    /// <summary>是否可以水平移动</summary>
    public bool CanMove =>
        CurrentState is PlayerState.Idle or PlayerState.Run or PlayerState.Jump or PlayerState.Fall;

    /// <summary>是否可以跳跃</summary>
    public bool CanJump =>
        CurrentState is PlayerState.Idle or PlayerState.Run or PlayerState.Jump or PlayerState.Fall;

    /// <summary>是否可以攻击</summary>
    public bool CanAttack =>
        CurrentState is PlayerState.Idle or PlayerState.Run or PlayerState.Jump or PlayerState.Fall;

    /// <summary>是否可以冲刺</summary>
    public bool CanDash =>
        CurrentState is PlayerState.Idle or PlayerState.Run or PlayerState.Jump or PlayerState.Fall or PlayerState.Attack;

    /// <summary>是否处于可受击状态（非 Hurt 非 Death）</summary>
    public bool CanBeHit =>
        CurrentState is not PlayerState.Hurt and not PlayerState.Death;

    // ============================================================
    // 状态转换
    // ============================================================

    /// <summary>
    /// 请求状态转换。如果合法则执行进入/退出回调。
    /// </summary>
    public bool TransitionTo(PlayerState newState)
    {
        if (CurrentState == newState) return false;
        if (CurrentState == PlayerState.Death) return false;

        OnExitState(CurrentState);
        CurrentState = newState;
        OnEnterState(newState);

        return true;
    }

    // ============================================================
    // 进入/退出回调
    // ============================================================

    /// <summary>进入状态时的副作用</summary>
    private void OnEnterState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Dash:
            case PlayerState.Attack:
                // 这两个状态自动锁定朝向
                // 实际锁定由各组件读取 IsFacingLocked 来实现
                break;
        }
    }

    /// <summary>退出状态时的副作用</summary>
    private void OnExitState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Dash:
            case PlayerState.Attack:
                // 退出这两个状态时自动解锁朝向
                break;
        }
    }
}
