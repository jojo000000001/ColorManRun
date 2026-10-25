using UnityEngine;

/// <summary>
/// 橙色/粉色材质组。仅替换指定子网格的共享材质引用，不修改材质资源。
/// </summary>
[DisallowMultipleComponent]
public class PlayerColorPalette : MonoBehaviour
{
    public SkinnedMeshRenderer colorRenderer;

    [Header("Submesh index on colorRenderer")]
    public int hatMaterialIndex;
    public int bodyMaterialIndex = 1;
    public int feetMaterialIndex = 2;

    [Header("Orange")]
    public Material hatOrange;
    public Material bodyOrange;
    public Material feetOrange;

    [Header("Pink")]
    public Material hatPink;
    public Material bodyPink;
    public Material feetPink;

    public bool ApplyColor(int colorIndex)
    {
        if (colorRenderer == null || colorIndex < 0 || colorIndex > 1)
            return false;

        Material hat = colorIndex == 0 ? hatOrange : hatPink;
        Material body = colorIndex == 0 ? bodyOrange : bodyPink;
        Material feet = colorIndex == 0 ? feetOrange : feetPink;
        Material[] materials = colorRenderer.sharedMaterials;
        if (hat == null || body == null || feet == null ||
            !IsValidIndex(hatMaterialIndex, materials.Length) ||
            !IsValidIndex(bodyMaterialIndex, materials.Length) ||
            !IsValidIndex(feetMaterialIndex, materials.Length))
        {
            Debug.LogWarning("Player 调色板缺少橙/粉材质，或子网格索引不正确。", this);
            return false;
        }

        materials[hatMaterialIndex] = hat;
        materials[bodyMaterialIndex] = body;
        materials[feetMaterialIndex] = feet;
        colorRenderer.sharedMaterials = materials;

        // 清除旧版本的颜色覆盖，否则橙色 PropertyBlock 会盖住新粉色材质。
        colorRenderer.SetPropertyBlock(null);
        colorRenderer.SetPropertyBlock(null, hatMaterialIndex);
        colorRenderer.SetPropertyBlock(null, bodyMaterialIndex);
        colorRenderer.SetPropertyBlock(null, feetMaterialIndex);
        return true;
    }

    static bool IsValidIndex(int index, int count)
    {
        return index >= 0 && index < count;
    }
}
