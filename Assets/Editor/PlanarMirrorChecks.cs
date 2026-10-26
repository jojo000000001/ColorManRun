using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated render/readback checks, including UV orientation and below-plane clipping.</summary>
public static class PlanarMirrorChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(PlanarMirror mirror, string field) => (T)typeof(PlanarMirror).GetField(field, Private).GetValue(mirror);
    static void Check(List<string> passed, string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
        passed.Add(name);
    }

    [MenuItem("Tools/ColorManRun/Run Planar Mirror Checks")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        var passed = new List<string>();
        Scene original = SceneManager.GetActiveScene();
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        var owned = new List<UnityEngine.Object>();
        var oldTarget = RenderTexture.active;
        bool oldCulling = GL.invertCulling;
        int oldLights = QualitySettings.pixelLightCount;
        try
        {
            var sourceObject = new GameObject("Mirror Test Camera", typeof(Camera));
            var source = sourceObject.GetComponent<Camera>();
            source.enabled = false;
            source.clearFlags = CameraClearFlags.SolidColor;
            source.backgroundColor = Color.black;
            source.farClipPlane = 60f;
            source.allowHDR = false;
            source.allowMSAA = false;
            source.aspect = 2f;
            var target = new RenderTexture(512, 256, 16);
            target.Create();
            owned.Add(target);
            source.targetTexture = target;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            UnityEngine.Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.layer = LayerMask.NameToLayer("MirrorGround");
            plane.transform.localScale = new Vector3(5, 1, 5);
            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/PlanarMirror.shader"));
            owned.Add(material);
            material.SetFloat("_Opacity", 1f);
            material.SetColor("_Tint", Color.white);
            plane.GetComponent<Renderer>().sharedMaterial = material;
            var mirror = plane.AddComponent<PlanarMirror>();
            mirror.sourceCamera = source;
            var cyan = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cyanMaterial = new Material(Shader.Find("Unlit/Color"));
            cyanMaterial.color = Color.cyan;
            owned.Add(cyanMaterial);
            cyan.GetComponent<Renderer>().sharedMaterial = cyanMaterial;
            var red = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var redMaterial = new Material(Shader.Find("Unlit/Color"));
            redMaterial.color = Color.red;
            owned.Add(redMaterial);
            red.GetComponent<Renderer>().sharedMaterial = redMaterial;
            var readback = new Texture2D(512, 256, TextureFormat.RGB24, false);
            owned.Add(readback);

            foreach (float height in new[] { 0f, -20f })
            {
                Vector3 origin = new Vector3(10000f, height, 0f);
                plane.transform.position = origin;
                cyan.transform.position = origin + new Vector3(0f, 2f, 0f);
                red.transform.position = origin + new Vector3(2f, -1.5f, 0f);
                source.transform.position = origin + new Vector3(0f, 5f, -10f);
                source.transform.LookAt(origin + Vector3.up);
                foreach (float roll in new[] { 0f, 13f })
                {
                    source.transform.LookAt(origin + Vector3.up);
                    source.transform.Rotate(0f, 0f, roll, Space.Self);
                    Matrix4x4 reflection = PlanarMirror.ReflectionMatrix(origin, Vector3.up);
                    Vector3 reflected = reflection.MultiplyPoint(cyan.transform.position);
                    Check(passed, height + "m reflection math roll=" + roll,
                        (reflected - (origin + Vector3.down * 2f)).sqrMagnitude < 0.001f);
                    source.Render();
                    var texture = Get<RenderTexture>(mirror, "reflectionTexture");
                    var camera = Get<Camera>(mirror, "reflectionCamera");
                    Check(passed, "RT/camera without listener", texture != null && camera != null && !camera.enabled && camera.GetComponent<AudioListener>() == null);
                    Check(passed, "all mirrors excluded", (camera.cullingMask & (1 << plane.layer)) == 0);
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, 512, 256), 0, 0);
                    readback.Apply();
                    var pixels = readback.GetPixels();
                    Check(passed, "above-plane geometry reflected", pixels.Count(IsCyan) > 10);
                    Check(passed, "below-plane geometry clipped", pixels.Count(c => c.r > 0.5f && c.g < 0.1f && c.b < 0.1f) == 0);
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 512, 256), 0, 0);
                    readback.Apply();
                    Vector3 expected = source.WorldToViewportPoint(reflected);
                    Check(passed, "projected reflection correctly oriented", CyanNear(readback, expected));
                    cyan.transform.position += Vector3.right;
                    source.Render();
                    Check(passed, "reuse RT rather than allocate every frame", texture == Get<RenderTexture>(mirror, "reflectionTexture"));
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 512, 256), 0, 0);
                    readback.Apply();
                    Check(passed, "moving object reflected in same render", CyanNear(readback, source.WorldToViewportPoint(reflection.MultiplyPoint(cyan.transform.position))));
                    cyan.transform.position -= Vector3.right;
                }
            }
            Check(passed, "global render state restored", GL.invertCulling == oldCulling && QualitySettings.pixelLightCount == oldLights);
            var helper = Get<Camera>(mirror, "reflectionCamera");
            var resource = Get<RenderTexture>(mirror, "reflectionTexture");
            mirror.enabled = false;
            Check(passed, "disable releases camera and RT", helper == null && resource == null);
            mirror.enabled = true;
            source.Render();
            Check(passed, "reenable recreates resources", Get<Camera>(mirror, "reflectionCamera") != null && Get<RenderTexture>(mirror, "reflectionTexture") != null);
            return "PASS " + passed.Count + " mirror checks\n" + string.Join("\n", passed);
        }
        finally
        {
            RenderTexture.active = oldTarget;
            GL.invertCulling = oldCulling;
            EditorSceneManager.CloseScene(fixture, true);
            foreach (var asset in owned)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            SceneManager.SetActiveScene(original);
        }
    }

    static bool IsCyan(Color c) => c.g > 0.4f && c.b > 0.4f && c.r < 0.15f;
    static bool CyanNear(Texture2D texture, Vector3 viewport)
    {
        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return false;
        int x = Mathf.RoundToInt(viewport.x * texture.width);
        int y = Mathf.RoundToInt(viewport.y * texture.height);
        int count = 0;
        for (int dx = -5; dx <= 5; dx++)
            for (int dy = -5; dy <= 5; dy++)
                if (IsCyan(texture.GetPixel(Mathf.Clamp(x + dx, 0, texture.width - 1), Mathf.Clamp(y + dy, 0, texture.height - 1)))) count++;
        return count > 5;
    }
}
