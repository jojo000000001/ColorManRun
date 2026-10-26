using UnityEditor;
using UnityEngine;

/// <summary>
/// 把奔跑尘土写进共享角色预制体，避免只在进 Play 时临时生成。
/// </summary>
[InitializeOnLoad]
static class RunDustSetup
{
    const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";

    static RunDustSetup()
    {
        EditorApplication.delayCall += InstallOnce;
    }

    static void InstallOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SessionState.GetBool("ColorManRun.RunDustInstalled", false))
            return;
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
            return;
        try
        {
            RunDust dust = root.GetComponent<RunDust>();
            bool created = dust == null;
            if (created)
                dust = root.AddComponent<RunDust>();

            Transform anchor = root.transform.Find("RunDust");
            bool missingSystem = anchor == null || anchor.GetComponent<ParticleSystem>() == null;
            if (anchor == null)
            {
                var go = new GameObject("RunDust");
                go.layer = 0;
                go.SetActive(false);
                anchor = go.transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(0f, -0.16f, -0.4f);
                anchor.localRotation = Quaternion.identity;
                anchor.localScale = Vector3.one;
            }
            if (anchor.GetComponent<ParticleSystem>() == null)
            {
                anchor.gameObject.SetActive(false);
                anchor.gameObject.AddComponent<ParticleSystem>();
            }

            var serialized = new SerializedObject(dust);
            SerializedProperty puff = serialized.FindProperty("puff");
            bool puffMissing = puff.objectReferenceValue == null;
            serialized.FindProperty("player").objectReferenceValue = root.GetComponent<PlayerController>();
            serialized.FindProperty("dust").objectReferenceValue = anchor.GetComponent<ParticleSystem>();
            if (puffMissing)
                puff.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Effects/RunDust.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (created || missingSystem || puffMissing)
            {
                dust.Configure();
                anchor.gameObject.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            SessionState.SetBool("ColorManRun.RunDustInstalled", true);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
