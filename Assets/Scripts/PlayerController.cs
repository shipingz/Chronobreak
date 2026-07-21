using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家控制器 — 水平移动（T-003）
/// 依赖 PlayerMovementConfig（ScriptableObject）管理数值
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerMovementConfig config;

    // 输入系统
    private InputSystem_Actions input;
    private Vector2 moveInput;
    private SpriteRenderer spriteRenderer;

    // 状态
    private bool isGrounded;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        input = new InputSystem_Actions();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (config == null)
            Debug.LogError("PlayerMovementConfig 未赋值！请在 Inspector 拖拽配置", this);
    }

    private void OnEnable() => input.Player.Enable();
    private void OnDisable() => input.Player.Disable();
    private void OnDestroy() => input?.Dispose();

    private void Update()
    {
        // 逐帧读取输入（Update 比 FixedUpdate 更及时）
        moveInput = input.Player.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        HandleHorizontalMovement();
        ClampFallSpeed();
    }

    // ============================================================
    // 地面检测
    // ============================================================

    private void CheckGrounded()
    {
        // 从碰撞体底部向下发射射线
        Collider2D col = GetComponent<Collider2D>();
        Vector2 origin = new Vector2(transform.position.x, col.bounds.min.y);
        float rayLength = config.groundCheckDistance;

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            rayLength,
            config.groundLayer
        );

        isGrounded = hit.collider != null;
    }

    // ============================================================
    // 水平移动（加速度模型）
    // ============================================================

    private void HandleHorizontalMovement()
    {
        float targetSpeed = moveInput.x * config.maxSpeed;

        // 速度差 = 目标速度 - 当前速度
        float speedDiff = targetSpeed - rb.linearVelocity.x;

        // 选择加速率：地面 vs 空中
        float accel = isGrounded
            ? config.acceleration
            : config.acceleration * config.airControlMultiplier;

        // 松手时用减速率（仅地面）
        if (Mathf.Approximately(moveInput.x, 0f) && isGrounded)
            accel = config.groundDeceleration;

        // 施加水平力
        float moveForce = speedDiff * accel;
        rb.AddForce(new Vector2(moveForce, 0f), ForceMode2D.Force);
    }

    // ============================================================
    // 下落速度上限（防穿墙）
    // ============================================================

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -config.maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -config.maxFallSpeed);
        }
    }

    // ============================================================
    // 调试可视化
    // ============================================================

    private void OnDrawGizmosSelected()
    {
        if (config == null) return;

        // 画出地面检测射线
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.down * config.groundCheckDistance
        );
    }
}
