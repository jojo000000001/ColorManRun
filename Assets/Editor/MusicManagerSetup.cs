using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in installer; asset paths are resolved only during authoring.</summary>
public static class MusicManagerSetup
{
    const string PrefabPath = "Assets/Prefabs/MusicManager.prefab";
    static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Level2.unity" };

    [MenuItem("Tools/ColorManRun/Install Music Manager")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("请先保存当前场景，再安装音频管理器。");

        // Validate every clip before touching scenes. FLAC clips are supported by this editor.
        string[] paths = { "Assets/Music/Bgm.wav", "Assets/Music/bgm2.wav", "Assets/Music/run.flac",
            "Assets/Music/jump.flac", "Assets/Music/crash.flac", "Assets/Music/stand2.flac" };
        foreach (string path in paths)
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                throw new InvalidOperationException("音频未正确导入：" + path);
        for (int i = 0; i < paths.Length; i++)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(paths[i]);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = i < 2 ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = i >= 2;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            CreatePrefab(paths);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Scene original = SceneManager.GetActiveScene();
        try
        {
            foreach (string path in Scenes)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MusicManager>(true)).ToArray();
                    if (managers.Length > 1) throw new InvalidOperationException(path + " 有多个音频管理器，请先检查。");
                    if (managers.Length == 0)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        Undo.RegisterCreatedObjectUndo(instance, "Install Music Manager");
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                finally
                {
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally { SceneManager.SetActiveScene(original); }
    }

    static void CreatePrefab(string[] paths)
    {
        GameObject root = new GameObject("MusicManager");
        try
        {
            // Inactive while authoring: never execute singleton/persistence lifecycle in Edit mode.
            root.SetActive(false);
            var manager = root.AddComponent<MusicManager>();
            var serialized = new SerializedObject(manager);
            var music = serialized.FindProperty("levelMusic");
            music.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var entry = music.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sceneName").stringValue = i == 0 ? "SampleScene" : "Level2";
                entry.FindPropertyRelative("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
            }
            string[] clips = { "run", "jump", "crash", "landing" };
            for (int i = 0; i < clips.Length; i++)
                serialized.FindProperty(clips[i]).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i + 2]);
            string[] fields = { "musicSource", "runSource", "actionSource", "crashSource" };
            string[] names = { "BGM", "Run", "Actions", "Crash" };
            float[] volumes = { 0.35f, 0.25f, 0.7f, 0.8f };
            for (int i = 0; i < fields.Length; i++)
            {
                var child = new GameObject(names[i]);
                child.transform.SetParent(root.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = i < 2;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.volume = volumes[i];
                source.priority = i == 0 ? 32 : 128;
                serialized.FindProperty(fields[i]).objectReferenceValue = source;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
