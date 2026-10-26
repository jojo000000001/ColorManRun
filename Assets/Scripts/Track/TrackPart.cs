using UnityEngine;

/// <summary>
/// 一条跑道的逻辑颜色。显示走 MaterialPropertyBlock，不实例化材质。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TrackPart : MonoBehaviour
{
    // Authoring is handled by TrackPartBinding; this mirror is kept hidden
    // for gameplay/color checks and backwards compatibility.
    [HideInInspector] public TrackPartDefinition definition;

    [Range(0, 2)] public int colorIndex;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock colorBlock;

    static readonly Color[] Colors =
    {
        // Warm yellow-orange used by the runway and the player's color channel 0.
        new Color(1f, 0.675f, 0.302f, 1f),
        new Color(1f, 0.18f, 0.72f, 1f),
        Color.black
    };

    public static int ColorCount
    {
        get { return Colors.Length; }
    }

    public static Color ColorOf(int index)
    {
        if (index < 0)
            index = 0;
        if (index >= Colors.Length)
            index = Colors.Length - 1;
        return Colors[index];
    }

    void Awake()
    {
        colorIndex = Mathf.Clamp(colorIndex, 0, Colors.Length - 1);
        ApplyColor();
    }

    void OnEnable()
    {
        // Property blocks are not serialized. Restore the preview after reload.
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= RefreshInEditor;
            UnityEditor.EditorApplication.update += RefreshInEditor;
            return;
        }
#endif
        if (definition != null)
            colorIndex = definition.ColorIndex;
        ApplyColor();
    }

    public void SetColor(int index)
    {
        colorIndex = Mathf.Clamp(index, 0, Colors.Length - 1);
        ApplyColor();
    }

    public void ApplyDefinition(TrackPartDefinition source)
    {
        if (source == null)
            return;

        definition = source;
        SetColor(source.ColorIndex);
    }

    void OnValidate()
    {
        colorIndex = Mathf.Clamp(colorIndex, 0, Colors.Length - 1);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= RefreshInEditor;
        UnityEditor.EditorApplication.update += RefreshInEditor;
#endif
    }

#if UNITY_EDITOR
    void RefreshInEditor()
    {
        UnityEditor.EditorApplication.update -= RefreshInEditor;
        if (this == null || Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(this))
            return;
        TrackPartBinding binding = GetComponent<TrackPartBinding>();
        if (binding != null && binding.definition != null)
            binding.ApplyDefinition();
        else
            ApplyColor();
    }

    void OnDisable()
    {
        UnityEditor.EditorApplication.update -= RefreshInEditor;
    }
#endif

    void ApplyColor()
    {
        if (colorBlock == null)
            colorBlock = new MaterialPropertyBlock();
        Color color = ColorOf(colorIndex);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(colorBlock);
            colorBlock.SetColor(ColorId, color);
            colorBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(colorBlock);
        }
    }
}
