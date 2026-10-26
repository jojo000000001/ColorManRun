using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 奔跑时留在跑道上的彩色尘土。粒子在世界空间里慢慢散掉，角色跑开后不会跟着飞。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class RunDust : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] ParticleSystem dust;
    [SerializeField] Texture2D puff;
    [SerializeField] Material dustMaterial;
    [SerializeField, Min(0f)] float emissionRate = 120f;
    [SerializeField, Min(0)] int landingBurst = 30;
    [SerializeField, Range(0f, 1f)] float dustAlpha = 0.85f;

    bool wasGrounded = true;
    bool spawned;

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();
        EnsureSystem();
        Configure();
        if (spawned && dust != null)
            dust.gameObject.SetActive(true);
    }

    void Start()
    {
        wasGrounded = player != null && player.IsGrounded;
        ApplyColor();
    }

    void OnEnable()
    {
        if (player != null)
            player.ColorChanged += ApplyColor;
    }

    void OnDisable()
    {
        if (player != null)
            player.ColorChanged -= ApplyColor;
        if (dust != null)
        {
            var emission = dust.emission;
            emission.enabled = false;
            dust.Pause();
        }
    }

    void LateUpdate()
    {
        if (player == null || dust == null)
            return;

        bool playing = GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;
        if (!playing)
        {
            var stopped = dust.emission;
            stopped.enabled = false;
            if (dust.isPlaying && !dust.isPaused)
                dust.Pause();
            return;
        }

        if (!dust.isPlaying || dust.isPaused)
            dust.Play();

        bool grounded = player.IsGrounded;
        var emission = dust.emission;
        emission.enabled = grounded || player.IsLongJumpHeld;
        if (grounded && !wasGrounded && landingBurst > 0)
            dust.Emit(landingBurst);
        wasGrounded = grounded;
    }

    void EnsureSystem()
    {
        spawned = false;
        if (dust != null)
            return;

        Transform existing = transform.Find("RunDust");
        if (existing != null)
            dust = existing.GetComponent<ParticleSystem>();
        if (dust != null)
            return;

        GameObject go = existing != null ? existing.gameObject : new GameObject("RunDust");
        if (existing == null)
        {
            go.layer = 0;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.35f, -0.7f);
        }
        go.SetActive(false);
        dust = go.AddComponent<ParticleSystem>();
        spawned = true;
    }

    public void Configure()
    {
        if (dust == null || puff == null)
            return;

        var main = dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.useUnscaledTime = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = 0.35f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.maxParticles = 160;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;

        var emission = dust.emission;
        emission.enabled = false;
        emission.rateOverTime = emissionRate;

        var shape = dust.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.75f;
        shape.randomDirectionAmount = 0.65f;

        var velocity = dust.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-1.3f, 1.3f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 1.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(-2.8f, -0.6f);

        var inherit = dust.inheritVelocity;
        inherit.enabled = false;

        var noise = dust.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.35f, 1.15f),
            new Keyframe(1f, 1.8f)));

        var color = dust.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.75f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        var collision = dust.collision;
        collision.enabled = false;

        var renderer = dust.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enableGPUInstancing = false;
        renderer.maxParticleSize = 5f;
        if (dustMaterial != null)
        {
            if (puff != null)
                dustMaterial.mainTexture = puff;
            renderer.sharedMaterial = dustMaterial;
        }
        ApplyColor();
    }

    void ApplyColor()
    {
        if (player == null || dust == null)
            return;
        Color tint = TrackPart.ColorOf(player.ColorIndex);
        tint.a = dustAlpha;
        var main = dust.main;
        main.startColor = tint;
    }
}
