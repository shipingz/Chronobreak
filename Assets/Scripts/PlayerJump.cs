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

    // 地面检测
    private Collider2D col;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        input = new InputSystem_Actions();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

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
        HandleVariableJump();
        ClampFallSpeed();
        ResetGravityWhenGrounded();
    }

    // ============================================================
    // 跳跃触发
    // ============================================================

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        if (!IsGrounded()) return;

        // 施加向上的初速度
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, config.jumpForce);
        jumpHeld = true;
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
    // 变跳高度 & 下落加速
    // ============================================================

    private void HandleVariableJump()
    {
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
        Vector2 origin = new Vector2(transform.position.x, col.bounds.min.y);
        RaycastHit2D hit = Physics2D.Raycast(
            origin, Vector2.down,
            config.groundCheckDistance,
            config.groundLayer
        );
        return hit.collider != null;
    }
}
