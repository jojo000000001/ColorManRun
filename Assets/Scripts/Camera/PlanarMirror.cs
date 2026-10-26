using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Built-in planar reflection. One bounded surface and render target per mirror.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
public sealed class PlanarMirror : MonoBehaviour
{
    public Camera sourceCamera;
    public LayerMask reflectionMask = ~0;
    [Range(128, 2048)] public int textureSize = 512;
    [Min(0.001f)] public float clipPlaneOffset = 0.03f;

    Camera reflectionCamera;
    RenderTexture reflectionTexture;
    MeshRenderer surface;
    MaterialPropertyBlock properties;
    readonly Plane[] frustum = new Plane[6];
    static bool renderingReflection;
    static readonly int ReflectionTex = Shader.PropertyToID("_ReflectionTex");
    static readonly int ReflectionVP = Shader.PropertyToID("_ReflectionVP");
    static readonly int MirrorSize = Shader.PropertyToID("_MirrorSize");

    void OnEnable()
    {
        surface = GetComponent<MeshRenderer>();
        properties = new MaterialPropertyBlock();
        Camera.onPreCull += BeforeCameraCull;
    }

    void OnDisable()
    {
        Camera.onPreCull -= BeforeCameraCull;
        if (surface != null) surface.SetPropertyBlock(null);
        ReleaseResources();
    }

    void BeforeCameraCull(Camera camera)
    {
        if (renderingReflection || camera == null || camera != sourceCamera || !isActiveAndEnabled ||
            surface == null || !surface.enabled || GraphicsSettings.currentRenderPipeline != null)
            return;
        if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
        Vector3 normal = transform.up;
        Vector3 point = transform.position;
        // One-sided floor: do not keep rendering an upper mirror once the camera falls below it.
        if (Vector3.Dot(normal, camera.transform.position - point) <= clipPlaneOffset) return;
        GeometryUtility.CalculateFrustumPlanes(camera, frustum);
        if (!GeometryUtility.TestPlanesAABB(frustum, surface.bounds)) return;

        EnsureResources(camera);
        reflectionCamera.CopyFrom(camera);
        reflectionCamera.enabled = false;
        reflectionCamera.cameraType = CameraType.Reflection;
        reflectionCamera.targetTexture = reflectionTexture;
        reflectionCamera.allowMSAA = false;
        reflectionCamera.allowHDR = false;
        reflectionCamera.useOcclusionCulling = false;
        // All mirrors share this layer, not just this particular renderer.
        int mirrorLayer = LayerMask.NameToLayer("MirrorGround");
        int excluded = (1 << gameObject.layer) | (1 << 5);
        if (mirrorLayer >= 0) excluded |= 1 << mirrorLayer;
        reflectionCamera.cullingMask = camera.cullingMask & reflectionMask.value & ~excluded;
        // Copy a per-camera skybox when present; otherwise preserve RenderSettings.skybox.
        var sourceSky = camera.GetComponent<Skybox>();
        var mirrorSky = reflectionCamera.GetComponent<Skybox>();
        mirrorSky.enabled = sourceSky != null && sourceSky.enabled && sourceSky.material != null;
        mirrorSky.material = mirrorSky.enabled ? sourceSky.material : null;

        Matrix4x4 reflection = ReflectionMatrix(point, normal);
        reflectionCamera.transform.SetPositionAndRotation(
            reflection.MultiplyPoint(camera.transform.position),
            Quaternion.LookRotation(Vector3.Reflect(camera.transform.forward, normal),
                Vector3.Reflect(camera.transform.up, normal)));
        reflectionCamera.worldToCameraMatrix = camera.worldToCameraMatrix * reflection;
        // Offset toward the visible side to reject geometry below the mirror without z-fighting.
        Vector3 cameraPoint = reflectionCamera.worldToCameraMatrix.MultiplyPoint(point + normal * clipPlaneOffset);
        Vector3 cameraNormal = reflectionCamera.worldToCameraMatrix.MultiplyVector(normal).normalized;
        Vector4 clipPlane = new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
            -Vector3.Dot(cameraPoint, cameraNormal));
        reflectionCamera.projectionMatrix = camera.projectionMatrix;
        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);

        bool previousCulling = GL.invertCulling;
        try
        {
            renderingReflection = true;
            GL.invertCulling = !previousCulling;
            reflectionCamera.Render();
        }
        finally
        {
            GL.invertCulling = previousCulling;
            renderingReflection = false;
        }
        surface.GetPropertyBlock(properties);
        properties.SetTexture(ReflectionTex, reflectionTexture);
        // Sample in Unity's upright texture UV space; Render() already handles the RT raster flip.
        // Applying the render-target projection flip here again would vertically invert the image.
        properties.SetMatrix(ReflectionVP, GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, false)
            * reflectionCamera.worldToCameraMatrix);
        properties.SetVector(MirrorSize, new Vector4(surface.bounds.size.x, surface.bounds.size.z, 0f, 0f));
        surface.SetPropertyBlock(properties);
    }

    public static Matrix4x4 ReflectionMatrix(Vector3 point, Vector3 normal)
    {
        normal.Normalize();
        float d = -Vector3.Dot(normal, point);
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = 1f - 2f * normal.x * normal.x;
        m.m01 = -2f * normal.x * normal.y;
        m.m02 = -2f * normal.x * normal.z;
        m.m03 = -2f * d * normal.x;
        m.m10 = -2f * normal.y * normal.x;
        m.m11 = 1f - 2f * normal.y * normal.y;
        m.m12 = -2f * normal.y * normal.z;
        m.m13 = -2f * d * normal.y;
        m.m20 = -2f * normal.z * normal.x;
        m.m21 = -2f * normal.z * normal.y;
        m.m22 = 1f - 2f * normal.z * normal.z;
        m.m23 = -2f * d * normal.z;
        return m;
    }

    void EnsureResources(Camera camera)
    {
        if (reflectionCamera == null)
        {
            var helper = new GameObject(name + " Reflection Camera", typeof(Camera), typeof(Skybox));
            helper.hideFlags = HideFlags.HideAndDontSave;
            reflectionCamera = helper.GetComponent<Camera>();
            reflectionCamera.enabled = false;
        }
        int width = Mathf.Clamp(Mathf.NextPowerOfTwo(textureSize), 128, 2048);
        int height = Mathf.Max(64, Mathf.RoundToInt(width / Mathf.Max(0.1f, camera.aspect)));
        if (reflectionTexture != null && reflectionTexture.width == width && reflectionTexture.height == height) return;
        if (reflectionTexture != null)
        {
            reflectionCamera.targetTexture = null;
            reflectionTexture.Release();
            DestroyOwned(reflectionTexture);
        }
        reflectionTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = name + " Reflection RT",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            antiAliasing = 1
        };
        reflectionTexture.Create();
    }

    void ReleaseResources()
    {
        if (reflectionCamera != null) reflectionCamera.targetTexture = null;
        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            DestroyOwned(reflectionTexture);
            reflectionTexture = null;
        }
        if (reflectionCamera != null)
        {
            DestroyOwned(reflectionCamera.gameObject);
            reflectionCamera = null;
        }
    }

    static void DestroyOwned(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
