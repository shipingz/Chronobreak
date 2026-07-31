using UnityEngine;

/// <summary>
/// 可受击对象接口（T-011 依赖，T-012 敌人实现）
/// </summary>
public interface IDamageable
{
    void TakeDamage(int damage, Vector2 knockbackDirection);
}
