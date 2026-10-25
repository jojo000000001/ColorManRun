using UnityEditor;
using UnityEngine;

/// <summary>
/// Compatibility entry point for the previous greybox runway tool.
/// Runway layout is now authored by placing TrackBeam prefabs manually and
/// assigning a TrackPartDefinition asset to TrackPartBinding.definition.
/// </summary>
public static class TrackLayoutTool
{
    [MenuItem("Tools/生成测试跑道")]
    private static void OpenDefinitionTool()
    {
        TrackPartDefinitionTool window = EditorWindow.GetWindow<TrackPartDefinitionTool>("跑道 SO");
        window.Show();
    }
}
