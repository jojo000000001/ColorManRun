using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only greybox generator for a test runway.
/// It creates scene objects only; it does not implement gameplay logic.
/// </summary>
public sealed class TrackLayoutTool : EditorWindow
{
    private const float TrackWidth = 3f;
    private const float TrackHeight = 0.4f;
    private const float TrackTopY = 0f;

    private int segmentCount = 6;
    private float segmentLength = 8f;
    private float gapWidth = 2f;
    private int colorCount = 2;

    [MenuItem("Tools/生成测试跑道")]
    private static void OpenWindow()
    {
        GetWindow<TrackLayoutTool>("测试跑道");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("测试跑道参数", EditorStyles.boldLabel);
        segmentCount = EditorGUILayout.IntField("段数", segmentCount);
        segmentLength = EditorGUILayout.FloatField("每段长度", segmentLength);
        gapWidth = EditorGUILayout.FloatField("缺口宽度", gapWidth);
        colorCount = EditorGUILayout.IntSlider("颜色数（橙 / 粉）", colorCount, 1, TrackPart.ColorCount);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("固定尺寸", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("宽度", TrackWidth.ToString("0.0") + " m");
        EditorGUILayout.LabelField("厚度", TrackHeight.ToString("0.0") + " m");
        EditorGUILayout.LabelField("顶面高度", TrackTopY.ToString("0.0") + " m");

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "生成内容会放在 Level_Test 下。若已存在同名根节点，点击生成时会询问是否删除并重建。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!IsValidInput()))
        {
            if (GUILayout.Button("生成测试跑道", GUILayout.Height(30f)))
            {
                GenerateTestLayout();
            }
        }

        if (!IsValidInput())
        {
            EditorGUILayout.HelpBox("段数和颜色数必须为正整数；长度和缺口宽度不能为负数。", MessageType.Warning);
        }
    }

    private bool IsValidInput()
    {
        return segmentCount > 0 && segmentLength > 0f && gapWidth >= 0f &&
            colorCount > 0 && colorCount <= TrackPart.ColorCount;
    }

    private void GenerateTestLayout()
    {
        GameObject existingRoot = GameObject.Find("Level_Test");
        if (existingRoot != null)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "重建测试跑道",
                "场景中已存在 Level_Test。删除该根节点及其全部子物体后重新生成吗？",
                "删除并生成",
                "取消");

            if (!confirmed)
            {
                return;
            }

            Undo.DestroyObjectImmediate(existingRoot);
        }

        GameObject root = new GameObject("Level_Test");
        Undo.RegisterCreatedObjectUndo(root, "生成测试跑道");

        GameObject startPoint = new GameObject("StartPoint");
        startPoint.transform.SetParent(root.transform, false);
        startPoint.transform.localPosition = new Vector3(0f, TrackTopY, 0f);
        Undo.RegisterCreatedObjectUndo(startPoint, "生成 StartPoint");

        GameObject finishTrigger = new GameObject("FinishTrigger");
        finishTrigger.transform.SetParent(root.transform, false);
        Undo.RegisterCreatedObjectUndo(finishTrigger, "生成 FinishTrigger 占位");

        GameObject parts = new GameObject("Parts");
        parts.transform.SetParent(root.transform, false);
        Undo.RegisterCreatedObjectUndo(parts, "生成 Parts 容器");

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            Debug.LogWarning("[生成测试跑道] Layer Ground 不存在，平台暂时保留在 Default Layer。请先创建 Ground Layer。");
        }

        float currentStartZ = 0f;
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = "TrackPart_" + (i + 1).ToString("00");
            segment.transform.SetParent(parts.transform, false);
            segment.transform.localPosition = new Vector3(
                0f,
                TrackTopY - TrackHeight * 0.5f,
                currentStartZ + segmentLength * 0.5f);
            segment.transform.localScale = new Vector3(TrackWidth, TrackHeight, segmentLength);

            if (groundLayer >= 0)
            {
                segment.layer = groundLayer;
            }

            TrackPart trackPart = segment.AddComponent<TrackPart>();
            trackPart.SetColor(i % colorCount);

            Undo.RegisterCreatedObjectUndo(segment, "生成跑道段");
            currentStartZ += segmentLength + gapWidth;
        }

        finishTrigger.transform.localPosition = new Vector3(
            0f,
            TrackTopY,
            currentStartZ - gapWidth + 0.5f);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[生成测试跑道] 已生成 Level_Test：" + segmentCount + " 段，颜色索引范围 0-" + (colorCount - 1) + "。请手动保存场景。", root);
    }
}
