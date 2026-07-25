using UnityEngine;

/// <summary>
/// 玩家角色属性配置（ScriptableObject）
///
/// 存放角色"属性类"参数：生命值、攻击伤害、击退力度。
/// 与移动物理参数（PlayerMovementConfig）和攻击判定几何参数（PlayerAttackConfig）分离。
/// </summary>
[CreateAssetMenu(fileName = "PlayerConfig", menuName = "Chronobreak/Player Config")]
public class PlayerConfig : ScriptableObject
{
    [Header("生命值")]
    [Tooltip("玩家最大生命值")]
    public int maxHP = 100;

    [Tooltip("受击后无敌时间（秒）")]
    public float invincibilityDuration = 0.5f;

    [Tooltip("受击硬直时间（秒）：硬直期间不受玩家输入控制，让击退飞完")]
    public float hitStunDuration = 0.25f;

    [Header("攻击")]
    [Tooltip("每次攻击的伤害值")]
    public int attackDamage = 20;

    [Tooltip("命中横向击退力度")]
    public float knockbackForce = 5f;

    [Tooltip("命中向上击退力度")]
    public float knockbackUpwardForce = 3f;
}
