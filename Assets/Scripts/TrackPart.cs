using UnityEngine;

/// <summary>
/// 一条跑道的逻辑颜色。显示走 MaterialPropertyBlock，不实例化材质。
/// </summary>
[DisallowMultipleComponent]
public sealed class TrackPart : MonoBehaviour
{
    [Range(0, 1)] public int colorIndex;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock colorBlock;

    static readonly Color[] Colors =
    {
        new Color(1f, 0.62f, 0.1f, 1f),
        new Color(1f, 0.18f, 0.72f, 1f)
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

    public void SetColor(int index)
    {
        colorIndex = Mathf.Clamp(index, 0, Colors.Length - 1);
        ApplyColor();
    }

    void OnValidate()
    {
        colorIndex = Mathf.Clamp(colorIndex, 0, Colors.Length - 1);
        ApplyColor();
    }

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
