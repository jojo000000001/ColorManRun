using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Keyboard hints only. Reads gameplay state; never handles input or moves the player.</summary>
[DisallowMultipleComponent]
public sealed class JumpTutorialHud : MonoBehaviour
{
    public enum HintKind { Tap, Hold, ChangeColor, JumpAndChangeColor }

    [Serializable]
    public sealed class TutorialPoint
    {
        [Tooltip("这段跑道或障碍的 Collider；运行时使用其世界包围盒前后沿。")]
        public Collider obstacle;
        public HintKind hint;
        [Min(0f), Tooltip("提前提示秒数；触发距离 = 实际前进速度 × 此值。0 表示进入这段才出现。")]
        public float leadSeconds = 1.5f;
        [Min(0f), Tooltip("进入前沿后再显示多少米；0 表示持续到整段跑道后沿。衔接提示可设为 8。")]
        public float endAfterFrontMeters;
        [Tooltip("这段提示期间，角色在空中时放慢 Time.timeScale，落地后恢复。")]
        public bool slowMotion;
    }

    [SerializeField] PlayerController player;
    [SerializeField] RectTransform jumpIcon;
    [SerializeField] RectTransform colorIcon;
    [SerializeField] CanvasGroup tutorialGroup;
    [SerializeField] Text tutorialLabel;
    [SerializeField] Text[] chineseLabels = new Text[0];
    [SerializeField] TutorialPoint[] tutorials = new TutorialPoint[0];
    [SerializeField, Min(0.01f)] float fadeSeconds = 0.2f;
    [SerializeField, Range(0.2f, 1f)] float colorHintTimeScale = 0.5f;

    bool[] consumed;
    int activeIndex = -1;
    HintKind shownHint;
    bool slowUntilLanding;
    bool slowApplied;
    float phase;
    bool hasMotionSample;
    float previousPlayerZ;
    GameManager manager;
    GameState lastState;
    Font runtimeFont;

    void Awake()
    {
        // The built-in Latin font is not guaranteed to contain Chinese glyphs.
        runtimeFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Noto Sans CJK SC", "SimHei", "Arial" }, 64);
        foreach (Text label in chineseLabels)
            if (label != null) label.font = runtimeFont;
    }

    void OnEnable()
    {
        manager = GameManager.Instance;
        lastState = manager != null ? manager.State : GameState.Ready;
        ResetRun();
        if (manager != null) manager.StateChanged += OnStateChanged;
    }

    void OnDisable()
    {
        if (manager != null) manager.StateChanged -= OnStateChanged;
        HideImmediately();
    }

