/// <summary>
/// 玩家状态枚举（T-FSM）
/// FSM 统一管理状态转换，各组件查询当前状态做决策。
/// </summary>
public enum PlayerState
{
    /// <summary>待机（地面静止）</summary>
    Idle,
    /// <summary>地面移动</summary>
    Run,
    /// <summary>跳跃上升（vy > 0）</summary>
    Jump,
    /// <summary>下落（vy < 0）</summary>
    Fall,
    /// <summary>冲刺（朝向锁定）</summary>
    Dash,
    /// <summary>攻击（朝向锁定）</summary>
    Attack,
    /// <summary>受击硬直</summary>
    Hurt,
    /// <summary>死亡（终态）</summary>
    Death,
}
