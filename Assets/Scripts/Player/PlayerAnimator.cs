using UnityEngine;

/// <summary>
/// Translates successful controller actions and logical state into animation only.
/// No input, position, velocity, collision, or material changes belong here.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAnimator : MonoBehaviour
{
    public Animator animator;
    [Min(0f)] public float jumpStartDuration = 0.12f;
    [Min(0.01f)] public float colorAnimationDuration = 0.30f;
    [Min(0.001f)] public float colorFadeOut = 0.04f;

    static readonly int InAirId = Animator.StringToHash("InAir");
    static readonly int HoldId = Animator.StringToHash("Hold");
    static readonly int DeadId = Animator.StringToHash("Dead");
    static readonly int JumpId = Animator.StringToHash("Jump");
    static readonly int ChangeColorId = Animator.StringToHash("ChangeColor");

    PlayerController controller;
    int colorLayer = -1;
    int jumpFrame = -1;
    int colorFrame = -1;
    float jumpElapsed;
    float colorElapsed;
    bool changingColor;
    bool startingJump;
    bool dead;
    bool configured;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerAnimator needs an Animator with Player.controller.", this);
            return;
        }

        configured = HasParameter(InAirId, AnimatorControllerParameterType.Bool)
            && HasParameter(HoldId, AnimatorControllerParameterType.Bool)
            && HasParameter(DeadId, AnimatorControllerParameterType.Bool)
            && HasParameter(JumpId, AnimatorControllerParameterType.Trigger)
            && HasParameter(ChangeColorId, AnimatorControllerParameterType.Trigger);
        colorLayer = animator.GetLayerIndex("ColorTurn");
        configured &= colorLayer >= 0;
        if (!configured)
        {
            Debug.LogWarning("PlayerAnimator parameters or ColorTurn layer are missing.", this);
            return;
        }

        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.SetLayerWeight(colorLayer, 0f);
    }

    void OnEnable()
    {
        if (controller == null)
            controller = GetComponent<PlayerController>();
        controller.JumpStarted += PlayJumpStart;
        controller.ColorChanged += PlayChangeColor;
    }

    void OnDisable()
    {
        if (controller == null)
            return;
        controller.JumpStarted -= PlayJumpStart;
        controller.ColorChanged -= PlayChangeColor;
        // 场景重载销毁时 Animator 可能先被停用；此时不能再重置参数。
        if (configured && animator != null && animator.isActiveAndEnabled && animator.isInitialized)
        {
            animator.ResetTrigger(JumpId);
            animator.ResetTrigger(ChangeColorId);
        }
    }

    void Update()
    {
        if (!configured)
            return;

        GameState state = GameManager.Instance != null
            ? GameManager.Instance.State : GameState.Ready;
        if (state == GameState.Dead)
        {
            PlayDead();
            return;
        }

        // The manager pauses logically without changing Time.timeScale.
        animator.speed = state == GameState.Playing ? 1f : 0f;
        if (state != GameState.Playing)
            return;

        dead = false;
        animator.SetBool(DeadId, false);
        if (changingColor)
        {
            if (colorFrame != Time.frameCount)
                colorElapsed += Time.deltaTime;
            float weight = 1f - Mathf.Clamp01((colorElapsed - colorAnimationDuration) / colorFadeOut);
            animator.SetLayerWeight(colorLayer, weight);
            if (weight <= 0f)
                changingColor = false;
        }
        if (controller.IsGrounded)
        {
            PlayRun();
            return;
        }

        animator.SetBool(InAirId, true);
        if (startingJump)
        {
            // Do not consume the whole visual start window on the launch frame.
            if (jumpFrame != Time.frameCount)
                jumpElapsed += Time.deltaTime;
            if (jumpElapsed >= jumpStartDuration)
                PlayJumpHold();
        }
        else
        {
            // Walking off an edge is falling, not another jump-start action.
            PlayJumpHold();
        }
    }

    public void PlayRun()
    {
        if (!configured)
            return;
        startingJump = false;
        jumpElapsed = 0f;
        dead = false;
        animator.SetBool(DeadId, false);
        // Both air states have zero-duration, no-exit-time transitions to Run.
        animator.SetBool(InAirId, false);
        animator.SetBool(HoldId, false);
        animator.ResetTrigger(JumpId);
    }

    public void PlayJumpStart()
    {
        if (!configured || dead || jumpFrame == Time.frameCount)
            return;
        jumpFrame = Time.frameCount;
        jumpElapsed = 0f;
        startingJump = true;
        animator.SetBool(InAirId, true);
        animator.SetBool(HoldId, false);
        animator.ResetTrigger(JumpId);
        animator.SetTrigger(JumpId);
    }

    public void PlayJumpHold()
    {
        if (!configured || dead)
            return;
        startingJump = false;
        animator.SetBool(InAirId, true);
        animator.SetBool(HoldId, true);
    }

    public void PlayChangeColor()
    {
        if (!configured || dead || colorFrame == Time.frameCount)
            return;
        colorFrame = Time.frameCount;
        colorElapsed = 0f;
        changingColor = true;
        // The FBX change_color clip contains a complete 360-degree turn.
        // Use an unmasked override layer so Bip001 rotation is not discarded;
        // Player's movement root and collider never rotate with the visual pose.
        animator.SetLayerWeight(colorLayer, 1f);
        animator.ResetTrigger(ChangeColorId);
        animator.SetTrigger(ChangeColorId);
    }

    public void PlayDead()
    {
        if (!configured || dead)
            return;
        dead = true;
        startingJump = false;
        changingColor = false;
        animator.ResetTrigger(JumpId);
        animator.ResetTrigger(ChangeColorId);
        animator.SetBool(DeadId, true);
        animator.SetLayerWeight(colorLayer, 0f);
        // No death clip was supplied: Dead holds a static jump_holding pose.
        animator.speed = 1f;
    }

    bool HasParameter(int id, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == id && parameter.type == type)
                return true;
        return false;
    }
}
