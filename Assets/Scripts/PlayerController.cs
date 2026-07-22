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

    // 缓存引用
    private Collider2D cachedCollider;
    private PlayerDash cachedPlayerDash;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        input = new InputSystem_Actions();
        spriteRenderer = GetComponent<SpriteRenderer>();
        cachedCollider = GetComponent<Collider2D>();
        cachedPlayerDash = GetComponent<PlayerDash>();

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
        if (cachedCollider == null) return;

        // 从碰撞体底部的左右两端分别向下发射射线
        // 单根中线可能因平台边缘刚好在脚底中间而漏过
        float leftX = cachedCollider.bounds.min.x + 0.05f;
        float rightX = cachedCollider.bounds.max.x - 0.05f;
        float footY = cachedCollider.bounds.min.y;
        float rayLength = config.groundCheckDistance;

        RaycastHit2D hitLeft = Physics2D.Raycast(
            new Vector2(leftX, footY),
            Vector2.down, rayLength, config.groundLayer
        );
        RaycastHit2D hitRight = Physics2D.Raycast(
            new Vector2(rightX, footY),
            Vector2.down, rayLength, config.groundLayer
        );

        isGrounded = hitLeft.collider != null || hitRight.collider != null;
    }

    // ============================================================
    // 水平移动（加速度模型）
    // ============================================================

    private void HandleHorizontalMovement()
    {
        // 冲刺期间不干涉，由 PlayerDash 接管水平速度
        if (cachedPlayerDash?.IsDashing == true) return;

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

        // 优先用缓存的碰撞体（运行时），回退到 GetComponent（Edit Mode）
        Collider2D col = cachedCollider;
        if (col == null) col = GetComponent<Collider2D>();
        if (col == null) return;

        // 与 CheckGrounded() 保持一致的检测：脚底左右两端
        float leftX = col.bounds.min.x + 0.05f;
        float rightX = col.bounds.max.x - 0.05f;
        float footY = col.bounds.min.y;

        Gizmos.color = isGrounded ? Color.green : Color.red;

        Vector2 leftOrigin = new Vector2(leftX, footY);
        Gizmos.DrawLine(leftOrigin, leftOrigin + Vector2.down * config.groundCheckDistance);
        Gizmos.DrawSphere(leftOrigin, 0.05f);

        Vector2 rightOrigin = new Vector2(rightX, footY);
        Gizmos.DrawLine(rightOrigin, rightOrigin + Vector2.down * config.groundCheckDistance);
        Gizmos.DrawSphere(rightOrigin, 0.05f);
    }
}
