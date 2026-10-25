using UnityEngine;

/// <summary>
/// 手动积分移动；刚体和 Trigger 胶囊只负责终点触发，不驱动位移。
/// centerOffset 允许脚底 Pivot 的 FBX 使用与胶囊相同的中心落地检测。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public float baseSpeed = 8f;
    public float gravity = -25f;
    public float jumpVelocity = 8.5f;
    public float longJumpVelocity = 11.5f;
    public float longJumpSpeedMul = 1.45f;
    [Min(0.05f)] public float halfHeight = 1f;
    public float deathY = -8f;
    public LayerMask groundMask;
    public PlayerColorPalette palette;
    public Vector3 centerOffset;

    public float coyoteTime = 0.08f;
    public float jumpBufferTime = 0.1f;
    public float longPressThreshold = 0.2f;
    public float colorCooldownDuration = 0.35f;

    public int ColorIndex { get { return colorIndex; } }
    public bool IsGrounded { get { return grounded; } }

    float vy;
    float speedMul = 1f;
    float coyoteTimer;
    float jumpBufferTimer;
    float colorCooldown;
    float arrowPressTime;
    bool grounded;
    bool bufferedLongJump;
    bool arrowTracking;
    bool arrowConsumed;
    int colorIndex;
    TrackPart support;
    Renderer fallbackRenderer;
    MaterialPropertyBlock colorBlock;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.direction = 1;
        capsule.center = centerOffset;
        capsule.height = halfHeight * 2f / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
        capsule.radius = Mathf.Min(0.4f, capsule.height * 0.45f);

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
        if (palette == null)
            palette = GetComponent<PlayerColorPalette>();

        fallbackRenderer = GetComponent<Renderer>();
        colorBlock = new MaterialPropertyBlock();
        ApplyColorVisual();
    }

    void Update()
    {
        if (!IsPlaying())
        {
            ResetPendingInput();
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;
        colorCooldown = Mathf.Max(0f, colorCooldown - dt);

        Vector3 position = transform.position;
        ProbeGround(ref position);
        transform.position = position;
        if (!IsPlaying())
            return;

        HandleSpace();
        HandleArrowUp();
        HandleColorInput();
        if (IsPlaying())
            SimulateMovement(dt);
    }

    void HandleSpace()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            RequestJump(false);
    }

    // 与空格分开：短按只在松开时跳，长按达到阈值就消耗本次按键。
    void HandleArrowUp()
    {
        ProcessArrowUp(Input.GetKeyDown(KeyCode.UpArrow), Input.GetKey(KeyCode.UpArrow),
            Input.GetKeyUp(KeyCode.UpArrow), Time.time);
    }

    void ProcessArrowUp(bool pressed, bool held, bool released, float now)
    {
        if (pressed)
        {
            arrowTracking = true;
            arrowConsumed = false;
            arrowPressTime = now;
        }

        if (!arrowTracking)
            return;

        // 也检查松开帧，避免低帧率下跨过阈值却误判为短按。
        if (!arrowConsumed && (held || released) && now - arrowPressTime >= longPressThreshold)
        {
            arrowConsumed = true;
            RequestJump(true);
        }

        if (released)
        {
            if (!arrowConsumed)
                RequestJump(false);
            arrowTracking = false;
            arrowConsumed = false;
        }
    }

    void RequestJump(bool longJump)
    {
        bufferedLongJump = longJump;
        jumpBufferTimer = jumpBufferTime;
        TryBufferedJump();
    }

    void TryBufferedJump()
    {
        if (jumpBufferTimer <= 0f || (!grounded && coyoteTimer <= 0f) || !IsPlaying())
            return;

        vy = bufferedLongJump ? longJumpVelocity : jumpVelocity;
        speedMul = bufferedLongJump ? longJumpSpeedMul : 1f;
        grounded = false;
        support = null;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
    }

    void HandleColorInput()
    {
        HandleLeftArrow(Input.GetKeyDown(KeyCode.LeftArrow));

        // 一帧只处理一次键盘颜色请求，不接受额外按键。
        if (Input.GetKeyDown(KeyCode.Alpha1))
            TrySetColor(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            TrySetColor(1);
        else if (Input.GetKeyDown(KeyCode.C))
            TrySetColor((colorIndex + 1) % PlayerColorPalette.ColorCount);
    }

    void HandleLeftArrow(bool pressed)
    {
        if (!pressed)
            return;
        TrySetColor((colorIndex + 1) % PlayerColorPalette.ColorCount);
    }

    void TrySetColor(int index)
    {
        // 冷却期间直接丢弃，不保存请求，也不延长当前冷却。
        if (!IsPlaying() || colorCooldown > 0f || index < 0 || index >= PlayerColorPalette.ColorCount)
            return;
        if (index == colorIndex)
            return;

        colorIndex = index;
        colorCooldown = colorCooldownDuration;
        ApplyColorVisual();

        if (grounded)
            JudgeColor(support);
    }

    void SimulateMovement(float dt)
    {
        // 小步积分避免低帧率时中心穿到薄跑道下面，漏掉向下射线。
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 120f)), 1, 128);
        float step = dt / steps;
        Vector3 position = transform.position;
        for (int i = 0; i < steps && IsPlaying(); i++)
        {
            if (!grounded)
                coyoteTimer = Mathf.Max(0f, coyoteTimer - step);
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - step);

            position.z += baseSpeed * speedMul * step;
            vy += gravity * step;
            position.y += vy * step;
            ProbeGround(ref position);

            // 先判断落地颜色，再消费缓冲，不能靠缓冲跳过错误颜色长条。
            TryBufferedJump();
            if (position.y < deathY)
                GameManager.Instance.GameOver("掉下去了");
        }
        transform.position = position;
    }

    void ProbeGround(ref Vector3 position)
    {
        RaycastHit hit = default(RaycastHit);
        Vector3 centerOffsetWorld = transform.TransformVector(centerOffset);
        Vector3 center = position + centerOffsetWorld;
        bool hitGround = vy <= 0f && Physics.Raycast(center, Vector3.down, out hit,
            halfHeight + 0.05f, groundMask, QueryTriggerInteraction.Ignore);

        if (!hitGround)
        {
            grounded = false;
            support = null;
            return;
        }

        TrackPart part = hit.collider.GetComponentInParent<TrackPart>();
        bool landedOrChangedPart = !grounded || part != support;
        position.y = hit.point.y + halfHeight - centerOffsetWorld.y;
        vy = 0f;
        speedMul = 1f;
        grounded = true;
        coyoteTimer = coyoteTime;
        support = part;

        if (landedOrChangedPart)
            JudgeColor(part);
    }

    void JudgeColor(TrackPart part)
    {
        if (part != null && part.colorIndex != colorIndex)
            GameManager.Instance.GameOver("颜色不匹配");
    }

    void ApplyColorVisual()
    {
        // 模型实际切换已有的橙/粉材质组，不再只覆盖 Shader 的颜色属性。
        if (palette != null && palette.ApplyColor(colorIndex))
            return;

        // 胶囊/Cube 没有模型调色板时，仍可直接显示逻辑颜色。
        Color color = TrackPart.ColorOf(colorIndex);
        if (fallbackRenderer != null)
        {
            fallbackRenderer.GetPropertyBlock(colorBlock);
            colorBlock.SetColor(ColorId, color);
            colorBlock.SetColor(BaseColorId, color);
            fallbackRenderer.SetPropertyBlock(colorBlock);
        }

    }

    void ResetPendingInput()
    {
        arrowTracking = false;
        arrowConsumed = false;
        jumpBufferTimer = 0f;
    }

    void OnDisable()
    {
        ResetPendingInput();
    }

    bool IsPlaying()
    {
        return GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
    }
}
