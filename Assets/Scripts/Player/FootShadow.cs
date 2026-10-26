using UnityEngine;

/// <summary>
/// 贴在脚底的软阴影。从脚底向下射线落到 groundMask 上，跳得越高越小、越淡。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class FootShadow : MonoBehaviour
{
    [SerializeField] Sprite shadowSprite;
    [SerializeField] PlayerController player;
    [SerializeField] SpriteRenderer blobRenderer;
    [SerializeField, Min(0.05f)] float groundedSize = 3f;
    [SerializeField, Min(0.05f)] float airborneSize = 1f;
    [SerializeField, Min(0.1f)] float fadeHeight = 4f;
    [SerializeField, Range(0f, 1f)] float groundedAlpha = 0.55f;
    [SerializeField, Min(0f)] float surfaceOffset = 0.04f;
    [SerializeField, Min(0.5f)] float maxDrop = 12f;

    const float RayLift = 0.2f;

    Transform blob;

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();

        // The shared player prefab owns the shadow, so scene reloads cannot leave it behind.
        // Keep a fallback for existing/test players without the prefab child.
        if (blobRenderer == null)
        {
            var go = new GameObject("FootShadow");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            blobRenderer = go.AddComponent<SpriteRenderer>();
        }
        blob = blobRenderer.transform;
        blobRenderer.sprite = shadowSprite;
        blobRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        blobRenderer.receiveShadows = false;
        blobRenderer.enabled = false;
    }

    void OnDisable()
    {
        if (blobRenderer != null)
            blobRenderer.enabled = false;
    }

    void LateUpdate()
    {
        if (player == null || shadowSprite == null || blobRenderer == null)
            return;

        int mask = player.groundMask.value;
        if (mask == 0)
            mask = LayerMask.GetMask("Ground");

        Vector3 origin = player.FeetPosition + Vector3.up * RayLift;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDrop, mask, QueryTriggerInteraction.Ignore))
        {
            blobRenderer.enabled = false;
            return;
        }

        float height = Mathf.Max(0f, hit.distance - RayLift);
        float fade = Mathf.Clamp01(height / fadeHeight);
        if (fade >= 1f)
        {
            blobRenderer.enabled = false;
            return;
        }

        Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        Vector3 heading = Vector3.ProjectOnPlane(player.transform.forward, normal);
        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.ProjectOnPlane(Vector3.forward, normal);

        float size = Mathf.Lerp(groundedSize, airborneSize, fade);
        blob.SetPositionAndRotation(hit.point + normal * surfaceOffset, Quaternion.LookRotation(normal, heading));
        blob.localScale = new Vector3(size, size, 1f);

        Color tint = blobRenderer.color;
        tint.r = 1f;
        tint.g = 1f;
        tint.b = 1f;
        tint.a = groundedAlpha * (1f - fade);
        blobRenderer.color = tint;
        blobRenderer.enabled = true;
    }
}
