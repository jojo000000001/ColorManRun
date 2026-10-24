using UnityEngine;

/// <summary>
/// Hat, body, and feet materials. The prefab starts on the orange set.
/// Pink materials are stored for a later color switch.
/// </summary>
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
}
