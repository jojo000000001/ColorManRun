using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places runway prefab instances from TrackPartDefinition assets.
/// The prefab remains the geometry source; the SO exposes length/thickness/color.
/// </summary>
public sealed class TrackPartDefinitionTool : EditorWindow
{
    const string SoFolder = "Assets/SO";
    const string YellowPath = SoFolder + "/Track_Yellow.asset";
    const string PinkPath = SoFolder + "/Track_Pink.asset";
    const string BlackPath = SoFolder + "/Track_Black.asset";
    const float TrackTopY = 1f;

    [SerializeField] TrackPartDefinition definition;
    [SerializeField] int segmentCount = 6;
    [SerializeField] float gapWidth = 2f;

    [MenuItem("Tools/跑道 SO/创建默认跑道 SO")]
    static void CreateDefaultAssets()
    {
        EnsureFolder();
        CreateOrUpdate(YellowPath, "Track_Yellow", 14f, TrackPartDefinition.RunwayColor.Yellow);
        CreateOrUpdate(PinkPath, "Track_Pink", 14f, TrackPartDefinition.RunwayColor.Pink);
        CreateOrUpdate(BlackPath, "Track_Black", 14f, TrackPartDefinition.RunwayColor.Black);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[跑道 SO] 默认 SO 已就绪；已有配置不会被覆盖。");
    }

    [MenuItem("Tools/跑道 SO/打开搭建窗口")]
    static void OpenWindow()
    {
        GetWindow<TrackPartDefinitionTool>("跑道 SO");
    }

    void OnEnable()
    {
        if (definition == null)
            definition = AssetDatabase.LoadAssetAtPath<TrackPartDefinition>(YellowPath);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("推荐直接拖放 TrackBeam 预制体，在 TrackPartBinding 的 Definition 中指定 SO。下面仅用于可选的测试批量摆放，不依赖 LevelRoot。", MessageType.Info);
        definition = (TrackPartDefinition)EditorGUILayout.ObjectField(
            "长条 SO", definition, typeof(TrackPartDefinition), false);
        segmentCount = EditorGUILayout.IntField("段数", segmentCount);
        gapWidth = EditorGUILayout.FloatField("缺口宽度", gapWidth);

        if (definition != null)
        {
            EditorGUILayout.LabelField("长度", definition.length.ToString("0.##") + " m");
            EditorGUILayout.LabelField("厚度", definition.thickness.ToString("0.##") + " m");
            EditorGUILayout.LabelField("颜色", definition.color.ToString());
        }

        using (new EditorGUI.DisabledScope(definition == null || segmentCount <= 0 || definition.length <= 0f || definition.thickness <= 0f || gapWidth < 0f))
        {
            if (GUILayout.Button("用 SO 生成测试跑道", GUILayout.Height(28f)))
                Generate();
        }

        if (GUILayout.Button("创建缺失的默认黄色、粉色和黑色 SO"))
            CreateDefaultAssets();
    }

    void Generate()
    {
        EnsureFolder();
        string prefabPath;
        switch (definition.color)
        {
            case TrackPartDefinition.RunwayColor.Pink:
                prefabPath = "Assets/Prefabs/TrackBeam_Pink.prefab";
                break;
            case TrackPartDefinition.RunwayColor.Black:
                prefabPath = "Assets/Prefabs/TrackBeam_Black.prefab";
                break;
            default:
                prefabPath = "Assets/Prefabs/TrackBeam.prefab";
                break;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[跑道 SO] 找不到形状预制体：" + prefabPath);
            return;
        }

        GameObject old = GameObject.Find("Level_Test_SO");
        if (old != null && !EditorUtility.DisplayDialog("重建测试跑道", "删除 Level_Test_SO 后重新生成？", "删除并生成", "取消"))
            return;
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        GameObject root = new GameObject("Level_Test_SO");
        Undo.RegisterCreatedObjectUndo(root, "用 SO 生成测试跑道");
        GameObject parts = new GameObject("Parts");
        parts.transform.SetParent(root.transform, false);
        Undo.RegisterCreatedObjectUndo(parts, "创建跑道 Parts");

        int groundLayer = LayerMask.NameToLayer("Ground");
        float z = 0f;
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segment = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parts.transform);
            segment.name = string.Format("{0:00}_{1}", i + 1, definition.name);
            // The binding component owns fixed width and SO length/thickness.
            segment.transform.localPosition = new Vector3(0f, TrackTopY, z);
            segment.transform.localRotation = Quaternion.identity;
            if (groundLayer >= 0)
                SetLayerRecursively(segment, groundLayer);

            TrackPartBinding binding = segment.GetComponent<TrackPartBinding>();
            if (binding == null)
                binding = Undo.AddComponent<TrackPartBinding>(segment);
            binding.SetDefinition(definition);
            Undo.RegisterCreatedObjectUndo(segment, "创建 SO 跑道段");
            z += definition.length + gapWidth;
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(string.Format("[跑道 SO] 已生成 {0} 段，长度 {1:0.##} m，颜色 {2}。", segmentCount, definition.length, definition.color), root);
    }

    static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(SoFolder))
            AssetDatabase.CreateFolder("Assets", "SO");
    }

    static void CreateOrUpdate(string path, string assetName, float length, TrackPartDefinition.RunwayColor color)
    {
        TrackPartDefinition asset = AssetDatabase.LoadAssetAtPath<TrackPartDefinition>(path);
        if (asset != null)
            return;

        asset = CreateInstance<TrackPartDefinition>();
        asset.name = assetName;
        asset.length = length;
        asset.color = color;
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
    }
}
