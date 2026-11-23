#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ScienceModeSetupEditor
{
    [MenuItem("Tools/BH-VR/Configure Science Mode")]
    public static void Configure()
    {
        CsvVisualizer visualizer = Resources.FindObjectsOfTypeAll<CsvVisualizer>()
            .FirstOrDefault(item => item.gameObject.scene.IsValid());
        if (visualizer == null)
        {
            EditorUtility.DisplayDialog("BH-VR", "CsvVisualizer was not found in the open scene.", "OK");
            return;
        }

        Undo.RecordObject(visualizer, "Configure Science Mode");
        if (visualizer.mainCanvas == null)
        {
            GameObject canvasObject = GameObject.Find("MainCanvas");
            if (canvasObject != null) visualizer.mainCanvas = canvasObject;
        }
        if (visualizer.mainCanvas == null)
        {
            EditorUtility.DisplayDialog("BH-VR", "MainCanvas was not found.", "OK");
            return;
        }

        Transform canvas = visualizer.mainCanvas.transform;
        Button scienceButton = FindChild(canvas, "ScienceModeButton")?.GetComponent<Button>();
        if (scienceButton != null) visualizer.scienceModeButton = scienceButton;

        GameObject panel = FindChild(canvas, "ScienceDataPanel")?.gameObject;
        if (panel == null)
        {
            panel = CreateUiObject("ScienceDataPanel", canvas);
            Undo.AddComponent<Image>(panel);
        }
        ConfigurePanel(panel);

        TextMeshProUGUI text = ConfigureScrollContent(panel);
        foreach (Text legacyText in panel.GetComponentsInChildren<Text>(true))
        {
            legacyText.enabled = false;
        }

        visualizer.scienceDataPanel = panel;
        visualizer.scienceDataText = null;
        visualizer.scienceDataTmpText = text;
        visualizer.scienceExchangeFile = "science_exchange_events.csv";
        visualizer.scienceClusterFile = "science_cluster_dynamics.csv";
        visualizer.playbackTimeIsNbody = true;
        visualizer.scienceEventTimeTolerance = 0.5f;
        panel.SetActive(false);

        EditorUtility.SetDirty(visualizer);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(visualizer.gameObject.scene);
        Selection.activeGameObject = panel;
        EditorUtility.DisplayDialog(
            "BH-VR",
            "Science Mode is configured. Save the scene and enter Play Mode.",
            "OK"
        );
    }

    static void ConfigurePanel(GameObject panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        if (rect == null) rect = Undo.AddComponent<RectTransform>(panel);
        rect.anchorMin = new Vector2(0.5f, 1.0f);
        rect.anchorMax = new Vector2(0.5f, 1.0f);
        rect.pivot = new Vector2(0.5f, 0.0f);
        rect.anchoredPosition = new Vector2(0f, 20f);
        rect.sizeDelta = new Vector2(760f, 300f);
        rect.SetAsLastSibling();

        Image background = panel.GetComponent<Image>();
        if (background == null) background = Undo.AddComponent<Image>(panel);
        background.color = new Color(0.025f, 0.035f, 0.055f, 0.94f);
        background.raycastTarget = true;
    }

    static TextMeshProUGUI ConfigureScrollContent(GameObject panel)
    {
        ScrollRect scroll = panel.GetComponent<ScrollRect>();
        if (scroll == null) scroll = Undo.AddComponent<ScrollRect>(panel);

        Transform viewportTransform = FindChild(panel.transform, "ScienceViewport");
        GameObject viewport = viewportTransform != null
            ? viewportTransform.gameObject
            : CreateUiObject("ScienceViewport", panel.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetStretch(viewportRect, 12f, 12f, 12f, 12f);
        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null) viewportImage = Undo.AddComponent<Image>(viewport);
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;
        if (viewport.GetComponent<RectMask2D>() == null) Undo.AddComponent<RectMask2D>(viewport);

        Transform contentTransform = FindChild(viewport.transform, "ScienceContent");
        GameObject content = contentTransform != null
            ? contentTransform.gameObject
            : CreateUiObject("ScienceContent", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = Undo.AddComponent<VerticalLayoutGroup>(content);
        layout.padding = new RectOffset(4, 4, 4, 8);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = Undo.AddComponent<ContentSizeFitter>(content);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform textTransform = FindChild(content.transform, "ScienceContentText");
        GameObject textObject = textTransform != null
            ? textTransform.gameObject
            : CreateUiObject("ScienceContentText", content.transform);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (text == null) text = Undo.AddComponent<TextMeshProUGUI>(textObject);
        text.fontSize = 16f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.raycastTarget = false;
        text.text = "<b>SCIENCE MODE</b>";

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;
        scroll.scrollSensitivity = 1f;
        scroll.verticalNormalizedPosition = 1f;
        return text;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(gameObject, "Configure Science Mode");
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform nested = FindChild(child, name);
            if (nested != null) return nested;
        }
        return null;
    }
}
#endif

