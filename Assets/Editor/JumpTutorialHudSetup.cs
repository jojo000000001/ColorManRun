using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit, opt-in authoring tool. Never runs automatically on import or Play.</summary>
public static class JumpTutorialHudSetup
{
    [MenuItem("Tools/ColorManRun/Install Keyboard Tutorial HUD")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("请先保存当前场景，再安装 HUD。");

        Scene original = SceneManager.GetActiveScene();
        InstallScene("Assets/Scenes/SampleScene.unity", true);
        InstallScene("Assets/Scenes/Level2.unity", false);
        SceneManager.SetActiveScene(original);
    }

    static void InstallScene(string path, bool bindTutorials)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).ToArray();
            Canvas canvas = components.OfType<Canvas>().First(c => c.renderMode == RenderMode.ScreenSpaceOverlay);
            Transform existingHints = canvas.transform.Find("KeyboardHints");
            if (existingHints != null)
            {
                if (!bindTutorials)
                {
                    existingHints.gameObject.SetActive(false);
                    Transform existingTutorial = canvas.transform.Find("JumpTutorial");
                    if (existingTutorial != null) existingTutorial.gameObject.SetActive(false);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                return;
            }
            PlayerController player = components.OfType<PlayerController>().Single();
            Collider first = null, second = null, pinkLanding = null;
            if (bindTutorials)
            {
                // Names are used ONLY during authoring. The scene stores actual object references.
                first = components.OfType<Collider>().Single(c => c.name == "TrackBeam_Black");
                second = components.OfType<Collider>().Single(c => c.name == "TrackBeam_Black Long");
                pinkLanding = components.OfType<Collider>().Single(c => c.name == "TrackBeam_Pink 1");
            }
            Sprite left = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ButtonLeft.png");
            Sprite up = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ButtonUp.png");
            if (left == null || up == null) throw new InvalidOperationException("按键图片未导入为 Sprite。");

            RectTransform root = Rect("KeyboardHints", canvas.transform, new Vector2(440, 260), new Vector2(-370, 225));
            root.anchorMin = root.anchorMax = new Vector2(1, 0);
            CanvasGroup rootGroup = root.gameObject.AddComponent<CanvasGroup>();
            rootGroup.interactable = rootGroup.blocksRaycasts = false;
            Image leftImage = Icon("ChangeColorKey", root, left, new Vector2(-110, 0));
            Image upImage = Icon("JumpKey", root, up, new Vector2(110, 0));
            Text leftLabel = Label("ChangeColorCaption", root, "变色", new Vector2(-110, -112), new Vector2(220, 72), 48);
            Text upLabel = Label("JumpCaption", root, "跳跃", new Vector2(110, -112), new Vector2(220, 72), 48);
            RectTransform tutorial = Rect("JumpTutorial", canvas.transform, new Vector2(680, 240), new Vector2(10, 160));
            tutorial.anchorMin = tutorial.anchorMax = new Vector2(0.5f, 0f);
            CanvasGroup tutorialGroup = tutorial.gameObject.AddComponent<CanvasGroup>();
            tutorialGroup.alpha = 0;
            tutorialGroup.interactable = tutorialGroup.blocksRaycasts = false;
            Text hint = Label("HintText", tutorial, "轻按 ↑ 跳跃", Vector2.zero, new Vector2(640, 150), 52);
            hint.color = Color.white;
            hint.supportRichText = true;
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.verticalOverflow = VerticalWrapMode.Overflow;
            if (!bindTutorials)
            {
                root.gameObject.SetActive(false);
                tutorial.gameObject.SetActive(false);
            }
            JumpTutorialHud hud = root.gameObject.AddComponent<JumpTutorialHud>();
            SerializedObject serialized = new SerializedObject(hud);
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("jumpIcon").objectReferenceValue = upImage.rectTransform;
            serialized.FindProperty("colorIcon").objectReferenceValue = leftImage.rectTransform;
            serialized.FindProperty("tutorialGroup").objectReferenceValue = tutorialGroup;
            serialized.FindProperty("tutorialLabel").objectReferenceValue = hint;
            SerializedProperty labels = serialized.FindProperty("chineseLabels");
            labels.arraySize = 3;
            labels.GetArrayElementAtIndex(0).objectReferenceValue = leftLabel;
            labels.GetArrayElementAtIndex(1).objectReferenceValue = upLabel;
            labels.GetArrayElementAtIndex(2).objectReferenceValue = hint;
            SerializedProperty points = serialized.FindProperty("tutorials");
            Collider[] transitions = bindTutorials ? new[]
            {
                components.OfType<Collider>().Single(c => c.name == "TrackBeam (6)"),
                components.OfType<Collider>().Single(c => c.name == "TrackBeam_Pink_Nomal"),
                components.OfType<Collider>().Single(c => c.name == "TrackBeam (7)")
            } : new Collider[0];
            points.arraySize = bindTutorials ? 3 + transitions.Length : 0;
            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                point.FindPropertyRelative("obstacle").objectReferenceValue = i == 0 ? first : i == 1 ? second : i == 2 ? pinkLanding : transitions[i - 3];
                point.FindPropertyRelative("hint").enumValueIndex = i < 3 ? i : (int)JumpTutorialHud.HintKind.JumpAndChangeColor;
                point.FindPropertyRelative("leadSeconds").floatValue = i == 2 ? 0f : 1.5f;
                point.FindPropertyRelative("endAfterFrontMeters").floatValue = i >= 3 ? 8f : 0f;
                point.FindPropertyRelative("slowMotion").boolValue = i == 2;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    static Image Icon(string name, Transform parent, Sprite sprite, Vector2 position)
    {
        Image image = Rect(name, parent, new Vector2(150, 150), position).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        Text text = Rect(name, parent, size, position).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = value;
        text.raycastTarget = false;
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }
}
