using UnityEngine;

/// <summary>
/// 在 Player 移动完成后平滑跟随，包括跳跃和不同高度跑道间的下落。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    // 轨道沿世界 +Z 延伸；摄像机保持在 Player 的右后上方。
    public Vector3 offset = new Vector3(26f, 10.5f, -14f);
    // 向前预留视野，让 Player 偏左、前方跑道向画面右上方延伸。
    public Vector3 lookOffset = new Vector3(0f, 1.1f, 6f);
    [Min(0f)] public float smoothTime = 0.12f;
    public bool followVertical = true;

    Vector3 velocity;
    float referenceY;

    void Start()
    {
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;
        referenceY = target.position.y;
        velocity = Vector3.zero;
        transform.position = FollowAnchor() + offset;
        AimAtTarget();
    }

    void LateUpdate()
    {
        if (target == null)
            return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return;

        Vector3 desired = FollowAnchor() + offset;
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime,
                Mathf.Infinity, Time.deltaTime)
            : desired;
        AimAtTarget();
    }

    Vector3 FollowAnchor()
    {
        Vector3 anchor = target.position;
        if (!followVertical)
            anchor.y = referenceY;
        return anchor;
    }

    void AimAtTarget()
    {
        // 位置保持平行跟随，瞄准点随 Player（包括跳跃高度）加构图偏移。
        Vector3 direction = target.position + lookOffset - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}
