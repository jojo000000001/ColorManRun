using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Isolated checks: no keyboard simulation, gameplay writes or saved scene changes.</summary>
public static class JumpTutorialHudChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);

    [MenuItem("Tools/ColorManRun/Check Keyboard Tutorials (Edit Mode)")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        Scene original = SceneManager.GetActiveScene();
        Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        int checks = 0;
        Action<bool, string> check = (condition, name) =>
        {
            if (!condition) throw new Exception("Tutorial check failed: " + name);
            checks++;
        };
        try
        {
            var first = new GameObject("First").AddComponent<BoxCollider>();
            first.transform.position = new Vector3(0, 0, 105);
            first.size = new Vector3(3, 2, 10);
            var second = new GameObject("Second").AddComponent<BoxCollider>();
            second.transform.position = new Vector3(0, 0, 170);
            second.size = new Vector3(3, 2, 20);
            Physics.SyncTransforms();
            var hud = new GameObject("Test HUD").AddComponent<JumpTutorialHud>();
            var icon = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
            var group = new GameObject("Hint", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var text = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            Set(hud, "jumpIcon", icon);
            Set(hud, "tutorialGroup", group);
            Set(hud, "tutorialLabel", text);
            Set(hud, "tutorials", new[]
            {
                new JumpTutorialHud.TutorialPoint { obstacle = first },
                new JumpTutorialHud.TutorialPoint { obstacle = second, hint = JumpTutorialHud.HintKind.Hold },
                new JumpTutorialHud.TutorialPoint() // Missing bindings are harmless.
            });
            Call(hud, "ResetRun");
            Action<float, float, GameState> tick = (z, speed, state) => Call(hud, "Tick", state, z, speed, 0.1f, false);
            Action<float, float, GameState> tickAir = (z, speed, state) => Call(hud, "Tick", state, z, speed, 0.1f, true);
            Func<int> active = () => Get<int>(hud, "activeIndex");
            tick(69, 20, GameState.Playing);
            check(active() == -1, "before front-edge threshold");
            tick(70, 20, GameState.Ready);
            check(active() == -1, "ready must not trigger");
            tick(70, 20, GameState.Playing);
            check(active() == 0 && text.text == "轻按 ↑ 跳跃", "tap threshold at 30m");
            float alpha = group.alpha;
            Vector3 scale = icon.localScale;
            tick(200, 20, GameState.Paused);
            check(active() == 0 && group.alpha == alpha && icon.localScale == scale, "pause freezes progress and animation");
            tick(110, 0, GameState.Playing);
            check(active() == 0, "stays through rear edge without timeout");
            alpha = group.alpha;
            tick(111, 20, GameState.Playing);
            check(active() == -1 && group.alpha < alpha && group.alpha > 0, "fade after rear edge");
            tick(111, 20, GameState.Playing);
            check(group.alpha == 0f, "fade completes");
            tick(70, 20, GameState.Playing);
            check(active() == -1, "once per run even on backtracking");
            tick(129, 20, GameState.Playing);
            check(active() == -1, "second threshold uses second front");
            tick(130, 20, GameState.Playing);
            check(active() == 1 && text.text == "长按 ↑ 跳得更远", "hold tutorial");
            Call(hud, "OnStateChanged", GameState.Dead);
            check(group.alpha == 0 && icon.localScale == Vector3.one, "death hides");
            Call(hud, "OnStateChanged", GameState.Playing);
            tick(70, 20, GameState.Playing);
            check(active() == 0, "new run resets consumed flags");
            Call(hud, "OnStateChanged", GameState.Paused);
            Call(hud, "OnStateChanged", GameState.Playing);
            check(active() == 0, "resume does not reset active tutorial");
            Call(hud, "ResetRun");
            tick(70, 10, GameState.Playing);
            check(active() == -1, "slow speed has shorter lead distance");
            tick(85, 10, GameState.Playing);
            check(active() == 0, "slow speed threshold");
            Call(hud, "ResetRun");
            tick(40, 40, GameState.Playing);
            check(active() == 0, "fast speed threshold");
            Call(hud, "ResetRun");
            tick(181, 20, GameState.Playing);
            tick(70, 20, GameState.Playing);
            check(active() == -1, "already passed obstacles skipped");
            first.transform.position += Vector3.forward * 10;
            first.transform.localScale = new Vector3(1, 1, 2);
            Physics.SyncTransforms();
            Call(hud, "ResetRun");
            tick(74, 20, GameState.Playing);
            check(active() == -1, "moved and scaled collider front");
            tick(75, 20, GameState.Playing);
            check(active() == 0, "reference tracks new bounds");
            Call(hud, "OnStateChanged", GameState.Clear);
            check(group.alpha == 0f, "clear hides immediately");
            Set(hud, "previousPlayerZ", 0f);
            var sample = typeof(JumpTutorialHud).GetMethod("SampleForwardSpeed", Private);
            check(Mathf.Abs((float)sample.Invoke(hud, new object[] { 1f, 0.05f }) - 20f) < 0.001f,
                "speed uses full-frame displacement, independent of Move substeps");
            check((float)sample.Invoke(hud, new object[] { 1f, 0.05f }) == 0f, "blocked player has zero speed");
            var pinkLanding = new GameObject("Pink landing track").AddComponent<BoxCollider>();
            pinkLanding.transform.position = new Vector3(0, -21, 270);
            pinkLanding.size = new Vector3(3, 2, 100);
            Physics.SyncTransforms();
            var colorIcon = new GameObject("Color", typeof(RectTransform)).GetComponent<RectTransform>();
            Set(hud, "colorIcon", colorIcon);
            Set(hud, "tutorials", new[]
            {
                new JumpTutorialHud.TutorialPoint
                {
                    obstacle = pinkLanding,
                    hint = JumpTutorialHud.HintKind.ChangeColor,
                    leadSeconds = 0f,
                    slowMotion = true
                }
            });
            Call(hud, "ResetRun");
            float timeScale = Time.timeScale;
            tickAir(210, 20, GameState.Playing);
            check(active() == -1 && !Get<bool>(hud, "slowUntilLanding"), "airborne over orange approach does not slow");
            tick(219, 20, GameState.Playing);
            check(active() == -1, "color hint waits until the pink track front edge");
            tick(220, 20, GameState.Playing);
            check(active() == 0 && text.text == "左键变颜色", "left-key color hint");
            check(colorIcon.localScale != Vector3.one && icon.localScale == Vector3.one, "color hint pulses the left key");
            check(Time.timeScale == timeScale, "edit mode does not change time scale");
            tick(321, 20, GameState.Playing);
            check(active() == -1, "color hint ends with the segment");
            Call(hud, "ResetRun");
            tick(220, 20, GameState.Playing);
            check(!Get<bool>(hud, "slowUntilLanding"), "color hint on the ground stays full speed");
            tickAir(230, 20, GameState.Playing);
            check(Get<bool>(hud, "slowUntilLanding"), "color hint slows only while airborne");
            check(active() == 0, "pink binding remains active after orange platform rear edge");
            tickAir(321, 20, GameState.Playing);
            check(active() == -1 && Get<bool>(hud, "slowUntilLanding"), "airborne slow continues past the segment");
            tick(330, 20, GameState.Playing);
            check(!Get<bool>(hud, "slowUntilLanding"), "landing restores full speed");

            var transitionPoints = new JumpTutorialHud.TutorialPoint[4];
            transitionPoints[0] = new JumpTutorialHud.TutorialPoint
            {
                obstacle = pinkLanding, hint = JumpTutorialHud.HintKind.ChangeColor,
                leadSeconds = 0f, slowMotion = true
            };
            for (int i = 1; i < transitionPoints.Length; i++)
            {
                var segment = new GameObject("Transition " + i).AddComponent<BoxCollider>();
                segment.transform.position = new Vector3(0, -21, 345 + i * 50);
                segment.size = new Vector3(3, 2, 50);
                transitionPoints[i] = new JumpTutorialHud.TutorialPoint
                {
                    obstacle = segment, hint = JumpTutorialHud.HintKind.JumpAndChangeColor,
                    leadSeconds = 1.5f, endAfterFrontMeters = 8f
                };
            }
            Physics.SyncTransforms();
            Set(hud, "tutorials", transitionPoints);
            Call(hud, "ResetRun");
            tickAir(230, 20, GameState.Playing);
            check(Get<bool>(hud, "slowUntilLanding"), "original slow tutorial still triggers with new points");
            tick(300, 20, GameState.Playing);
            check(!Get<bool>(hud, "slowUntilLanding"), "original landing still restores speed");
            tick(339, 20, GameState.Playing);
            check(active() == -1, "no junction reminder before lead threshold");
            text.supportRichText = false;
            for (int i = 1; i < transitionPoints.Length; i++)
            {
                float front = transitionPoints[i].obstacle.bounds.min.z;
                tick(front - 30, 20, GameState.Playing);
                check(active() == i, "junction reminder appears before transition " + i);
                check(text.supportRichText && text.text == "尝试<color=#FFA500>跳起来</color>\n并在空中<color=#FF69B4>切换</color>颜色",
                    "orange jump and pink change rich text " + i);
                check(icon.localScale != Vector3.one && colorIcon.localScale == icon.localScale,
                    "both key icons pulse " + i);
                tickAir(front, 20, GameState.Playing);
                check(!Get<bool>(hud, "slowUntilLanding"), "junction hint does not enable slow motion " + i);
                tick(front + 8, 20, GameState.Playing);
                check(active() == i, "junction includes eight-meter end boundary " + i);
                tick(front + 8.1f, 20, GameState.Playing);
                check(active() == -1, "junction ends early enough for next reminder " + i);
            }
            tick(340, 20, GameState.Playing);
            check(active() == -1, "junction reminders do not repeat on backtracking");
            Call(hud, "ResetRun");
            tick(340, 20, GameState.Playing);
            check(active() == 1, "new run allows junction reminder again");
            Call(hud, "OnStateChanged", GameState.Dead);
            check(group.alpha == 0 && icon.localScale == Vector3.one && colorIcon.localScale == Vector3.one,
                "death hides combined reminder and resets both key icons");
            Call(hud, "ResetRun");
            tick(379, 20, GameState.Playing);
            check(active() == -1, "late spawn skips expired junction window, not just segment end");
            transitionPoints[1].obstacle.transform.position += Vector3.forward * 10;
            Physics.SyncTransforms();
            Call(hud, "ResetRun");
            tick(349, 20, GameState.Playing);
            check(active() == -1, "moving segment moves junction lead threshold");
            tick(350, 20, GameState.Playing);
            check(active() == 1, "moved junction binds to collider rather than fixed coordinates");
            return "Keyboard tutorial checks: " + checks + " passed.";
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(isolated, true);
        }
    }
}
