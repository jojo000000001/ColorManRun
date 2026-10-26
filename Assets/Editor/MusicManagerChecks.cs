using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Manual checks; never save runtime test state into scenes or assets.</summary>
public static class MusicManagerChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);
    static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);
    static void Check(List<string> passed, string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        passed.Add(name);
    }

    [MenuItem("Tools/ColorManRun/Check Music Configuration")]
    public static void ConfigurationFromMenu() => Debug.Log(Configuration());

    public static string Configuration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        var passed = new List<string>();
        Scene original = SceneManager.GetActiveScene();
        try
        {
            foreach (string name in new[] { "SampleScene", "Level2" })
            {
                string path = "Assets/Scenes/" + name + ".unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MusicManager>(true)).ToArray();
                    Check(passed, name + " 一个启用的 MusicManager", managers.Length == 1 && managers[0].isActiveAndEnabled);
                    var m = managers[0];
                    var map = Get<MusicManager.LevelMusic[]>(m, "levelMusic");
                    Check(passed, name + " BGM 映射", map.Any(x => x.sceneName == name && x.clip != null && x.clip.name == (name == "SampleScene" ? "Bgm" : "bgm2")));
                    foreach (string field in new[] { "run", "jump", "crash", "landing" })
                        Check(passed, name + " " + field + " clip", Get<AudioClip>(m, field) != null);
                    var sources = new List<AudioSource>();
                    foreach (string field in new[] { "musicSource", "runSource", "actionSource", "crashSource" })
                    {
                        var source = Get<AudioSource>(m, field);
                        Check(passed, name + " " + field + " 2D/no autoplay", source != null && source.spatialBlend == 0f && !source.playOnAwake);
                        sources.Add(source);
                    }
                    Check(passed, name + " 独立音源/循环配置", sources.Distinct().Count() == 4 && sources[0].loop && sources[1].loop && !sources[2].loop && !sources[3].loop);
                    Check(passed, name + " 一个有效 AudioListener", scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioListener>(true)).Count(x => x.isActiveAndEnabled) == 1);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally { SceneManager.SetActiveScene(original); }
        return "PASS " + passed.Count + " configuration checks";
    }

    [MenuItem("Tools/ColorManRun/Check Music Runtime (Paused Play Mode)")]
    public static void RuntimeFromMenu() => Debug.Log(Runtime());

    public static string Runtime()
    {
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
            throw new InvalidOperationException("请进入 Play Mode 并暂停；测试后退出 Play Mode 恢复关卡。");
        var m = MusicManager.Instance;
        var game = GameManager.Instance;
        if (m == null || game == null) throw new InvalidOperationException("缺少运行中的管理器。");
        var player = Get<PlayerController>(m, "player");
        var bgm = Get<AudioSource>(m, "musicSource");
        var run = Get<AudioSource>(m, "runSource");
        var action = Get<AudioSource>(m, "actionSource");
        var crash = Get<AudioSource>(m, "crashSource");
        var passed = new List<string>();
        // Unity's editor pause also suspends audio. Unpause only inside this synchronous call:
        // no gameplay frames advance between assertions. Re-pause in finally, including failures.
        var savedPlayer = typeof(PlayerController).GetFields(Private)
            .Where(f => f.FieldType.IsValueType || f.Name == "support")
            .ToDictionary(f => f, f => f.GetValue(player));
        GameState savedState = game.State;
        float savedTime = game.LevelTime;
        try
        {
            EditorApplication.isPaused = false;
            Call(game, "SetState", GameState.Playing);
            Check(passed, "BGM 播放", bgm.isPlaying && bgm.loop);
            Call(m, "UpdateGroundAudio", true, true);
            Check(passed, "落地移动跑步循环", run.isPlaying && run.clip.name == "run");
            Call(m, "UpdateGroundAudio", true, false);
            Check(passed, "静止停跑步", !run.isPlaying);
            Call(player, "StartJump", false, false);
            Check(passed, "成功起跳事件播放 jump", action.isPlaying && !run.isPlaying);
            action.Stop();
            Call(player, "StartJump", false, true);
            Check(passed, "二段跳事件播放 jump", action.isPlaying);
            action.Stop();
            Call(m, "UpdateGroundAudio", false, true);
            Check(passed, "空中不播放跑步/落地", !run.isPlaying && !action.isPlaying);
            Call(m, "UpdateGroundAudio", true, true);
            Check(passed, "空中转落地播放 stand2", action.isPlaying && run.isPlaying);
            action.Stop();
            Call(m, "UpdateGroundAudio", true, true);
            Check(passed, "持续着地不重复落地音效", !action.isPlaying);
            bgm.time = 5f;
            Call(game, "SetState", GameState.Paused);
            Check(passed, "逻辑暂停全部音源", !bgm.isPlaying && !run.isPlaying && Get<bool>(m, "paused"));
            Call(game, "SetState", GameState.Playing);
            Check(passed, "恢复不重置音乐进度", bgm.isPlaying && Mathf.Abs(bgm.time - 5f) < 0.15f);
            Check(passed, "恢复跑步循环", run.isPlaying);
            Call(game, "SetState", GameState.Dead);
            Check(passed, "死亡独立 crash，停止 BGM 和动作", crash.isPlaying && !bgm.isPlaying && !run.isPlaying && !action.isPlaying);
            Call(m, "BindScene", game.gameObject.scene);
            Check(passed, "重绑场景不截断 crash", crash.isPlaying);
            crash.Stop();
            Call(game, "SetState", GameState.Dead);
            Check(passed, "重复 Dead 不重播 crash", !crash.isPlaying);
            Call(game, "SetState", GameState.Playing);
            Call(game, "SetState", GameState.Clear);
            Check(passed, "通关停止关卡音频", !bgm.isPlaying && !run.isPlaying && !action.isPlaying);
            Call(m, "SelectMusic", "Level2");
            Check(passed, "第二关选择 bgm2", bgm.clip.name == "bgm2");
            Call(m, "SelectMusic", "SampleScene");
            Check(passed, "第一关选择 Bgm", bgm.clip.name == "Bgm");
            Check(passed, "慢动作不变调", bgm.pitch == 1f && run.pitch == 1f && action.pitch == 1f && crash.pitch == 1f);
            Check(passed, "跨场景持久化单例", m.gameObject.scene.name == "DontDestroyOnLoad" && UnityEngine.Object.FindObjectsOfType<MusicManager>().Length == 1);
            return "PASS " + passed.Count + " runtime checks\n" + string.Join("\n", passed);
        }
        finally
        {
            Call(m, "StopAll");
            Call(game, "SetState", savedState);
            typeof(GameManager).GetField("<LevelTime>k__BackingField", Private).SetValue(game, savedTime);
            foreach (var pair in savedPlayer) pair.Key.SetValue(player, pair.Value);
            Call(m, "BindScene", game.gameObject.scene);
            EditorApplication.isPaused = true;
        }
    }
}
