using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 落地时砸出一小撮方块。下落越多方块越多，超过上限后不再增加。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class LandingBlocks : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] ParticleSystem blocks;
    [SerializeField] Material blockMaterial;
    [SerializeField, Min(0.05f)] float minDrop = 0.45f;
    [SerializeField, Min(0.1f)] float dropForMax = 8f;
    [SerializeField, Min(0)] int minBlocks = 3;
    [SerializeField, Min(1)] int maxBlocks = 14;

    bool wasGrounded = true;
    bool tracking;
    float peakY;
    bool spawned;

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();
        EnsureSystem();
        Configure();
        if (spawned && blocks != null)
            blocks.gameObject.SetActive(true);
    }

    void Start()
    {
        wasGrounded = player == null || player.IsGrounded;
        if (!wasGrounded && player != null)
        {
            tracking = true;
            peakY = player.FeetPosition.y;
        }
    }

    void LateUpdate()
    {
        if (player == null || blocks == null)
            return;

        bool playing = GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;
        if (!playing)
        {
            var stopped = blocks.emission;
            stopped.enabled = false;
            if (blocks.isPlaying && !blocks.isPaused)
                blocks.Pause();
            return;
        }

        if (!blocks.isPlaying || blocks.isPaused)
            blocks.Play();

        float feetY = player.FeetPosition.y;
        if (!player.IsGrounded)
        {
            if (!tracking)
            {
                tracking = true;
                peakY = feetY;
            }
            else if (feetY > peakY)
                peakY = feetY;
            wasGrounded = false;
            return;
        }

        // Wait until PlayerController has finished its support probe and settled the
        // CharacterController.  This is the actual landing state, unlike the first
        // OnControllerColliderHit callback which can arrive while the capsule is
        // still sweeping into the front/side of a platform.
        if (!wasGrounded && tracking)
            PlayImpact(FindLandingPoint());
        tracking = false;
        wasGrounded = true;
    }

    Vector3 FindLandingPoint()
    {
        if (player == null)
            return transform.position;

        // Use the settled feet and a short downward probe.  The collision callback
        // point is often the leading edge of a platform, so it makes the burst look
        // offset from the player's actual contact point.
        Vector3 feet = player.FeetPosition;
        int mask = player.groundMask.value;
        if (mask == 0)
            mask = LayerMask.GetMask("Ground");

        RaycastHit hit;
        if (Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out hit, 0.8f,
            mask, QueryTriggerInteraction.Ignore) && hit.normal.y >= 0.55f)
            return hit.point;

        return feet;
    }

    void PlayImpact(Vector3 point)
    {
        int count = BlocksForDrop(peakY - point.y);
        if (count <= 0)
            return;
        // Clear the previous burst so every landing starts at the collision frame,
        // rather than mixing with blocks left from an earlier jump.
        blocks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        blocks.Play();
        EmitUpward(count, point);
    }

    int BlocksForDrop(float drop)
    {
        if (drop < minDrop)
            return 0;
        float span = Mathf.Max(0.01f, dropForMax - minDrop);
        float t = Mathf.Clamp01((drop - minDrop) / span);
        int count = Mathf.RoundToInt(Mathf.Lerp(minBlocks, maxBlocks, t));
        if (count < minBlocks)
            count = minBlocks;
        if (count > maxBlocks)
            count = maxBlocks;
        return count;
    }

    void EnsureSystem()
    {
        spawned = false;
        if (blocks != null)
            return;

        Transform existing = transform.Find("LandingBlocks");
        if (existing != null)
            blocks = existing.GetComponent<ParticleSystem>();
        if (blocks != null)
            return;

        var go = new GameObject("LandingBlocks");
        go.layer = 0;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.SetActive(false);
        blocks = go.AddComponent<ParticleSystem>();
        spawned = true;
    }

    public void Configure()
    {
        if (blocks == null)
            return;

        blocks.transform.localRotation = Quaternion.identity;

        var main = blocks.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.useUnscaledTime = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.15f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.05f;
        main.maxParticles = maxBlocks * 2;
        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;

        var emission = blocks.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = blocks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        shape.randomDirectionAmount = 0f;

        var inherit = blocks.inheritVelocity;
        inherit.enabled = false;

        var spin = blocks.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
        spin.y = new ParticleSystem.MinMaxCurve(-3f, 3f);
        spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

        var collision = blocks.collision;
        collision.enabled = false;

        var renderer = blocks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = BuiltinCube();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enableGPUInstancing = false;
        renderer.maxParticleSize = 5f;
        if (blockMaterial != null)
            renderer.sharedMaterial = blockMaterial;
        ApplyColor();
    }

    void EmitUpward(int count, Vector3 point)
    {
        Color tint = TrackPart.ColorOf(player.ColorIndex);
        tint.a = 1f;
        Vector3 origin = point + Vector3.up * 0.06f;
        for (int i = 0; i < count; i++)
        {
            var emit = new ParticleSystem.EmitParams();
            emit.applyShapeToPosition = false;
            emit.position = origin + new Vector3(
                Random.Range(-0.15f, 0.15f),
                Random.Range(0f, 0.05f),
                Random.Range(-0.15f, 0.15f));
            emit.velocity = new Vector3(
                Random.Range(-1.4f, 1.4f),
                Random.Range(8f, 13f),
                Random.Range(-1.4f, 1.4f));
            emit.startLifetime = Random.Range(0.75f, 1.15f);
            emit.startSize = Random.Range(0.5f, 0.85f);
            emit.startColor = tint;
            emit.rotation3D = new Vector3(
                Random.Range(0f, Mathf.PI * 2f),
                Random.Range(0f, Mathf.PI * 2f),
                Random.Range(0f, Mathf.PI * 2f));
            emit.angularVelocity3D = new Vector3(
                Random.Range(-4f, 4f),
                Random.Range(-4f, 4f),
                Random.Range(-4f, 4f));
            blocks.Emit(emit, 1);
        }
    }

    void ApplyColor()
    {
        if (player == null || blocks == null)
            return;
        Color tint = TrackPart.ColorOf(player.ColorIndex);
        tint.a = 1f;
        var main = blocks.main;
        main.startColor = tint;
    }

    static Mesh cubeMesh;

    static Mesh BuiltinCube()
    {
        if (cubeMesh != null)
            return cubeMesh;
        cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        return cubeMesh;
    }
}
