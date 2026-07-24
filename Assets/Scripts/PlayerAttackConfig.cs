using UnityEngine;

/// <summary>
/// 攻击参数配置（ScriptableObject）
/// 所有数值集中管理，Inspector 可视化调参
/// </summary>
[CreateAssetMenu(fileName = "PlayerAttackConfig", menuName = "Chronobreak/Player Attack Config")]
public class PlayerAttackConfig : ScriptableObject
{
    [Header("伤害判定")]
    [Tooltip("判定框大小 (宽x高)")]
    public Vector2 hitboxSize = new Vector2(1.5f, 1.0f);

    [Tooltip("判定框在角色前方的偏移量")]
    public float hitboxForwardOffset = 0.8f;

    [Tooltip("判定框垂直偏移（正数往上）")]
    public float hitboxVerticalOffset = 0.2f;

    [Tooltip("每次攻击的伤害值")]
    public int damage = 20;

    [Tooltip("命中击退力度")]
    public float knockbackForce = 5f;

    [Header("时序")]
    [Tooltip("按下攻击键到判定框激活的延迟（秒）")]
    public float hitDelay = 0.15f;

    [Tooltip("判定框持续有效的时间（秒）")]
    public float hitActiveDuration = 0.1f;

    [Tooltip("攻击结束后的冷却时间（秒）")]
    public float cooldown = 0.2f;

    [Header("目标")]
    [Tooltip("可被攻击的目标层级")]
    public LayerMask enemyLayer;
}
