using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackPartBinding)), CanEditMultipleObjects]
public sealed class TrackPartBindingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "SO 控制长度、厚度和颜色，宽度固定 3 米；厚度从顶面向下延伸。预制体保留形状和碰撞体。位置、旋转由你手动摆放，不需要 LevelRoot。共享同一 SO 的长条会一起更新；单独调整请先复制 SO。",
            MessageType.Info);

        foreach (Object item in targets)
        {
            TrackPartBinding binding = (TrackPartBinding)item;
            if (binding.definition == null)
                EditorGUILayout.HelpBox(binding.name + " 尚未绑定 SO，保持现有形状/颜色。", MessageType.Warning);

            Transform parent = binding.transform.parent;
            if (parent != null && parent.lossyScale != Vector3.one)
                EditorGUILayout.HelpBox(binding.name + " 的父节点有缩放。请将父级缩放保持 (1,1,1)，否则实际宽度、厚度和长度会跟着变化。", MessageType.Warning);
        }
    }
}

[CustomEditor(typeof(TrackPart)), CanEditMultipleObjects]
public sealed class TrackPartEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool bound = false;
        foreach (Object item in targets)
        {
            TrackPartBinding binding = ((TrackPart)item).GetComponent<TrackPartBinding>();
            bound |= binding != null && binding.definition != null;
        }

        using (new EditorGUI.DisabledScope(bound))
            DrawDefaultInspector();

        if (bound)
            EditorGUILayout.HelpBox("此长条的逻辑颜色由绑定的 SO 提供，请在 SO 中修改 Color。", MessageType.Info);
    }
}

[InitializeOnLoad]
public static class TrackPartBindingUndoRefresh
{
    static TrackPartBindingUndoRefresh()
    {
        Undo.undoRedoPerformed += Refresh;
    }

    static void Refresh()
    {
        if (!Application.isPlaying)
            TrackPartBinding.RefreshLoadedBindings();
    }
}
