using UnityEngine;

/// <summary>
/// Reusable runway recipe. Geometry remains owned by TrackBeam.prefab;
/// designers choose the segment length, thickness and gameplay color.
/// </summary>
[CreateAssetMenu(fileName = "TrackPart", menuName = "跑酷/跑道长条配置", order = 10)]
public sealed class TrackPartDefinition : ScriptableObject
{
    public enum RunwayColor
    {
        Yellow = 0,
        Pink = 1,
        Black = 2
    }

    [Min(0.1f)] public float length = 14f;
    [Tooltip("跑道厚度（米），从顶面向下延伸，不改变顶面高度。")]
    [Min(0.01f)] public float thickness = 0.4f;
    public RunwayColor color = RunwayColor.Yellow;

    public int ColorIndex
    {
        get { return (int)color; }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        length = Mathf.Max(0.1f, length);
        thickness = Mathf.Max(0.01f, thickness);
        // Coalesce edits and avoid scene/renderer APIs on the loading thread.
        UnityEditor.EditorApplication.update -= TrackPartBinding.RefreshLoadedBindings;
        UnityEditor.EditorApplication.update += TrackPartBinding.RefreshLoadedBindings;
    }
#endif
}