    void OnDestroy()
    {
        if (runtimeFont != null) Destroy(runtimeFont);
    }

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Ready || (state == GameState.Playing && lastState != GameState.Paused))
            ResetRun();
        else if (state == GameState.Dead || state == GameState.Clear)
            HideImmediately();
        lastState = state;
    }

    void ResetRun()
    {
        consumed = new bool[tutorials.Length];
        activeIndex = -1;
        shownHint = HintKind.Tap;
        phase = 0f;
        hasMotionSample = false;
        previousPlayerZ = player != null ? player.transform.position.z : 0f;
        HideImmediately();
    }

    void HideImmediately()
    {
        if (tutorialGroup != null) tutorialGroup.alpha = 0f;
        if (jumpIcon != null) jumpIcon.localScale = Vector3.one;
        if (colorIcon != null) colorIcon.localScale = Vector3.one;
        slowUntilLanding = false;
        ApplySlowMotion(false);
    }

    void LateUpdate()
    {
        if (manager == null || player == null) return;
        float playerZ = player.transform.position.z;
        if (manager.State != GameState.Playing)
        {
            previousPlayerZ = playerZ;
            ApplySlowMotion(false);
            return;
        }
        Tick(manager.State, playerZ, SampleForwardSpeed(playerZ, Time.deltaTime), Time.deltaTime, !player.IsGrounded);
    }

    float SampleForwardSpeed(float playerZ, float deltaTime)
    {
        // PlayerController calls Move several times per frame; controller.velocity only
        // describes the last Move. Use the whole frame's displacement instead.
        float speed = deltaTime > 0f ? Mathf.Max(0f, (playerZ - previousPlayerZ) / deltaTime) : 0f;
        previousPlayerZ = playerZ;
        if (!hasMotionSample && speed <= 0f && player != null) speed = player.baseSpeed;
        hasMotionSample = true;
        return speed;
    }

    void Tick(GameState state, float playerZ, float speed, float deltaTime, bool airborne)
    {
        // Pause is not implemented through timeScale in this project.
        if (state != GameState.Playing)
        {
            ApplySlowMotion(false);
            return;
        }

        if (activeIndex >= 0)
        {
            TutorialPoint point = tutorials[activeIndex];
            Collider obstacle = point.obstacle;
            if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy ||
                playerZ > EndZ(point))
                activeIndex = -1;
        }

        if (activeIndex < 0)
        {
            float nearestFront = float.PositiveInfinity;
            for (int i = 0; i < tutorials.Length; i++)
            {
                TutorialPoint point = tutorials[i];
                if (consumed[i] || point == null || point.obstacle == null ||
                    !point.obstacle.enabled || !point.obstacle.gameObject.activeInHierarchy) continue;
                Bounds bounds = point.obstacle.bounds;
                if (playerZ > EndZ(point))
                {
                    consumed[i] = true; // Do not show tutorials behind the spawn point.
                    continue;
                }
                if (bounds.min.z - playerZ <= Mathf.Max(0f, speed) * Mathf.Max(0f, point.leadSeconds) &&
                    bounds.min.z < nearestFront)
                {
                    activeIndex = i;
                    nearestFront = bounds.min.z;
                }
            }
            if (activeIndex >= 0)
            {
                consumed[activeIndex] = true;
                phase = 0f;
                shownHint = tutorials[activeIndex].hint;
                if (tutorialLabel != null)
                {
                    tutorialLabel.supportRichText = true;
                    tutorialLabel.text = TextFor(shownHint);
                }
            }
        }

        float targetAlpha = activeIndex >= 0 ? 1f : 0f;
        if (tutorialGroup != null)
            tutorialGroup.alpha = Mathf.MoveTowards(tutorialGroup.alpha, targetAlpha,
                Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, fadeSeconds));
        phase += Mathf.Max(0f, deltaTime);
        float strength = tutorialGroup != null ? tutorialGroup.alpha : targetAlpha;
        PulseIcon(strength);
        bool zoneSlow = activeIndex >= 0 && tutorials[activeIndex].slowMotion;
        if (zoneSlow && airborne)
            slowUntilLanding = true;
        if (!airborne)
            slowUntilLanding = false;
        ApplySlowMotion(slowUntilLanding);
    }

    static float EndZ(TutorialPoint point)
    {
        Bounds bounds = point.obstacle.bounds;
        return point.endAfterFrontMeters > 0f
            ? Mathf.Min(bounds.max.z, bounds.min.z + point.endAfterFrontMeters)
            : bounds.max.z;
    }

    static string TextFor(HintKind hint)
    {
        switch (hint)
        {
            case HintKind.Hold: return "长按 ↑ 跳得更远";
            case HintKind.ChangeColor: return "左键变颜色";
            case HintKind.JumpAndChangeColor:
                return "尝试<color=#FFA500>跳起来</color>\n并在空中<color=#FF69B4>切换</color>颜色";
            default: return "轻按 ↑ 跳跃";
        }
    }

    void PulseIcon(float strength)
    {
        float pulse = 1f + strength * 0.07f * (0.5f + 0.5f * Mathf.Sin(phase * 6f));
        if (shownHint == HintKind.JumpAndChangeColor)
        {
            if (jumpIcon != null) jumpIcon.localScale = Vector3.one * pulse;
            if (colorIcon != null) colorIcon.localScale = Vector3.one * pulse;
            return;
        }
        bool colorHint = shownHint == HintKind.ChangeColor;
        RectTransform active = colorHint ? colorIcon : jumpIcon;
        RectTransform idle = colorHint ? jumpIcon : colorIcon;
        if (idle != null) idle.localScale = Vector3.one;
        if (active == null) return;
        active.localScale = Vector3.one * pulse;
    }

    void ApplySlowMotion(bool slow)
    {
        if (!Application.isPlaying) return;
        if (slow)
        {
            Time.timeScale = colorHintTimeScale;
            slowApplied = true;
            return;
        }
        if (!slowApplied) return;
        Time.timeScale = 1f;
        slowApplied = false;
    }
}
