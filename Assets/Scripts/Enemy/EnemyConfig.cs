using UnityEngine;

/// <summary>
/// 敌人参数配置（ScriptableObject）
/// 所有数值集中管理，Inspector 可视化调参
/// 供 EnemyStateMachine + EnemyHealth 共用
/// </summary>
[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Chronobreak/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("血量")]
    [Tooltip("最大生命值（玩家攻击伤害 20，60 = 3 次击杀）")]
    public int maxHP = 60;

    [Header("移动")]
    [Tooltip("巡逻速度（units/s）")]
    public float patrolSpeed = 3f;

    [Tooltip("追击速度（units/s）")]
    public float chaseSpeed = 5f;

    [Header("检测")]
    [Tooltip("开始追击玩家的距离（units）")]
    public float detectionRange = 6f;

    [Tooltip("攻击距离（units）")]
    public float attackRange = 1.5f;

    [Header("攻击")]
    [Tooltip("每次攻击对玩家造成的伤害")]
    public int attackDamage = 15;

    [Tooltip("攻击命中玩家的横向击退力度")]
    public float knockbackForce = 3f;

    [Tooltip("攻击命中玩家的向上击退力度")]
    public float knockbackUpwardForce = 2f;

    [Tooltip("攻击间隔（秒，含前后摇）")]
    public float attackInterval = 1.5f;

    [Tooltip("攻击伤害生效延迟（秒），从进入 Attack 状态算起")]
    public float attackHitDelay = 0.5f;

    [Header("巡逻")]
    [Tooltip("巡逻范围半径（从起点向左右延伸的 units）")]
    public float patrolRadius = 4f;

    [Tooltip("进入 Idle 状态后的等待时间（秒）")]
    public float idleDuration = 1f;

    [Header("地面检测")]
    [Tooltip("检测前方地面的射线长度（从碰撞体底部算起）")]
    public float groundCheckDistance = 0.15f;

    [Header("FSM 调参")]
    [Tooltip("巡逻时前方无地面后掉头的冷却（秒），防平台边缘反复横跳")]
    public float edgeTurnCooldown = 0.3f;

    [Tooltip("追击时卡在平台边缘的超时（秒），超时后退回 Patrol")]
    public float edgeWaitTimeout = 2f;

    [Tooltip("攻击结束后 Cooldown 状态的持续时间（秒）")]
    public float cooldownDuration = 0.5f;

    [Tooltip("检测范围的迟滞系数（超出 detectionRange × 此值才放弃追击），防玩家在边界反复横跳")]
    public float chaseHysteresis = 1.2f;

    [Tooltip("攻击距离的额外缓冲区（units），防贴身不出伤")]
    public float attackRangeBuffer = 0.5f;

    [Header("受击")]
    [Tooltip("击退抵抗系数（0=完全击退，1=完全抵抗）")]
    [Range(0f, 1f)]
    public float knockbackResistance = 0.5f;

    [Tooltip("受伤闪白持续时间（秒）—— 如使用动画帧则此字段无效")]
    public float hurtFlashDuration = 0.1f;

    [Header("层级")]
    [Tooltip("地面/平台层级")]
    public LayerMask groundLayer;

    [Tooltip("玩家层级（用于攻击检测和追击）")]
    public LayerMask playerLayer;
}
