using UnityEngine;

/// <summary>
/// Binds a runway prefab instance to a TrackPartDefinition.
/// The prefab supplies the mesh/collider shape; the SO supplies length, thickness and color.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TrackPartBinding : MonoBehaviour
{
    // The imported runway mesh is 4.8 x 2 x 56 units, with its pivot at the top.
    // Keep width fixed at 3m; thickness extends down without moving the top.
    const float NativeLength = 56f;
    const float FixedWidthScale = 0.625f;
    const float NativeThickness = 2f;

    [Tooltip("该实例使用的跑道 SO；SO 控制长度、厚度和颜色，宽度固定为 3 米。")]
    public TrackPartDefinition definition;

    bool applying;

    void Awake()
    {
        if (Application.isPlaying)
            ApplyDefinition();
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRefresh();
            return;
        }
#endif
        ApplyDefinition();
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        // OnValidate can run while Unity is loading assets. Defer scene and
        // renderer operations until the next editor tick on the main thread.
        QueueEditorRefresh();
#endif
    }

#if UNITY_EDITOR
    void QueueEditorRefresh()
    {
        UnityEditor.EditorApplication.update -= RefreshInEditor;
        UnityEditor.EditorApplication.update += RefreshInEditor;
    }

    void RefreshInEditor()
    {
        UnityEditor.EditorApplication.update -= RefreshInEditor;
        if (this != null && !Application.isPlaying)
            ApplyDefinition();
    }

    void OnDisable()
    {
        UnityEditor.EditorApplication.update -= RefreshInEditor;
    }

    public static void RefreshLoadedBindings()
    {
        UnityEditor.EditorApplication.update -= RefreshLoadedBindings;
        foreach (TrackPartBinding binding in Resources.FindObjectsOfTypeAll<TrackPartBinding>())
        {
            if (binding != null)
                binding.ApplyDefinition();
        }
    }
#endif

    public void SetDefinition(TrackPartDefinition value)
    {
        definition = value;
        ApplyDefinition();
    }

    public void ApplyDefinition()
    {
        if (applying || definition == null)
            return;

#if UNITY_EDITOR
        // Never silently write back to prefab assets loaded by the Inspector.
        // Prefab instances (including Prefab Mode) refresh on enable instead.
        if (UnityEditor.EditorUtility.IsPersistent(this))
            return;
#endif

        applying = true;
        try
        {
            float length = Mathf.Max(0.1f, definition.length);
            float thickness = Mathf.Max(0.01f, definition.thickness);
            Vector3 targetScale = new Vector3(
                FixedWidthScale,
                thickness / NativeThickness,
                length / NativeLength);
            bool scaleChanged = transform.localScale != targetScale;
            if (scaleChanged)
                transform.localScale = targetScale;

            TrackPart part = GetComponent<TrackPart>();
            if (part == null)
                part = gameObject.AddComponent<TrackPart>();
            bool colorChanged = part.definition != definition || part.colorIndex != definition.ColorIndex;
            part.definition = definition;
            part.SetColor(definition.ColorIndex);

#if UNITY_EDITOR
            if (!Application.isPlaying && (scaleChanged || colorChanged))
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                UnityEditor.EditorUtility.SetDirty(transform);
                UnityEditor.EditorUtility.SetDirty(part);
                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }
        finally
        {
            applying = false;
        }
    }
}
