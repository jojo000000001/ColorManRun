using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 逻辑积分速度，CharacterController 负责全部移动与碰撞。
/// centerOffset 保留脚底 Pivot 模型的胶囊中心偏移。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("两关引用同一份 SO；运动与输入时序直接读取该资源，不复制成场景参数。")]
    public PlayerMovementSettings movementSettings;

    // Keep serialized values for old/unmigrated scenes without exposing a second tuning source.
    [SerializeField, HideInInspector, FormerlySerializedAs("baseSpeed")] float legacyBaseSpeed = 8f;
    [SerializeField, HideInInspector, FormerlySerializedAs("gravity")] float legacyGravity = -25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("jumpVelocity")] float legacyJumpVelocity = 8.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("longJumpVelocity")] float legacyLongJumpVelocity = 11.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("longJumpSpeedMul")] float legacyLongJumpSpeedMul = 1.45f;
    [SerializeField, HideInInspector, FormerlySerializedAs("longJumpHoldAcceleration")] float legacyLongJumpHoldAcceleration = 30f;
    [SerializeField, HideInInspector, FormerlySerializedAs("longJumpMaxUpwardSpeed")] float legacyLongJumpMaxUpwardSpeed = 16f;
    [SerializeField, HideInInspector, FormerlySerializedAs("deathY")] float legacyDeathY = -40f;
    [SerializeField, HideInInspector, FormerlySerializedAs("coyoteTime")] float legacyCoyoteTime = 0.08f;
    [SerializeField, HideInInspector, FormerlySerializedAs("jumpBufferTime")] float legacyJumpBufferTime = 0.1f;
    [SerializeField, HideInInspector, FormerlySerializedAs("longPressThreshold")] float legacyLongPressThreshold = 0.2f;
    [SerializeField, HideInInspector, FormerlySerializedAs("colorCooldownDuration")] float legacyColorCooldownDuration = 0.35f;

    // Read directly, including after scene changes or live Inspector tuning. Never write into the SO.
    public float baseSpeed => movementSettings != null ? movementSettings.baseSpeed : legacyBaseSpeed;
    public float gravity => movementSettings != null ? movementSettings.gravity : legacyGravity;
    public float jumpVelocity => movementSettings != null ? movementSettings.jumpVelocity : legacyJumpVelocity;
    public float longJumpVelocity => movementSettings != null ? movementSettings.longJumpVelocity : legacyLongJumpVelocity;
    public float longJumpSpeedMul => movementSettings != null ? movementSettings.longJumpSpeedMul : legacyLongJumpSpeedMul;
    public float longJumpHoldAcceleration => movementSettings != null ? movementSettings.longJumpHoldAcceleration : legacyLongJumpHoldAcceleration;
    public float longJumpMaxUpwardSpeed => movementSettings != null ? movementSettings.longJumpMaxUpwardSpeed : legacyLongJumpMaxUpwardSpeed;
    public float deathY => movementSettings != null ? movementSettings.deathY : legacyDeathY;
    public float coyoteTime => movementSettings != null ? movementSettings.coyoteTime : legacyCoyoteTime;
    public float jumpBufferTime => movementSettings != null ? movementSettings.jumpBufferTime : legacyJumpBufferTime;
    public float longPressThreshold => movementSettings != null ? movementSettings.longPressThreshold : legacyLongPressThreshold;
    public float colorCooldownDuration => movementSettings != null ? movementSettings.colorCooldownDuration : legacyColorCooldownDuration;

    [Header("角色与场景绑定（不共享）")]
    [Min(0.05f)] public float halfHeight = 1f;
    public LayerMask groundMask;
    public PlayerColorPalette palette;
    public Vector3 centerOffset;

    public int ColorIndex { get { return colorIndex; } }
    public bool IsGrounded { get { return grounded; } }
    public TrackPart Support { get { return grounded ? support : null; } }
    public Vector3 FeetPosition => transform.TransformPoint(centerOffset) - Vector3.up * halfHeight;

    // Visual observers receive only successfully executed actions, not raw input.
    public event System.Action JumpStarted;
    public event System.Action ColorChanged;

    float vy;
    float speedMul = 1f;
    float coyoteTimer;
    float jumpBufferTimer;
    float colorCooldown;
    float arrowPressTime;
    bool grounded;
    bool arrowTracking;
    bool arrowConsumed;
    bool arrowHeld;
    bool longJumpBoostActive;
    bool spaceNeedsRelease;
    bool arrowNeedsRelease;
    int lastJumpFrame = -1;
    int colorIndex;
    TrackPart support;
    Renderer fallbackRenderer;
    MaterialPropertyBlock colorBlock;
    CharacterController controller;
    GameManager subscribedManager;
    readonly RaycastHit[] groundHits = new RaycastHit[32];

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        if (movementSettings == null)
            Debug.LogWarning("PlayerController 未指定共享运动 SO，暂用旧场景参数。请绑定 Assets/SO/SharedPlayerMovement.asset。", this);

        controller = GetComponent<CharacterController>();
        controller.center = centerOffset;
        controller.height = halfHeight * 2f / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
        controller.radius = Mathf.Min(0.4f, controller.height * 0.45f);
        controller.skinWidth = 0.02f;
        controller.minMoveDistance = 0f;
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 50f;
        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.applyRootMotion = false;

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
        if (palette == null)
            palette = GetComponent<PlayerColorPalette>();

        fallbackRenderer = GetComponent<Renderer>();
        colorBlock = new MaterialPropertyBlock();
        ApplyColorVisual();
    }

    void OnEnable()
    {
        subscribedManager = GameManager.Instance;
        if (subscribedManager != null)
            subscribedManager.StateChanged += OnStateChanged;
        ResetPendingInput();
    }

    void OnStateChanged(GameState state)
    {
        ResetPendingInput();
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

        RefreshSupport();
        if (!IsPlaying())
            return;

        ProcessJumpInput(Input.GetKeyDown(KeyCode.Space), Input.GetKey(KeyCode.Space),
            Input.GetKeyDown(KeyCode.UpArrow), Input.GetKey(KeyCode.UpArrow),
            Input.GetKeyUp(KeyCode.UpArrow), Time.time);
        HandleColorInput();
        // 起跳只改变速度，不能借 grounded=false 跳过尚未离开的错误颜色支撑。
        JudgePhysicalSupport();
        if (IsPlaying())
            SimulateMovement(dt);
    }

    void ProcessJumpInput(bool spacePressed, bool spaceHeld, bool arrowPressed,
        bool held, bool released, float now)
    {
        if (!spaceHeld)
            spaceNeedsRelease = false;
        if (!held)
            arrowNeedsRelease = false;

        if (spacePressed && !spaceNeedsRelease)
            RequestJump(false);
        ProcessArrowUp(arrowPressed && !arrowNeedsRelease, held && !arrowNeedsRelease,
            released && !arrowNeedsRelease, now);
    }

    void ProcessArrowUp(bool pressed, bool held, bool released, float now)
    {
        arrowHeld = held && !released;
        if (!arrowHeld)
            longJumpBoostActive = false;

        if (pressed)
        {
            // A new press cannot resume the boost belonging to an older jump.
            longJumpBoostActive = false;
            // 空中起按整次忽略，不能在落地后补成长跳。
            arrowTracking = grounded || coyoteTimer > 0f;
            arrowConsumed = lastJumpFrame == Time.frameCount;
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
        if (longJump)
        {
            // 长跳只在阈值帧尝试一次，绝不进入落地缓冲。
            TryJump(true);
            return;
        }
        jumpBufferTimer = jumpBufferTime;
        TryBufferedJump();
    }

    void TryBufferedJump()
    {
        if (jumpBufferTimer > 0f)
            TryJump(false);
    }

    bool TryJump(bool longJump)
    {
        if ((!grounded && coyoteTimer <= 0f) || !IsPlaying() || lastJumpFrame == Time.frameCount)
            return false;

        lastJumpFrame = Time.frameCount;
        vy = longJump ? longJumpVelocity : jumpVelocity;
        speedMul = longJump ? longJumpSpeedMul : 1f;
        longJumpBoostActive = longJump && arrowTracking && arrowConsumed && arrowHeld;
        // 成功的 Space/普通缓冲跳消耗同次 ↑，松开/阈值均不再请求第二跳。
        if (!longJump && arrowTracking)
            arrowConsumed = true;
        grounded = false;
        support = null;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        JumpStarted?.Invoke();
        return true;
    }

    void JudgePhysicalSupport()
    {
        TrackPart part;
        if (FindSupport(out part))
            JudgeColor(part);
    }

    bool FindSupport(out TrackPart part)
    {
        part = null;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        Vector3 bottomCenter = FeetPosition + Vector3.up * radius;
        // 从足够高处扫掠，避免斜坡上的起始球重叠；零距离命中没有可靠表面法线。
        float lift = Mathf.Max(0.3f, radius);
        int count = Physics.SphereCastNonAlloc(bottomCenter + Vector3.up * lift,
            Mathf.Max(0.01f, radius - 0.005f), Vector3.down, groundHits, lift + 0.05f,
            groundMask, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        int closestId = int.MaxValue;
        float minNormal = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == controller || hit.distance <= 0.0001f || hit.normal.y < minNormal)
                continue;
            int id = hit.collider.GetInstanceID();
            if (hit.distance > closest + 0.0001f ||
                (Mathf.Abs(hit.distance - closest) <= 0.0001f && id >= closestId))
                continue;
            closest = hit.distance;
            closestId = id;
            part = hit.collider.GetComponentInParent<TrackPart>();
        }
        return closest < float.PositiveInfinity;
    }

    void RefreshSupport()
    {
        TrackPart part;
        if (vy > 0f || !FindSupport(out part))
        {
            grounded = false;
            support = null;
            return;
        }
        grounded = true;
        support = part;
        coyoteTimer = coyoteTime;
        vy = 0f;
        speedMul = 1f;
        longJumpBoostActive = false;
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

        ColorChanged?.Invoke();

        JudgePhysicalSupport();
    }

    void SimulateMovement(float dt)
    {
        // 小步保持原有助推积分；每步由控制器扫掠，墙面/顶面均真实阻挡。
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 120f)), 1, 128);
        float step = dt / steps;
        for (int i = 0; i < steps && IsPlaying(); i++)
        {
            if (!grounded)
                coyoteTimer = Mathf.Max(0f, coyoteTimer - step);
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - step);

            float maxBoostSpeed = Mathf.Max(0f, longJumpMaxUpwardSpeed);
            bool applyBoost = longJumpBoostActive && arrowHeld && !grounded;
            if (applyBoost && vy >= maxBoostSpeed)
            {
                longJumpBoostActive = false;
                applyBoost = false;
            }

            float accelerationY = gravity;
            if (applyBoost)
                accelerationY += longJumpHoldAcceleration;
            vy += accelerationY * step;
            if (applyBoost && vy >= maxBoostSpeed)
            {
                // End this jump's boost, rather than hovering at the cap or restarting it next step.
                vy = maxBoostSpeed;
                longJumpBoostActive = false;
            }
            float verticalMove = grounded ? -Mathf.Max(2f, baseSpeed * speedMul) * step : vy * step;
            CollisionFlags flags = controller.Move(new Vector3(0f, verticalMove, baseSpeed * speedMul * step));
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f)
            {
                vy = 0f;
                longJumpBoostActive = false;
            }
            RefreshSupport();
            JudgePhysicalSupport();

            // 先判断落地颜色，再消费缓冲，不能靠缓冲跳过错误颜色长条。
            TryBufferedJump();
            if (transform.position.y < deathY)
                GameManager.Instance.GameOver("掉下去了");
        }
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
        arrowHeld = false;
        longJumpBoostActive = false;
        jumpBufferTimer = 0f;
        // 新场景/恢复/重启前已经按住的键，必须先松开后重新按下。
        spaceNeedsRelease = true;
        arrowNeedsRelease = true;
    }

    void OnDisable()
    {
        if (subscribedManager != null)
            subscribedManager.StateChanged -= OnStateChanged;
        ResetPendingInput();
    }

    bool IsPlaying()
    {
        return GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
    }
}
