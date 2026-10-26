using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 会把 Game 视图的缩放（ZoomableArea.m_Scale）保存在窗口布局里；
/// 进入 Play 时可能恢复为 1.5x（常见于 150% 系统缩放），与场景摄像机 FOV 无关。
/// 仅在进入 Play 后把 Game 视图缩放拉回 1x，不改动任何运行时逻辑。
/// </summary>
[InitializeOnLoad]
static class GameViewPlayScaleFix
{
    const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
    const int FramesToEnforce = 5;

    static int framesLeft;

    static GameViewPlayScaleFix()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
            return;

        framesLeft = FramesToEnforce;
        EditorApplication.update -= EnforceScaleOnUpdate;
        EditorApplication.update += EnforceScaleOnUpdate;
        EditorApplication.delayCall += () => SetAllGameViewScales(Vector2.one);
    }

    static void EnforceScaleOnUpdate()
    {
        SetAllGameViewScales(Vector2.one);
        framesLeft--;
        if (framesLeft <= 0)
            EditorApplication.update -= EnforceScaleOnUpdate;
    }

    static void SetAllGameViewScales(Vector2 scale)
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType == null)
            return;

        var zoomField = gameViewType.GetField("m_ZoomArea", InstanceNonPublic);
        if (zoomField == null)
            return;

        foreach (var window in Resources.FindObjectsOfTypeAll(gameViewType))
        {
            var zoomArea = zoomField.GetValue(window);
            if (zoomArea == null)
                continue;

            var scaleField = zoomArea.GetType().GetField("m_Scale", InstanceNonPublic);
            if (scaleField == null)
                continue;

            scaleField.SetValue(zoomArea, scale);
            if (window is EditorWindow editorWindow)
                editorWindow.Repaint();
        }
    }
}
