using UnityEngine;

// 在玩家 Update 的移动、支撑颜色和掉落判定之后收集终点候选。
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class FinishTrigger : MonoBehaviour
{
    [Tooltip("必须绑定本关最后一块安全平台的实际碰撞体；未绑定时不会通关。")]
    public BoxCollider finalPlatform;
    public PlayerController player;

    BoxCollider zone;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void Awake()
    {
        zone = GetComponent<BoxCollider>();
        zone.isTrigger = true;
        if (player == null)
            player = FindObjectOfType<PlayerController>();
        if (finalPlatform == null)
            Debug.LogError("FinishTrigger 未绑定最后一块安全平台，已禁用通关判定。", this);
    }

    void Update()
    {
        // 不依赖 Rigidbody/触发回调时序，落在终点区域内也能正确判定。
        if (CanFinish(player) && GameManager.Instance != null)
            GameManager.Instance.RequestLevelClear(this, player);
    }

    public bool CanFinish(PlayerController candidate)
    {
        if (!isActiveAndEnabled || finalPlatform == null || !finalPlatform.enabled ||
            !finalPlatform.gameObject.activeInHierarchy || candidate == null ||
            !candidate.isActiveAndEnabled || !candidate.CompareTag("Player"))
            return false;

        if (!candidate.IsGrounded)
            return CanFinishFromAbove(candidate);

        TrackPart part = finalPlatform.GetComponentInParent<TrackPart>();
        if (part == null || candidate.Support != part || part.colorIndex != candidate.ColorIndex)
            return false;

        // 脚底位于平台上表面，且整个胶囊水平截面都在平台内；侧边/下方不算。
        Vector3 feet = candidate.FeetPosition;
        Vector3 localFeet = finalPlatform.transform.InverseTransformPoint(feet) - finalPlatform.center;
        Vector3 half = finalPlatform.size * 0.5f;
        CharacterController body = candidate.GetComponent<CharacterController>();
        float radius = body.radius * Mathf.Max(Mathf.Abs(candidate.transform.lossyScale.x), Mathf.Abs(candidate.transform.lossyScale.z));
        Vector3 scale = finalPlatform.transform.lossyScale;
        if (Mathf.Abs(localFeet.x) + radius / Mathf.Abs(scale.x) > half.x ||
            Mathf.Abs(localFeet.z) + radius / Mathf.Abs(scale.z) > half.z ||
            Mathf.Abs(localFeet.y - half.y) * Mathf.Abs(scale.y) > 0.12f)
            return false;

        if (zone == null)
            zone = GetComponent<BoxCollider>();
        if (!zone.enabled)
            return false;
        Vector3 local = zone.transform.InverseTransformPoint(feet + Vector3.up * 0.2f) - zone.center;
        Vector3 extent = zone.size * 0.5f;
        return Mathf.Abs(local.x) <= extent.x && Mathf.Abs(local.y) <= extent.y && Mathf.Abs(local.z) <= extent.z;
    }

    // 空中跳过终点：水平位置仍须在终点区域内，向下射线打到最后一块平台即可，不要求颜色一致。
    bool CanFinishFromAbove(PlayerController candidate)
    {
        if (zone == null)
            zone = GetComponent<BoxCollider>();
        if (!zone.enabled)
            return false;

        Vector3 feet = candidate.FeetPosition;
        Vector3 local = zone.transform.InverseTransformPoint(feet + Vector3.up * 0.2f) - zone.center;
        Vector3 extent = zone.size * 0.5f;
        if (Mathf.Abs(local.x) > extent.x || Mathf.Abs(local.z) > extent.z)
            return false;

        CharacterController body = candidate.GetComponent<CharacterController>();
        float radius = body.radius * Mathf.Max(Mathf.Abs(candidate.transform.lossyScale.x), Mathf.Abs(candidate.transform.lossyScale.z));
        Vector3 scale = finalPlatform.transform.lossyScale;
        Vector3 localFeet = finalPlatform.transform.InverseTransformPoint(feet) - finalPlatform.center;
        Vector3 half = finalPlatform.size * 0.5f;
        if (Mathf.Abs(localFeet.x) + radius / Mathf.Abs(scale.x) > half.x ||
            Mathf.Abs(localFeet.z) + radius / Mathf.Abs(scale.z) > half.z)
            return false;

        float belowTop = (half.y - localFeet.y) * Mathf.Abs(scale.y);
        if (belowTop > 0.05f)
            return false;

        Vector3 origin = feet + Vector3.up * 0.2f;
        RaycastHit hit;
        if (!Physics.Raycast(origin, Vector3.down, out hit, 200f, candidate.groundMask, QueryTriggerInteraction.Ignore))
            return false;
        return hit.collider == finalPlatform;
    }
}
