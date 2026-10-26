using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks shared prefab inheritance without rewriting scenes or shared tuning assets.</summary>
public static class SharedPlayerChecks
{
    const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";
    static int passed;

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed++;
    }

    [MenuItem("Tools/ColorManRun/Check Shared Player Prefab")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run prefab checks in Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene edits before checking.");

        passed = 0;
        Scene original = SceneManager.GetActiveScene();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null, "Shared player prefab missing.");
        CheckPlayer(prefab.GetComponent<PlayerController>());
        foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Level2.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var players = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)).ToArray();
                Check(players.Length == 1, path + ": expected one player.");
                PlayerController player = players[0];
                Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player) == PrefabPath,
                    path + ": player must inherit the shared prefab.");
                CheckPlayer(player);
                Check(PrefabUtility.GetAddedComponents(player.gameObject).Count == 0,
                    path + ": extra player components override the shared configuration.");
                Check(!PrefabUtility.HasPrefabInstanceAnyOverrides(player.gameObject, false),
                    path + ": only default overrides (spawn transform/name) are expected.");
                var cameras = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CameraFollow>(true)).ToArray();
                Check(cameras.Length == 1 && cameras[0].target == player.transform,
                    path + ": camera must target this scene's player.");
                Check(cameras[0].useInitialPose, path + ": preserve initial camera pose.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(original);
            }
        }
        return "PASS " + passed + " shared-player prefab checks.";
    }

    static void CheckPlayer(PlayerController player)
    {
        Check(player != null, "PlayerController missing.");
        Check(player.gameObject.layer == LayerMask.NameToLayer("Player") && player.CompareTag("Player"),
            "Player tag/layer incorrect.");
        Check(player.movementSettings == AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
            "Assets/SO/SharedPlayerMovement.asset"), "Shared movement settings missing.");
        Check(player.palette == player.GetComponent<PlayerColorPalette>() && player.palette.colorRenderer != null
            && player.palette.colorRenderer.transform.IsChildOf(player.transform), "Palette binding incorrect.");
        var animator = player.GetComponent<Animator>();
        Check(animator != null && animator.runtimeAnimatorController != null && !animator.applyRootMotion,
            "Animator configuration missing.");
        Check(player.GetComponent<PlayerAnimator>().animator == animator, "Animation driver binding incorrect.");
        var controller = player.GetComponent<CharacterController>();
        Check(controller != null && Mathf.Approximately(controller.height, player.halfHeight * 2f)
            && controller.center == player.centerOffset, "CharacterController geometry incorrect.");
        Check((player.groundMask.value & (1 << LayerMask.NameToLayer("Ground"))) != 0
            && (player.groundMask.value & (1 << LayerMask.NameToLayer("MirrorGround"))) == 0,
            "Ground queries must exclude mirrors.");
        var shadow = player.GetComponent<FootShadow>();
        Check(shadow != null && shadow.enabled, "Foot shadow missing.");
        var data = new SerializedObject(shadow);
        Check(data.FindProperty("player").objectReferenceValue == player, "Shadow player binding incorrect.");
        var renderer = data.FindProperty("blobRenderer").objectReferenceValue as SpriteRenderer;
        Check(renderer != null && renderer.transform.parent == player.transform && renderer.sprite != null,
            "Shadow must be a bound child of the player prefab.");
        Check(player.GetComponentsInChildren<SpriteRenderer>(true).Length == 1,
            "Expected exactly one shadow renderer.");
        foreach (Component component in player.GetComponentsInChildren<Component>(true))
        {
            Check(component != null, "Missing script in player hierarchy.");
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                    Check(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                        component.name + ": broken reference " + property.propertyPath);
        }
    }

    [MenuItem("Tools/ColorManRun/Check Foot Shadow (Paused Play Mode)")]
    public static void RunRuntimeFromMenu() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
            throw new InvalidOperationException("Enter and pause Play Mode before checking shadows.");
        passed = 0;
        var player = UnityEngine.Object.FindObjectOfType<PlayerController>();
        var shadow = player.GetComponent<FootShadow>();
        var renderer = player.transform.Find("FootShadow").GetComponent<SpriteRenderer>();
        var controller = player.GetComponent<CharacterController>();
        Vector3 original = player.transform.position;
        bool wasEnabled = controller.enabled;
        var update = typeof(FootShadow).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        Action<Vector3> sample = position =>
        {
            player.transform.position = position;
            Physics.SyncTransforms();
            update.Invoke(shadow, null);
        };
        try
        {
            controller.enabled = false;
            sample(new Vector3(0f, 0.21f, 20f));
            Check(renderer.enabled, "Grounded shadow not visible.");
            Check(Mathf.Abs(renderer.transform.position.y - 0.04f) < 0.01f, "Shadow not on ground.");
            float groundSize = renderer.transform.localScale.x;
            float groundAlpha = renderer.color.a;
            sample(new Vector3(0f, 2.21f, 20f));
            Check(renderer.enabled && renderer.color.a < groundAlpha, "Airborne shadow must fade.");
            Check(renderer.transform.localScale.x < groundSize, "Airborne shadow must shrink.");
            Check(Mathf.Abs(renderer.transform.position.y - 0.04f) < 0.01f, "Shadow must stay on ground during jumps.");
            sample(new Vector3(0f, 5.21f, 20f));
            Check(!renderer.enabled, "High airborne shadow must disappear.");
            sample(new Vector3(10000f, 0.21f, 20f));
            Check(!renderer.enabled, "No-ground shadow must disappear.");
            sample(new Vector3(0f, 0.21f, 20f));
            shadow.enabled = false;
            Check(!renderer.enabled, "Disabling shadow must hide its renderer.");
            shadow.enabled = true;
            update.Invoke(shadow, null);
            Check(renderer.enabled && player.GetComponentsInChildren<SpriteRenderer>(true).Length == 1,
                "Re-enabling shadow must reuse the prefab child.");
            Check(renderer.gameObject.scene == player.gameObject.scene && renderer.transform.parent == player.transform,
                "Shadow lifetime must belong to the scene player.");
            return "PASS " + passed + " foot-shadow runtime checks in " + player.gameObject.scene.name + ".";
        }
        finally
        {
            shadow.enabled = true;
            sample(original);
            controller.enabled = wasEnabled;
        }
    }
}
