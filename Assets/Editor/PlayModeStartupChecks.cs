using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in startup timing across domain reloads. Never runs a benchmark automatically.</summary>
[InitializeOnLoad]
public static class PlayModeStartupChecks
{
    const string Prefix = "ColorManRun.StartupCheck.";

    static PlayModeStartupChecks()
    {
        EditorApplication.playModeStateChanged += OnStateChanged;
        if (SessionState.GetBool(Prefix + "active", false))
            EditorApplication.update += ObserveFirstFrame;
    }

    public static string LastResult => SessionState.GetString(Prefix + "result", "No measurement");

    [MenuItem("Tools/ColorManRun/Measure Play Startup (Stops After First Frame)")]
    public static void Measure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("请退出 Play Mode 并等待编译完成后测量。");
        SessionState.SetBool(Prefix + "active", true);
        SessionState.SetString(Prefix + "result", "Measuring");
        EditorApplication.update -= ObserveFirstFrame;
        EditorApplication.update += ObserveFirstFrame;
        Stamp("request");
        EditorApplication.isPlaying = true;
    }

    static void Stamp(string key)
    {
        SessionState.SetString(Prefix + key, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
    }

    static double Read(string key)
    {
        return double.Parse(SessionState.GetString(Prefix + key, "0"), CultureInfo.InvariantCulture);
    }

    static void OnStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Prefix + "active", false))
            return;
        if (state == PlayModeStateChange.ExitingEditMode)
            Stamp("exitEdit");
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Stamp("entered");
            SessionState.SetInt(Prefix + "frame", Time.frameCount);
            SessionState.SetBool(Prefix + "waitingForFrame", true);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Prefix + "active", false);
            SessionState.SetBool(Prefix + "waitingForFrame", false);
            EditorApplication.update -= ObserveFirstFrame;
        }
    }

    static void ObserveFirstFrame()
    {
        if (!SessionState.GetBool(Prefix + "active", false) ||
            !SessionState.GetBool(Prefix + "waitingForFrame", false) ||
            !EditorApplication.isPlaying || Time.frameCount <= SessionState.GetInt(Prefix + "frame", 0))
            return;
        double now = EditorApplication.timeSinceStartup;
        string result = string.Format(CultureInfo.InvariantCulture,
            "requestToFirstFrameMs={0:F1}, playTransitionMs={1:F1}, enteredToFirstFrameMs={2:F1}, domainReloadDisabled={3}, sceneReloadDisabled={4}",
            (now - Read("request")) * 1000.0, (Read("entered") - Read("exitEdit")) * 1000.0,
            (now - Read("entered")) * 1000.0,
            EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0,
            EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0);
        SessionState.SetString(Prefix + "result", result);
        SessionState.SetBool(Prefix + "active", false);
        SessionState.SetBool(Prefix + "waitingForFrame", false);
        EditorApplication.update -= ObserveFirstFrame;
        Debug.Log("Play startup: " + result);
        EditorApplication.isPlaying = false;
    }
}
