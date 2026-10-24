using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only checks for imported assets and required project layers.
/// This tool never creates, edits, or saves project assets.
/// </summary>
public static class AssetCheckTool
{
    private static readonly string[] RequiredAnimationNames =
    {
        "run",
        "jump_start",
        "jump_holding",
        "change_color"
    };

    [MenuItem("Tools/检查素材")]
    private static void CheckAssets()
    {
        Debug.Log("[素材检查] 开始检查 FBX 动画剪辑和项目 Layer。此检查只读，不会修改资源。");

        CheckFbxAnimationClips();
        CheckRequiredLayers();

        Debug.Log("[素材检查] 检查完成。");
    }

    private static void CheckFbxAnimationClips()
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model");
        int fbxCount = 0;

        Array.Sort(modelGuids, StringComparer.Ordinal);

        foreach (string guid in modelGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fbxCount++;
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            bool[] foundRequiredClip = new bool[RequiredAnimationNames.Length];
            int clipCount = 0;

            Debug.Log("[素材检查] FBX: " + assetPath);

            foreach (UnityEngine.Object subAsset in subAssets)
            {
                AnimationClip clip = subAsset as AnimationClip;
                if (clip == null)
                {
                    continue;
                }

                clipCount++;
                bool isRequired = false;
                for (int i = 0; i < RequiredAnimationNames.Length; i++)
                {
                    if (string.Equals(clip.name, RequiredAnimationNames[i], StringComparison.OrdinalIgnoreCase))
                    {
                        foundRequiredClip[i] = true;
                        isRequired = true;
                        break;
                    }
                }

                string loopState = clip.isLooping ? "Loop Time=已勾选" : "Loop Time=未勾选";
                Debug.Log("[素材检查]   AnimationClip: " + clip.name + "（" + loopState + "）", clip);

                if (!clip.isLooping)
                {
                    Debug.LogWarning(
                        "[素材检查]   提醒：" + clip.name + " 未勾选 Loop Time。请根据动作用途确认是否正确。",
                        clip);
                }
            }

            if (clipCount == 0)
            {
                Debug.LogWarning("[素材检查]   未找到 AnimationClip，请检查 FBX Import Settings 的 Animation 页。", AssetDatabase.LoadMainAssetAtPath(assetPath));
            }

            for (int i = 0; i < RequiredAnimationNames.Length; i++)
            {
                if (!foundRequiredClip[i])
                {
                    Debug.LogWarning(
                        "[素材检查]   缺少要求的 AnimationClip：" + RequiredAnimationNames[i] + "。",
                        AssetDatabase.LoadMainAssetAtPath(assetPath));
                }
            }
        }

        if (fbxCount == 0)
        {
            Debug.LogWarning("[素材检查] Project 中未找到 .fbx 文件。");
        }
        else
        {
            Debug.Log("[素材检查] 共找到 " + fbxCount + " 个 FBX。");
        }
    }

    private static void CheckRequiredLayers()
    {
        string[] requiredLayers = { "Ground", "MirrorGround", "Player" };

        foreach (string layerName in requiredLayers)
        {
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex < 0)
            {
                Debug.LogWarning("[素材检查] 缺少 Layer：" + layerName + "。请在 Project Settings > Tags and Layers 中创建。" );
            }
            else
            {
                Debug.Log("[素材检查] Layer 已存在：" + layerName + "（索引 " + layerIndex + "）。");
            }
        }
    }
}
