using UnityEngine;

/// <summary>
/// 在 Player 移动完成后平滑跟随，默认保留场景中手动调整的初始构图。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("使用场景中摄像机相对玩家的初始位置和世界旋转；跟随时不再自动瞄准。")]
    public bool useInitialPose = true;
    // 轨道沿世界 +Z 延伸；摄像机保持在 Player 的右后上方。
    [Tooltip("仅在关闭 Use Initial Pose 时使用。")]
    public Vector3 offset = new Vector3(26f, 10.5f, -14f);
    // 向前预留视野，让 Player 偏左、前方跑道向画面右上方延伸。
    [Tooltip("仅在关闭 Use Initial Pose 时使用。")]
    public Vector3 lookOffset = new Vector3(0f, 1.1f, 6f);
    [Min(0f)] public float smoothTime = 0.12f;
    public bool followVertical = true;

    Vector3 velocity;
    float referenceY;
    Vector3 initialOffset;
    Quaternion initialRotation;
    bool initialPoseCaptured;

    void Start()
    {
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;
        CaptureInitialPose();
        velocity = Vector3.zero;
        transform.position = FollowAnchor() + FollowOffset();
        ApplyRotation();
    }

    void LateUpdate()
    {
        if (target == null)
            return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return;

        CaptureInitialPose();
        Vector3 desired = FollowAnchor() + FollowOffset();
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime,
                Mathf.Infinity, Time.deltaTime)
            : desired;
        ApplyRotation();
    }

    void CaptureInitialPose()
    {
        if (initialPoseCaptured)
            return;
        // 只记录一次，重置跟随时不能把平滑滞后的当前位置当成新偏移。
        referenceY = target.position.y;
        initialOffset = transform.position - target.position;
        initialRotation = transform.rotation;
        initialPoseCaptured = true;
    }

    Vector3 FollowOffset()
    {
        return useInitialPose ? initialOffset : offset;
    }

    void ApplyRotation()
    {
        if (useInitialPose)
            transform.rotation = initialRotation;
        else
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
