using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlanarMirrorSetup
{
    public const string MaterialPath = "Assets/Materials/PlanarMirror.mat";

    [MenuItem("Tools/ColorManRun/Install Planar Mirrors")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存场景。");
        int layer = LayerMask.NameToLayer("MirrorGround");
        if (layer < 0) throw new InvalidOperationException("缺少 MirrorGround Layer。");
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PlanarMirror.shader");
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("镜面 Shader 未正确导入。");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        Scene original = SceneManager.GetActiveScene();
        try
        {
            foreach (string sceneName in new[] { "SampleScene", "Level2" })
            {
                string path = "Assets/Scenes/" + sceneName + ".unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    Camera camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>())
                        .Single(c => c.name == "PlayerFollowCamera");
                    if (sceneName == "SampleScene")
                    {
                        // The low pink track begins at z=220. Never extend the upper sheet across it.
                        Configure(scene, "MirrorGround", -4f, -180f, 220f, camera, material, layer);
                        Configure(scene, "MirrorGround_Lower", -24f, 220f, 760f, camera, material, layer);
                    }
                    else Configure(scene, "MirrorGround", -4f, -180f, 760f, camera, material, layer);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        finally { SceneManager.SetActiveScene(original); }
        AssetDatabase.SaveAssets();
    }

    static void Configure(Scene scene, string name, float y, float startZ, float endZ,
        Camera camera, Material material, int layer)
    {
        GameObject root = scene.GetRootGameObjects().SingleOrDefault(g => g.name == name);
        if (root != null && root.GetComponent<PlanarMirror>() != null) return; // Preserve manual tuning on repeat install.
        if (root == null)
        {
            root = GameObject.CreatePrimitive(PrimitiveType.Plane);
            root.name = name;
            SceneManager.MoveGameObjectToScene(root, scene);
            UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(root, "Create Planar Mirror");
        }
        else
        {
            if (root.GetComponent<MeshFilter>() == null || root.GetComponent<Collider>() != null)
                throw new InvalidOperationException(name + " 不是无碰撞的镜面占位平面，未覆盖。");
            Undo.RecordObject(root.transform, "Configure Planar Mirror");
            Undo.RecordObject(root.GetComponent<MeshRenderer>(), "Configure Planar Mirror");
        }
        root.layer = layer;
        root.transform.SetPositionAndRotation(new Vector3(0f, y, (startZ + endZ) * 0.5f), Quaternion.identity);
        root.transform.localScale = new Vector3(60f, 1f, (endZ - startZ) / 10f);
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var mirror = Undo.AddComponent<PlanarMirror>(root);
        mirror.sourceCamera = camera;
        mirror.reflectionMask = ~((1 << layer) | (1 << 5));
        mirror.textureSize = 512;
        EditorUtility.SetDirty(mirror);
    }
}
