using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家跳跃 — 变跳高度 + 下降重力加速（T-004）
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerJump : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerMovementConfig config;
    [SerializeField] private Rigidbody2D rb;

    // 输入
    private InputSystem_Actions input;
    private bool jumpHeld;          // 当前是否按住跳跃

    // 重力
    private float defaultGravity;

    // 碰撞体缓存
    private Collider2D cachedCollider;

    // Coyote Time & 跳跃缓冲
    private float coyoteTimer;
    private float jumpBufferTimer;

    // 缓存引用
    private PlayerDash cachedPlayerDash;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        input = new InputSystem_Actions();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        cachedCollider = GetComponent<Collider2D>();
        cachedPlayerDash = GetComponent<PlayerDash>();

        defaultGravity = rb.gravityScale;
    }

    private void OnEnable()
    {
        input.Player.Jump.started += OnJumpStarted;
        input.Player.Jump.canceled += OnJumpCanceled;
        input.Enable();
    }

    private void OnDisable()
    {
        input.Player.Jump.started -= OnJumpStarted;
        input.Player.Jump.canceled -= OnJumpCanceled;
        input.Disable();
    }

    private void OnDestroy()
    {
        input?.Dispose();
    }

    private void FixedUpdate()
    {
        UpdateTimers();
        HandleVariableJump();
        ClampFallSpeed();
        ResetGravityWhenGrounded();
    }

    // ============================================================
    // 跳跃触发
    // ============================================================

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        if (IsGrounded() || coyoteTimer > 0f)
        {
            // 在地面或 Coyote Time 窗口内 → 直接跳
            PerformJump();
        }
        else
        {
            // 在空中 → 缓存跳跃请求，落地时自动触发
            jumpBufferTimer = config.jumpBufferTime;
        }
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        jumpHeld = false;

        // 松手时如果还在上升 → 砍速度，实现变跳高度
        if (rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }
    }

    // ============================================================
    // 跳跃执行（Coyote / 缓冲 共用入口）
    // ============================================================

    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, config.jumpForce);
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;

        // 同步按键状态：缓冲触发时 jumpHeld 还未设为 true
        if (input.Player.Jump.IsPressed())
        {
            jumpHeld = true;
        }
    }

    // ============================================================
    // Coyote Time & 跳跃缓冲 计时
    // ============================================================

    private void UpdateTimers()
    {
        if (IsGrounded())
        {
            coyoteTimer = config.coyoteTime;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
        }

        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.fixedDeltaTime;
            if (IsGrounded())
            {
                PerformJump();
            }
        }
    }

    // ============================================================
    // 变跳高度 & 下落加速
    // ============================================================

    private void HandleVariableJump()
    {
        // 冲刺期间不干预重力
        if (cachedPlayerDash?.IsDashing == true) return;

        // 长按期间减少重力 → 跳得更高
        if (jumpHeld && rb.linearVelocity.y > 0f)
        {
            rb.gravityScale = defaultGravity * config.variableJumpGravityScale;
        }
        // 下落时加重重力 → 更干脆
        else if (rb.linearVelocity.y < 0f)
        {
            rb.gravityScale = defaultGravity * config.fallGravityMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravity;
        }
    }

    // ============================================================
    // 下落速度上限
    // ============================================================

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -config.maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -config.maxFallSpeed);
        }
    }

    // ============================================================
    // 落地重置
    // ============================================================

    private void ResetGravityWhenGrounded()
    {
        // 冲刺期间不干预重力
        if (cachedPlayerDash?.IsDashing == true) return;

        if (IsGrounded() && rb.linearVelocity.y <= 0f)
        {
            rb.gravityScale = defaultGravity;
        }
    }

    // ============================================================
    // 地面检测
    // ============================================================

    private bool IsGrounded()
    {
        if (cachedCollider == null) return false;

        // 从碰撞体底部的左右两端分别向下发射射线
        float leftX = cachedCollider.bounds.min.x + 0.05f;
        float rightX = cachedCollider.bounds.max.x - 0.05f;
        float footY = cachedCollider.bounds.min.y;

        RaycastHit2D hitLeft = Physics2D.Raycast(
            new Vector2(leftX, footY), Vector2.down,
            config.groundCheckDistance, config.groundLayer
        );
        RaycastHit2D hitRight = Physics2D.Raycast(
            new Vector2(rightX, footY), Vector2.down,
            config.groundCheckDistance, config.groundLayer
        );

        return hitLeft.collider != null || hitRight.collider != null;
    }
}
