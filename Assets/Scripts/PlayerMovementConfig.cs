using UnityEngine;

/// <summary>
/// 玩家移动参数配置（ScriptableObject）
/// 所有数值集中管理，Inspector 可视化调参
/// </summary>
[CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Chronobreak/Player Movement Config")]
public class PlayerMovementConfig : ScriptableObject
{
    [Header("水平移动")]
    [Tooltip("最大水平移动速度（units/s）")]
    public float maxSpeed = 8f;

    [Tooltip("地面加速力度（越大手感越「硬」）")]
    public float acceleration = 60f;

    [Tooltip("松手后减速力度")]
    public float groundDeceleration = 40f;

    [Tooltip("空中控制倍率（0~1，1=地面手感）")]
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.6f;

    [Header("下落")]
    [Tooltip("最大下落速度限制（防穿墙）")]
    public float maxFallSpeed = 15f;

    [Header("地面检测")]
    [Tooltip("检测地面的射线长度（从碰撞体底部算起）")]
    public float groundCheckDistance = 0.15f;

    [Tooltip("地面所在层级")]
    public LayerMask groundLayer;

    [Header("跳跃")]
    [Tooltip("跳跃初始向上的速度（~7 = 跳2.5倍身高）")]
    public float jumpForce = 7f;

    [Tooltip("上升时重力缩放（1=正常重力，上升自然减速）")]
    [Range(0f, 1f)]
    public float variableJumpGravityScale = 1f;

    [Tooltip("下落时重力倍率（1=正常，>1 下落更快）")]
    public float fallGravityMultiplier = 1.15f;

    [Header("Coyote Time & 跳跃缓冲")]
    [Tooltip("离开地面后仍可跳跃的时间（秒）。经典值 0.1s")]
    public float coyoteTime = 0.1f;

    [Tooltip("落地前按下跳跃的缓存时间（秒）。经典值 0.05s")]
    public float jumpBufferTime = 0.05f;

    [Header("冲刺")]
    [Tooltip("冲刺距离（units）")]
    public float dashDistance = 3f;

    [Tooltip("冲刺时长（秒）")]
    public float dashDuration = 0.2f;

    [Tooltip("冲刺冷却时间（秒）")]
    public float dashCooldown = 0.8f;

    [Tooltip("冲刺无敌时间（秒），冲刺前段生效")]
    public float dashInvincibilityTime = 0.15f;
}
