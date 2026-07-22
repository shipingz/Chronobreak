using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家冲刺 — 固定距离/墙体检测/无敌帧/冷却/攻击可取消（T-006）
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDash : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private PlayerMovementConfig config;
    [SerializeField] private Rigidbody2D rb;

    // 输入
    private InputSystem_Actions input;
    private InputAction dashAction;
    private Vector2 moveInput;

    // 碰撞体缓存
    private Collider2D cachedCollider;
    private SpriteRenderer cachedSpriteRenderer;

    // 冲刺状态
    private bool isDashing;
    private float dashTimer;
    private float cooldownTimer;
    private int dashDirection; // 1 或 -1
    private float originalGravity;
    private bool originalIsTrigger; // 记录冲刺前的 isTrigger 状态，结束时恢复
    private bool triggerActivatedThisDash; // 标记本轮冲刺是否已打开 Trigger

    // 公开状态供其他组件查询（PlayerJump/PlayerHealth）
    public bool IsDashing => isDashing;
    public bool IsInvincible => isDashing && dashTimer > config.dashDuration - config.dashInvincibilityTime;

    // 计算属性
    private float DashSpeed => config.dashDistance / config.dashDuration;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        input = new InputSystem_Actions();
        dashAction = input.asset.FindActionMap("Player").FindAction("Dash");
        if (dashAction == null)
            Debug.LogError("Dash action not found! 请确认 InputSystem_Actions 中已添加 Dash 动作", this);

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        cachedCollider = GetComponent<Collider2D>();
        cachedSpriteRenderer = GetComponent<SpriteRenderer>();

        originalGravity = rb.gravityScale;
    }

    private void OnEnable()
    {
        if (dashAction != null) dashAction.started += OnDashStarted;
        input.Player.Enable();
    }

    private void OnDisable()
    {
        if (dashAction != null) dashAction.started -= OnDashStarted;
        input.Player.Disable();
    }

    private void OnDestroy()
    {
        input?.Dispose();
    }

    private void Update()
    {
        // 逐帧读取移动输入（用于确定冲刺方向）
        moveInput = input.Player.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            UpdateDash();
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.fixedDeltaTime;
        }
    }

    // ============================================================
    // 冲刺触发
    // ============================================================

    private void OnDashStarted(InputAction.CallbackContext ctx)
    {
        if (isDashing || cooldownTimer > 0f) return;

        // 确定冲刺方向：优先移动输入，回退到精灵朝向
        dashDirection = (int)Mathf.Sign(moveInput.x);
        if (dashDirection == 0)
        {
            dashDirection = cachedSpriteRenderer != null && cachedSpriteRenderer.flipX ? -1 : 1;
        }

        StartDash();
    }

    // ============================================================
    // 冲刺执行
    // ============================================================

    private void StartDash()
    {
        isDashing = true;
        dashTimer = config.dashDuration;
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(dashDirection * DashSpeed, 0f);
        triggerActivatedThisDash = false;

        // 先保存原始 isTrigger，但不立即修改
        // 等第一次 UpdateDash 确认前方没墙之后再开——否则会穿进墙体
        if (cachedCollider != null)
            originalIsTrigger = cachedCollider.isTrigger;
    }

    private void UpdateDash()
    {
        dashTimer -= Time.fixedDeltaTime;

        // 墙体检测 → 碰墙即停（优先级最高）
        if (CheckWallAhead())
        {
            EndDash();
            return;
        }

        // 确认前方没墙 + 在无敌期内 → 才开 Trigger 穿越非 ground 物体
        if (!triggerActivatedThisDash && IsInvincible && cachedCollider != null)
        {
            cachedCollider.isTrigger = true;
            triggerActivatedThisDash = true;
        }

        // 无敌帧结束 → 碰撞体恢复
        if (cachedCollider != null && cachedCollider.isTrigger && !IsInvincible)
        {
            cachedCollider.isTrigger = originalIsTrigger;
        }

        // 时长耗尽
        if (dashTimer <= 0f)
        {
            EndDash();
            return;
        }

        // 维持冲刺速度（抵消 AddForce 等外力干扰）
        rb.linearVelocity = new Vector2(dashDirection * DashSpeed, 0f);
    }

    private void EndDash()
    {
        isDashing = false;
        rb.gravityScale = originalGravity;
        cooldownTimer = config.dashCooldown;

        // 确保碰撞体恢复（可能在无敌期内终止）
        if (cachedCollider != null)
            cachedCollider.isTrigger = originalIsTrigger;

        // 保留少量水平惯性（防止突然卡死）
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, 0f);
    }

    // ============================================================
    // 墙体检测
    // ============================================================

    private bool CheckWallAhead()
    {
        if (cachedCollider == null) return false;

        // 从碰撞体前边缘的上/中/下三个高度分别发射射线
        // 单根射线可能因平台边缘刚好在中心高度而漏过
        float frontX = cachedCollider.bounds.center.x + dashDirection * cachedCollider.bounds.extents.x;
        float topY = cachedCollider.bounds.max.y - 0.05f;
        float midY = cachedCollider.bounds.center.y;
        float botY = cachedCollider.bounds.min.y + 0.05f;

        float checkDistance = DashSpeed * Time.fixedDeltaTime + 0.1f;
        Vector2 direction = Vector2.right * dashDirection;

        RaycastHit2D hitTop = Physics2D.Raycast(new Vector2(frontX, topY), direction, checkDistance, config.groundLayer);
        RaycastHit2D hitMid = Physics2D.Raycast(new Vector2(frontX, midY), direction, checkDistance, config.groundLayer);
        RaycastHit2D hitBot = Physics2D.Raycast(new Vector2(frontX, botY), direction, checkDistance, config.groundLayer);

        return hitTop.collider != null || hitMid.collider != null || hitBot.collider != null;
    }

    // ============================================================
    // 外部取消（供 PlayerAttack 调用）
    // ============================================================

    public void CancelDash()
    {
        if (isDashing)
            EndDash();
    }

}
