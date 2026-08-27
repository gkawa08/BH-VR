using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Networking;

[System.Serializable]
public class BodyData
{
    public float time;
    public int id;
    public float mass;
    public Vector3 position;
    public Vector3 velocity;
}

public class InteractionEvent
{
    public string type;
    public float time;
    public int[] ids;
}

public class CsvVisualizer : MonoBehaviour
{
    [Header("Settings")]
    public string historyFile = "bh_history.csv";
    public string eventsFile = "bh_events.csv";
    public GameObject spherePrefab;
    public float scaleMultiplier = 0.1f;
    public float vectorScale = 1.0f;
    public float preRollSeconds = 2.0f; 
    public float sizeSliderMin = 0.001f;
    public float sizeSliderMax = 0.1f;
    public Transform xrOriginTransform;

    [Header("Velocity Arrow Settings")]
    [Tooltip("Show velocity arrows for black holes in the selected event.")]
    public bool showVelocityArrows = true;

    [Header("First Event Selection Size")]
    public bool reduceBhSizeOnFirstEventSelection = true;
    [Min(0.000001f)]
    public float firstEventScaleMultiplier = 0.01f;

    [Header("Event Selection View")]
    [Tooltip("Move the player toward the black holes involved whenever an event is selected.")]
    public bool movePlayerCloserOnEventSelection = true;
    [Min(0.1f)]
    [Tooltip("Distance from the headset to the center of the selected event after focusing.")]
    public float eventTargetViewDistance = 3.0f;
    [Tooltip("Move the control panel by the same amount so that it remains reachable after focusing.")]
    public bool moveMainCanvasWithPlayer = true;

    [Header("Initial Demo Layout")]
    [Range(0.1f, 30f)]
    public float initialStepsPerSecond = 1.0f;
    [Min(0.001f)]
    public float minimumStepsPerSecond = 0.1f;
    [Min(0.001f)]
    public float maximumStepsPerSecond = 30.0f;
    [Min(1.0f)]
    public float initialViewPadding = 1.35f;
    [Min(0.1f)]
    public float minimumInitialViewDistance = 4.0f;
    [Min(0.1f)]
    public float initialControlPanelDistance = 1.2f;
    public float initialControlPanelVerticalOffset = 0.0f;

    [Header("Scale Bar Settings")]
    public bool showScaleBar = true;
    [Min(0.01f)]
    public float scaleBarLength = 1.0f;
    public string scaleBarLabel = "1 pc";
    public Color scaleBarColor = Color.white;
    [Min(0.001f)]
    public float scaleBarLineWidth = 0.025f;
    [Min(0.01f)]
    public float scaleBarEndCapHeight = 0.2f;
    [Min(0.0f)]
    public float scaleBarMargin = 0.5f;
    [Min(0.0f)]
    public float scaleBarLabelOffset = 0.12f;
    [Min(0.001f)]
    public float scaleBarLabelCharacterSize = 0.03f;

    [Header("Colors")]
    public Color normalColor = Color.black;
    public Color binaryColor = Color.magenta;
    public Color interloperColor = Color.red;
    public Color postMergeColor = Color.cyan;
    public Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.05f);

    [Header("Label & Callout Settings")]
    public float labelFontSize = 100f;
    public float labelCharacterSize = 0.02f;
    public float labelHeightOffset = 1.0f;
    public float labelSpreadX = 1.5f;
    public float labelSpreadY = 0.5f;
    [Min(0.1f)]
    public float calloutDistanceMultiplier = 1.35f;
    public bool keepLabelScreenSize = true;
    [Min(0.01f)]
    public float labelReferenceDistance = 10.0f;
    [Min(0.01f)]
    public float minimumLabelDistanceScale = 0.5f;
    [Min(0.01f)]
    public float maximumLabelDistanceScale = 100.0f;
    public bool showCalloutLine = true;      // 【新規】線を表示するかどうか
    public float calloutLineWidth = 0.005f;  // 【新規】引き出し線の太さ（極細に設定）

    [Header("Orbit Settings")]
    public int orbitFutureFrames = 150; 
    public int orbitPastFrames = 50;    
    public float orbitLineWidth = 0.02f; 
    [Range(1, 20)]
    public int orbitSmoothSteps = 10;   

    [Header("HUD Settings")]
    public GameObject mainCanvas;
    public float hudFontSize = 150f;
    public float hudCharacterSize = 0.03f;
    public Vector3 hudPositionOffset = Vector3.zero;

    [Header("Science Mode Settings")]
    public bool startInScienceMode = false;
    public string solarMassUnit = "M☉";
    public string distanceUnit = "pc";
    public string velocityUnit = "km/s";
    public string timeUnit = "Myr";
    public bool showSciencePairLines = true;
    public float sciencePairLineWidth = 0.012f;
    public float scienceDistanceLabelOffset = 0.15f;
    [Min(0.0f)]
    public float scienceExchangeLabelSeparation = 0.35f;
    public float scienceDistanceLabelCharacterSize = 0.02f;
    public Color scienceBeforePairColor = Color.magenta;
    public Color scienceAfterPairColor = Color.cyan;
    public string scienceExchangeFile = "science_exchange_events.csv";
    public string scienceClusterFile = "science_cluster_dynamics.csv";
    public bool playbackTimeIsNbody = true;
    [Min(0.0001f)]
    public float scienceEventTimeTolerance = 0.05f;

    [Header("UI Linking")]
    public Slider timeSlider;
    public Slider speedSlider;
    public Slider sizeSlider;
    public Text timeLabel;
    public Button playButton;
    [Tooltip("Optional. Assign this when the Play button uses Legacy UI Text.")]
    public Text playButtonText;
    [Tooltip("Optional. Assign this when the Play button uses TextMeshPro.")]
    public TMP_Text playButtonTmpText;
    public Dropdown eventDropdown;
    public Button togglePosModeButton;
    public Button scienceModeButton;
    public GameObject scienceDataPanel;
    public Text scienceDataText;
    public TMP_Text scienceDataTmpText;

    [Header("Follow Mode UI")]
    public Button followModeButton;
    [Tooltip("Optional. Assign this when the Follow Mode button uses Legacy UI Text.")]
    public Text followModeButtonText;
    [Tooltip("Optional. Assign this when the Follow Mode button uses TextMeshPro.")]
    public TMP_Text followModeButtonTmpText;
    public Dropdown followBhDropdown;
    [Tooltip("Hide the followed black hole so that it does not block the headset view.")]
    public bool hideFollowTarget = true;

    [Header("Follow Background Motion")]
    [Tooltip("Optional background or star-field objects that should move in the followed black hole's relative frame.")]
    public Transform[] followRelativeBackgrounds = new Transform[0];
    [Min(0.0f)]
    public float followBackgroundMotionScale = 1.0f;
    [Tooltip("Rotate a compatible Skybox material slightly while following to add a visual sense of travel.")]
    public bool rotateSkyboxWhileFollowing = true;
    [Min(0.0f)]
    public float followSkyboxDegreesPerDistance = 0.1f;

    private Dictionary<float, List<BodyData>> timeDataMap = new Dictionary<float, List<BodyData>>();
    private List<float> sortedTimeKeys = new List<float>();
    private Dictionary<float, Vector3> centerOfMassMap = new Dictionary<float, Vector3>();
    private Dictionary<float, Vector3> centerOfMassVelocityMap = new Dictionary<float, Vector3>();
    private Dictionary<int, GameObject> activeObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> activeLabels = new Dictionary<int, GameObject>();
    private Dictionary<int, LineRenderer> activeOrbits = new Dictionary<int, LineRenderer>();
    private List<InteractionEvent> loadedEvents = new List<InteractionEvent>();
    private InteractionEvent currentSelectedEvent = null;
    
    private HashSet<int> validHistoryIds = new HashSet<int>();
    
    private bool isPlaying = false;
    private bool useCenterOfMass = true;
    private bool isScienceMode = false;
    private bool isFollowMode = false;
    private bool followBinaryCenterOfMass = false;
    private int followedBhId = -1;
    private const int FollowBinarySentinel = int.MinValue;
    private Vector3 followAnchorPosition = Vector3.zero;
    private readonly List<int> followDropdownIds = new List<int>();
    private readonly Dictionary<Transform, Vector3> followBackgroundOriginalPositions = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> followBackgroundSegmentPositions = new Dictionary<Transform, Vector3>();
    private Vector3 followBackgroundTargetAnchorPosition = Vector3.zero;
    private bool hasFollowBackgroundAnchor = false;
    private Material followOriginalSkyboxMaterial;
    private Material followRuntimeSkyboxMaterial;
    private Vector3 followSkyboxPreviousTargetPosition = Vector3.zero;
    private float followSkyboxRotation = 0.0f;
    private bool hasFollowSkyboxMotion = false;
    private bool hasAppliedFirstEventSize = false;
    private bool isHandlingEventSelection = false;
    private int lastHandledEventSelection = -1;
    private bool lastShowVelocityArrows;
    private float timer = 0f;
    private Camera mainCamera;

    private GameObject eventHudObj;
    private TextMesh eventHudText;
    private int currentEventIndex = -1;
    private bool hasError = false;
    private Vector3 resetXrOriginPosition = Vector3.zero;
    private Vector3 resetMainCanvasPosition = Vector3.zero;
    private Quaternion resetMainCanvasRotation = Quaternion.identity;
    private bool hasResetMainCanvasPose = false;
    private GameObject scaleBarRoot;
    private Material scaleBarMaterial;
    private readonly List<LineRenderer> sciencePairLines = new List<LineRenderer>();
    private readonly List<GameObject> scienceDistanceLabels = new List<GameObject>();
    private readonly ScienceModeDataStore scienceDataStore = new ScienceModeDataStore();
    
    [HideInInspector]
    public float stepsPerSecond = 1.0f; 

    IEnumerator Start()
    {
        lastShowVelocityArrows = showVelocityArrows;
        mainCamera = Camera.main;
        SetupHUD();

        string[] historyLines = null;
        yield return StartCoroutine(LoadOptionalDataFile(historyFile, lines => historyLines = lines));
        
        if (historyLines == null || historyLines.Length == 0)
        {
            if (!hasError) ShowError($"Data missing or unreadable:\n{historyFile}");
            yield break;
        }

        LoadHistoryData(historyLines);

        string[] eventLines = null;
        yield return StartCoroutine(LoadOptionalDataFile(eventsFile, lines => eventLines = lines));
        if (eventLines != null && eventLines.Length > 0)
        {
            LoadEventData(eventLines);
        }

        string[] scienceExchangeLines = null;
        yield return StartCoroutine(LoadOptionalDataFile(scienceExchangeFile, lines => scienceExchangeLines = lines));
        scienceDataStore.LoadExchange(scienceExchangeLines);

        string[] scienceClusterLines = null;
        yield return StartCoroutine(LoadOptionalDataFile(scienceClusterFile, lines => scienceClusterLines = lines));
        scienceDataStore.LoadCluster(scienceClusterLines);
        
        isScienceMode = startInScienceMode;
        SetupUI();
        SetupScienceWorldVisuals();

        if (timeLabel != null)
        {
            timeLabel.text = $"Loaded: {sortedTimeKeys.Count} frames";
        }

        UpdateVisuals(0);
        yield return null;
        ApplyInitialLayout();
        CreateScaleBar();
    }

    void ApplyInitialLayout()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        ResolveXrOriginTransform();

        if (xrOriginTransform != null && TryGetInitialBlackHoleBounds(out Bounds bounds))
        {
            float verticalHalfFov = Mathf.Max(1.0f, mainCamera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
            float cameraAspect = Mathf.Max(0.1f, mainCamera.aspect);
            float horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * cameraAspect);
            float limitingHalfFov = Mathf.Max(
                1.0f * Mathf.Deg2Rad,
                Mathf.Min(verticalHalfFov, horizontalHalfFov)
            );

            float populationRadius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float requiredDistance = populationRadius / Mathf.Sin(limitingHalfFov);
            requiredDistance = Mathf.Max(
                minimumInitialViewDistance,
                requiredDistance * initialViewPadding
            );

            Vector3 viewingDirection = mainCamera.transform.forward.normalized;
            if (viewingDirection.sqrMagnitude < 0.0001f)
            {
                viewingDirection = Vector3.forward;
            }

            Vector3 desiredCameraPosition = bounds.center - viewingDirection * requiredDistance;
            Vector3 rigTranslation = desiredCameraPosition - mainCamera.transform.position;
            xrOriginTransform.position += rigTranslation;
            resetXrOriginPosition = xrOriginTransform.position;
        }
        else if (xrOriginTransform != null)
        {
            resetXrOriginPosition = xrOriginTransform.position;
        }

        if (mainCanvas != null)
        {
            Vector3 panelForward = Vector3.ProjectOnPlane(
                mainCamera.transform.forward,
                Vector3.up
            ).normalized;

            if (panelForward.sqrMagnitude < 0.0001f)
            {
                panelForward = Vector3.forward;
            }

            Vector3 panelPosition =
                mainCamera.transform.position
                + panelForward * initialControlPanelDistance
                + Vector3.up * initialControlPanelVerticalOffset;

            Quaternion panelRotation = Quaternion.LookRotation(
                panelForward,
                Vector3.up
            );

            mainCanvas.transform.SetPositionAndRotation(
                panelPosition,
                panelRotation
            );

            resetMainCanvasPosition = mainCanvas.transform.position;
            resetMainCanvasRotation = mainCanvas.transform.rotation;
            hasResetMainCanvasPose = true;
        }
    }

    void ResolveXrOriginTransform()
    {
        if (xrOriginTransform != null || mainCamera == null) return;

        Transform candidate = mainCamera.transform;
        while (candidate != null)
        {
            string candidateName = candidate.name.ToLowerInvariant();
            if (candidateName.Contains("xr origin") || candidateName.Contains("xr rig"))
            {
                xrOriginTransform = candidate;
                return;
            }
            candidate = candidate.parent;
        }

        if (mainCamera.transform.root != mainCamera.transform)
        {
            xrOriginTransform = mainCamera.transform.root;
        }
    }

    bool TryGetInitialBlackHoleBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        foreach (GameObject obj in activeObjects.Values)
        {
            if (obj == null || !obj.activeInHierarchy) continue;

            Renderer renderer = obj.GetComponent<Renderer>();
            Bounds objectBounds = renderer != null
                ? renderer.bounds
                : new Bounds(obj.transform.position, Vector3.zero);

            if (!hasBounds)
            {
                bounds = objectBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(objectBounds);
            }
        }

        return hasBounds;
    }

    void CreateScaleBar()
    {
        if (!showScaleBar || scaleBarRoot != null || mainCamera == null) return;
        if (!TryGetInitialBlackHoleBounds(out Bounds bounds)) return;

        scaleBarRoot = new GameObject("ScaleBar_1pc");
        scaleBarRoot.transform.SetParent(transform, true);

        Shader scaleBarShader = Shader.Find("Sprites/Default");
        scaleBarMaterial = new Material(scaleBarShader);

        float halfLength = scaleBarLength * 0.5f;
        float halfCapHeight = scaleBarEndCapHeight * 0.5f;

        CreateScaleBarLine(
            "ScaleBar_Main",
            new Vector3(-halfLength, 0f, 0f),
            new Vector3(halfLength, 0f, 0f)
        );
        CreateScaleBarLine(
            "ScaleBar_LeftCap",
            new Vector3(-halfLength, -halfCapHeight, 0f),
            new Vector3(-halfLength, halfCapHeight, 0f)
        );
        CreateScaleBarLine(
            "ScaleBar_RightCap",
            new Vector3(halfLength, -halfCapHeight, 0f),
            new Vector3(halfLength, halfCapHeight, 0f)
        );

        GameObject labelObject = new GameObject("ScaleBar_Label");
        labelObject.transform.SetParent(scaleBarRoot.transform, false);
        labelObject.transform.localPosition = new Vector3(
            0f,
            -halfCapHeight - scaleBarLabelOffset,
            0f
        );

        TextMesh labelText = labelObject.AddComponent<TextMesh>();
        labelText.text = scaleBarLabel;
        labelText.anchor = TextAnchor.UpperCenter;
        labelText.alignment = TextAlignment.Center;
        labelText.fontSize = 100;
        labelText.characterSize = scaleBarLabelCharacterSize;
        labelText.color = scaleBarColor;

        PositionScaleBar(bounds);
    }

    void CreateScaleBarLine(string objectName, Vector3 start, Vector3 end)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(scaleBarRoot.transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = scaleBarLineWidth;
        line.endWidth = scaleBarLineWidth;
        line.startColor = scaleBarColor;
        line.endColor = scaleBarColor;
        line.material = scaleBarMaterial;
    }

    void PositionScaleBar(Bounds bounds)
    {
        if (scaleBarRoot == null || mainCamera == null) return;

        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 cameraRight = mainCamera.transform.right.normalized;
        Vector3 cameraUp = mainCamera.transform.up.normalized;
        Vector3 cameraForward = mainCamera.transform.forward.normalized;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        float minRight = float.PositiveInfinity;
        float maxRight = float.NegativeInfinity;
        float minUp = float.PositiveInfinity;

        foreach (Vector3 corner in corners)
        {
            Vector3 relativeCorner = corner - cameraPosition;
            float rightProjection = Vector3.Dot(relativeCorner, cameraRight);
            float upProjection = Vector3.Dot(relativeCorner, cameraUp);
            minRight = Mathf.Min(minRight, rightProjection);
            maxRight = Mathf.Max(maxRight, rightProjection);
            minUp = Mathf.Min(minUp, upProjection);
        }

        float centerRight = (minRight + maxRight) * 0.5f;
        float centerDepth = Vector3.Dot(
            bounds.center - cameraPosition,
            cameraForward
        );
        float centerUp = minUp - scaleBarMargin - scaleBarEndCapHeight * 0.5f;

        scaleBarRoot.transform.position =
            cameraPosition
            + cameraRight * centerRight
            + cameraUp * centerUp
            + cameraForward * centerDepth;

        UpdateScaleBarFacingCamera();
    }

    void UpdateScaleBarFacingCamera()
    {
        if (scaleBarRoot == null || mainCamera == null) return;

        Vector3 lookDirection =
            scaleBarRoot.transform.position - mainCamera.transform.position;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            scaleBarRoot.transform.rotation = Quaternion.LookRotation(
                lookDirection,
                mainCamera.transform.up
            );
        }
    }

    IEnumerator LoadFileSafe(string path, System.Action<string[]> onLoaded)
    {
        List<string> linesList = new List<string>();
        try
        {
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    linesList.Add(line);
                }
            }
            onLoaded?.Invoke(linesList.ToArray());
        }
        catch (System.Exception e)
        {
            ShowError($"Read Error:\n{e.Message}");
            onLoaded?.Invoke(null);
        }
        yield return null;
    }

    IEnumerator LoadOptionalDataFile(string fileName, System.Action<string[]> onLoaded)
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName);
        if (File.Exists(persistentPath))
        {
            yield return StartCoroutine(LoadFileSafe(persistentPath, onLoaded));
            yield break;
        }

        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!streamingPath.Contains("://") && File.Exists(streamingPath))
        {
            yield return StartCoroutine(LoadFileSafe(streamingPath, onLoaded));
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(streamingPath))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string text = request.downloadHandler.text;
                onLoaded?.Invoke(text.Replace("\r\n", "\n").Split('\n'));
            }
            else
            {
                onLoaded?.Invoke(null);
            }
        }
    }

    void SetupHUD()
    {
        if (eventHudObj == null)
        {
            eventHudObj = new GameObject("EventHUD");
            eventHudObj.transform.SetParent(transform);
            eventHudText = eventHudObj.AddComponent<TextMesh>();
            eventHudText.anchor = TextAnchor.MiddleCenter;
            eventHudText.alignment = TextAlignment.Center;
            eventHudText.fontSize = (int)hudFontSize;
            eventHudText.characterSize = hudCharacterSize;
            eventHudObj.SetActive(false);
        }
    }

    void SetupScienceWorldVisuals()
    {
        if (sciencePairLines.Count > 0) return;

        for (int i = 0; i < 2; i++)
        {
            GameObject lineObject = new GameObject(i == 0 ? "SciencePair_Before" : "SciencePair_After");
            lineObject.transform.SetParent(transform, true);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = sciencePairLineWidth;
            line.endWidth = sciencePairLineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.enabled = false;
            sciencePairLines.Add(line);

            GameObject labelObject = new GameObject(i == 0 ? "ScienceDistance_Before" : "ScienceDistance_After");
            labelObject.transform.SetParent(transform, true);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = (int)labelFontSize;
            label.characterSize = scienceDistanceLabelCharacterSize;
            label.color = i == 0 ? scienceBeforePairColor : scienceAfterPairColor;
            labelObject.SetActive(false);
            scienceDistanceLabels.Add(labelObject);
        }

        UpdateScienceUIState();
    }

    void ResolveScienceUIReferences()
    {
        if (mainCanvas == null) return;

        if (scienceModeButton == null)
        {
            Button[] buttons = mainCanvas.GetComponentsInChildren<Button>(true);
            scienceModeButton = buttons.FirstOrDefault(button =>
                button.name == "ScienceModeButton" || button.name == "Science Mode Button");
        }

        if (scienceDataPanel == null)
        {
            Transform panelTransform = FindChildRecursive(mainCanvas.transform, "ScienceDataPanel");
            if (panelTransform != null) scienceDataPanel = panelTransform.gameObject;
        }

        if (scienceDataText == null && scienceDataPanel != null)
        {
            scienceDataText = scienceDataPanel.GetComponentInChildren<Text>(true);
        }

        if (scienceDataTmpText == null && scienceDataPanel != null)
        {
            scienceDataTmpText = scienceDataPanel.GetComponentInChildren<TMP_Text>(true);
        }
    }

    Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null) return nestedChild;
        }
        return null;
    }

    void ShowError(string msg)
    {
        hasError = true;
        if (eventHudObj != null)
        {
            eventHudObj.SetActive(true);
            if (mainCamera != null) {
                eventHudObj.transform.position = mainCamera.transform.position + mainCamera.transform.forward * 2.0f;
                eventHudObj.transform.rotation = Quaternion.LookRotation(eventHudObj.transform.position - mainCamera.transform.position);
            } else {
                eventHudObj.transform.position = new Vector3(0, 1.5f, 2.0f);
            }
            eventHudText.text = msg;
            eventHudText.color = Color.red;
            eventHudText.characterSize = 0.05f; 
        }
    }

    void Update()
    {
        if (hasError || sortedTimeKeys.Count == 0 || timeSlider == null) return;
        if (mainCamera == null) mainCamera = Camera.main;

        if (lastShowVelocityArrows != showVelocityArrows)
        {
            lastShowVelocityArrows = showVelocityArrows;
            UpdateVisuals((int)timeSlider.value);
        }

        float fraction = 0f;

        if (isPlaying)
        {
            float stepDuration = 1.0f / stepsPerSecond;
            timer += Time.deltaTime;
            
            while (timer >= stepDuration)
            {
                timer -= stepDuration;
                float nextValue = timeSlider.value + 1;
                if (nextValue > timeSlider.maxValue) nextValue = 0;
                timeSlider.value = nextValue;
            }
            fraction = timer / stepDuration;
        }

        int currentIndex = (int)timeSlider.value;
        int nextIndex = Mathf.Min(currentIndex + 1, sortedTimeKeys.Count - 1);

        float currentTime = sortedTimeKeys[currentIndex];
        float nextTime = sortedTimeKeys[nextIndex];

        List<BodyData> currentData = timeDataMap[currentTime];
        List<BodyData> nextData = timeDataMap[nextTime];

        Dictionary<int, BodyData> nextDataDict = new Dictionary<int, BodyData>();
        foreach(var d in nextData) nextDataDict[d.id] = d;

        Vector3 currentReference = GetDisplayedPositionReference(currentTime, Vector3.zero);
        Vector3 nextReference = GetDisplayedPositionReference(nextTime, currentReference);
        Vector3 currentVelocityReference = GetDisplayedVelocityReference(currentTime, Vector3.zero);
        Vector3 nextVelocityReference = GetDisplayedVelocityReference(nextTime, currentVelocityReference);

        UpdateFollowBackgroundMotion(currentTime, nextTime, fraction);

        // --- 1. BHオブジェクトの移動 ---
        foreach (var cData in currentData)
        {
            if (activeObjects.TryGetValue(cData.id, out GameObject obj))
            {
                if (!obj.activeSelf) continue;

                Vector3 startPos = cData.position - currentReference;
                float startScale = cData.mass * scaleMultiplier;
                Vector3 startVel = cData.velocity - currentVelocityReference;

                Vector3 endPos = startPos;
                float endScale = startScale;
                Vector3 endVel = startVel;

                if (nextDataDict.TryGetValue(cData.id, out BodyData nData))
                {
                    endPos = nData.position - nextReference;
                    endScale = nData.mass * scaleMultiplier;
                    endVel = nData.velocity - nextVelocityReference;
                }

                obj.transform.position = Vector3.Lerp(startPos, endPos, fraction);
                obj.transform.localScale = Vector3.Lerp(Vector3.one * startScale, Vector3.one * endScale, fraction);

                LineRenderer lr = obj.GetComponent<LineRenderer>();
                if (showVelocityArrows && lr != null && lr.enabled && lr.positionCount == 2)
                {
                    Vector3 interpVel = Vector3.Lerp(startVel, endVel, fraction);
                    lr.SetPosition(0, obj.transform.position);
                    lr.SetPosition(1, obj.transform.position + interpVel * vectorScale);
                }

                if (activeLabels.TryGetValue(cData.id, out GameObject labelObj))
                {
                    if (labelObj.activeSelf && currentSelectedEvent != null)
                    {
                        int eventIndex = ArrayUtility.IndexOf(currentSelectedEvent.ids, cData.id);
                        int totalIds = currentSelectedEvent.ids.Length;

                        if (eventIndex != -1)
                        {
                            Vector3 camRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;
                            Vector3 camUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;

                            float midSize = (sizeSliderMin + sizeSliderMax) / 2f;
                            float sizeRatio = scaleMultiplier / midSize;

                            float distanceMultiplier = Mathf.Max(0.1f, calloutDistanceMultiplier);
                            float currentSpreadX = labelSpreadX * sizeRatio * distanceMultiplier;
                            float currentSpreadY = labelSpreadY * sizeRatio * distanceMultiplier;
                            float currentHeightOffset = labelHeightOffset * sizeRatio * distanceMultiplier;

                            float xOffset = (eventIndex - (totalIds - 1) / 2.0f) * currentSpreadX;
                            float yOffset = currentHeightOffset + (eventIndex % 2 == 0 ? 0 : currentSpreadY);

                            Vector3 basePos = obj.transform.position;
                            Vector3 targetLabelPos = basePos + camUp * (obj.transform.localScale.y * 0.5f + yOffset) + camRight * xOffset;

                            labelObj.transform.position = targetLabelPos;

                            if (mainCamera != null)
                            {
                                Vector3 lookDir = labelObj.transform.position - mainCamera.transform.position;
                                if (lookDir != Vector3.zero) labelObj.transform.rotation = Quaternion.LookRotation(lookDir);
                            }

                            TextMesh tm = labelObj.GetComponent<TextMesh>();
                            float distanceScale = GetLabelDistanceScale(targetLabelPos);
                            if (tm != null)
                            {
                                tm.characterSize = labelCharacterSize * distanceScale;
                            }

                            LineRenderer calloutLr = labelObj.GetComponent<LineRenderer>();
                            if (calloutLr != null && showCalloutLine)
                            {
                                calloutLr.enabled = true;
                                float currentCalloutWidth = calloutLineWidth * vectorScale * distanceScale;
                                calloutLr.startWidth = currentCalloutWidth;
                                calloutLr.endWidth = currentCalloutWidth;
                                calloutLr.SetPosition(0, basePos);
                                calloutLr.SetPosition(1, targetLabelPos);
                            }
                            else if (calloutLr != null)
                            {
                                calloutLr.enabled = false;
                            }
                        }
                    }
                }
            }
        }

        UpdateScienceDisplay(currentTime, currentData);

        // --- 2. 軌道（Orbit）の動的更新 ---
        if (currentSelectedEvent != null)
        {
            foreach (var kvp in activeOrbits)
            {
                int eid = kvp.Key;
                LineRenderer orbitLr = kvp.Value;

                if (orbitLr.enabled)
                {
                    List<Vector3> rawPositions = new List<Vector3>();
                    int startIdx = Mathf.Max(0, currentIndex - orbitPastFrames);
                    int endIdx = Mathf.Min(sortedTimeKeys.Count - 1, currentIndex + orbitFutureFrames);

                    for (int i = startIdx; i <= endIdx; i++)
                    {
                        float t = sortedTimeKeys[i];
                        BodyData bData = timeDataMap[t].FirstOrDefault(b => b.id == eid);
                        if (bData != null)
                        {
                            if (TryGetDisplayedPositionReference(t, out Vector3 reference))
                            {
                                Vector3 localPos = bData.position - reference;
                                rawPositions.Add(transform.TransformPoint(localPos));
                            }
                        }
                    }

                    List<Vector3> smoothedPositions = SmoothCurve(rawPositions, orbitSmoothSteps);

                    orbitLr.positionCount = smoothedPositions.Count;
                    if (smoothedPositions.Count > 0)
                    {
                        orbitLr.SetPositions(smoothedPositions.ToArray());
                    }
                }
            }
        }

        // --- 3. イベントHUDの更新 ---
        if (mainCanvas != null && !mainCanvas.activeSelf && currentSelectedEvent != null)
        {
            if (!eventHudObj.activeSelf) eventHudObj.SetActive(true);

            eventHudObj.transform.position = mainCanvas.transform.position + mainCanvas.transform.TransformDirection(hudPositionOffset);
            
            if (mainCamera != null)
            {
                Vector3 lookDir = eventHudObj.transform.position - mainCamera.transform.position;
                if (lookDir != Vector3.zero)
                {
                    eventHudObj.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
            else
            {
                eventHudObj.transform.rotation = mainCanvas.transform.rotation;
            }

            int stepsLeft = currentEventIndex - currentIndex;

            if (stepsLeft > 0)
            {
                float secondsLeft = stepsLeft / stepsPerSecond;
                eventHudText.text = $"{currentSelectedEvent.type} IN\n{secondsLeft:F1} sec";
                eventHudText.color = Color.yellow;
            }
            else
            {
                eventHudText.text = $"{currentSelectedEvent.type}\nOCCURRED!";
                eventHudText.color = postMergeColor;
            }
        }
        else
        {
            if (eventHudObj != null && eventHudObj.activeSelf) eventHudObj.SetActive(false);
        }

        UpdateScaleBarFacingCamera();
    }

    void UpdateScienceDisplay(float currentTime, List<BodyData> currentData)
    {
        if (!isScienceMode || currentSelectedEvent == null)
        {
            HideScienceWorldVisuals();
            return;
        }

        if (sciencePairLines.Count == 0)
        {
            SetupScienceWorldVisuals();
        }

        Dictionary<int, BodyData> bodyById = new Dictionary<int, BodyData>();
        foreach (BodyData body in currentData)
        {
            bodyById[body.id] = body;
        }

        for (int i = 0; i < sciencePairLines.Count; i++)
        {
            SetSciencePairVisible(i, false);
        }

        if (currentSelectedEvent.ids.Length >= 2)
        {
            string firstPairName = currentSelectedEvent.type == "EXCHANGE" ? "Before pair" : "Pair";
            UpdateSciencePairVisual(
                0,
                currentSelectedEvent.ids[0],
                currentSelectedEvent.ids[1],
                firstPairName,
                scienceBeforePairColor,
                bodyById
            );
        }

        if (currentSelectedEvent.type == "EXCHANGE" && currentSelectedEvent.ids.Length >= 4)
        {
            UpdateSciencePairVisual(
                1,
                currentSelectedEvent.ids[2],
                currentSelectedEvent.ids[3],
                "After pair",
                scienceAfterPairColor,
                bodyById
            );
        }

        string scienceText = BuildScienceDataText(currentTime, bodyById);
        if (scienceDataText != null) scienceDataText.text = scienceText;
        if (scienceDataTmpText != null) scienceDataTmpText.text = scienceText;
    }

    void UpdateSciencePairVisual(
        int visualIndex,
        int idA,
        int idB,
        string pairName,
        Color pairColor,
        Dictionary<int, BodyData> bodyById)
    {
        if (!showSciencePairLines || visualIndex < 0 || visualIndex >= sciencePairLines.Count)
        {
            SetSciencePairVisible(visualIndex, false);
            return;
        }

        if (!bodyById.TryGetValue(idA, out BodyData bodyA) ||
            !bodyById.TryGetValue(idB, out BodyData bodyB) ||
            !activeObjects.TryGetValue(idA, out GameObject objectA) ||
            !activeObjects.TryGetValue(idB, out GameObject objectB) ||
            !objectA.activeSelf || !objectB.activeSelf)
        {
            SetSciencePairVisible(visualIndex, false);
            return;
        }

        Vector3 positionA = objectA.transform.position;
        Vector3 positionB = objectB.transform.position;
        float separation = Vector3.Distance(positionA, positionB);

        LineRenderer line = sciencePairLines[visualIndex];
        line.enabled = true;
        line.positionCount = 2;
        line.startWidth = sciencePairLineWidth;
        line.endWidth = sciencePairLineWidth;
        line.startColor = pairColor;
        line.endColor = pairColor;
        line.SetPosition(0, positionA);
        line.SetPosition(1, positionB);

        GameObject labelObject = scienceDistanceLabels[visualIndex];
        TextMesh label = labelObject.GetComponent<TextMesh>();
        Vector3 labelPosition = (positionA + positionB) * 0.5f;
        float labelDistanceScale = GetLabelDistanceScale(labelPosition);
        float exchangeLabelOffset = 0.0f;
        if (currentSelectedEvent != null && currentSelectedEvent.type == "EXCHANGE")
        {
            exchangeLabelOffset = (visualIndex == 0 ? 0.5f : -0.5f)
                * scienceExchangeLabelSeparation
                * labelDistanceScale;
        }

        if (mainCamera != null)
        {
            labelPosition += mainCamera.transform.up * (scienceDistanceLabelOffset + exchangeLabelOffset);
        }
        else
        {
            labelPosition += Vector3.up * (scienceDistanceLabelOffset + exchangeLabelOffset);
        }

        labelObject.transform.position = labelPosition;
        if (mainCamera != null)
        {
            Vector3 lookDirection = labelPosition - mainCamera.transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                labelObject.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        label.text = $"{pairName}: r = {separation:F3} {distanceUnit}";
        label.color = pairColor;
        label.characterSize = scienceDistanceLabelCharacterSize * GetLabelDistanceScale(labelPosition);
        labelObject.SetActive(true);
    }

    void SetSciencePairVisible(int visualIndex, bool isVisible)
    {
        if (visualIndex < 0) return;
        if (visualIndex < sciencePairLines.Count)
        {
            sciencePairLines[visualIndex].enabled = isVisible;
        }
        if (visualIndex < scienceDistanceLabels.Count)
        {
            scienceDistanceLabels[visualIndex].SetActive(isVisible);
        }
    }

    void HideScienceWorldVisuals()
    {
        for (int i = 0; i < sciencePairLines.Count; i++)
        {
            SetSciencePairVisible(i, false);
        }
    }

    double GetDisplayTimeMyr(double playbackTime)
    {
        if (!playbackTimeIsNbody) return playbackTime;
        double? converted = scienceDataStore.ConvertNbodyToMyr(playbackTime);
        return converted ?? playbackTime;
    }

    string BuildScienceDataText(float currentTime, Dictionary<int, BodyData> bodyById)
    {
        if (currentSelectedEvent.type == "EXCHANGE")
        {
            ScienceExchangeRecord exchange = scienceDataStore.FindExchange(
                currentSelectedEvent,
                scienceEventTimeTolerance
            );
            if (exchange != null)
            {
                return BuildExchangeScienceDataText(currentTime, bodyById, exchange);
            }
        }

        List<string> lines = new List<string>();
        double eventTimeMyr = GetDisplayTimeMyr(currentSelectedEvent.time);
        double currentTimeMyr = GetDisplayTimeMyr(currentTime);
        double deltaTimeMyr = currentTimeMyr - eventTimeMyr;
        string frameName = isFollowMode
            ? (followBinaryCenterOfMass ? "Follow binary CoM" : $"Follow BH {followedBhId}")
            : (useCenterOfMass ? "Cluster CoM" : "Raw");

        lines.Add("SCIENCE MODE");
        lines.Add($"{currentSelectedEvent.type} event");
        if (currentSelectedEvent.type == "EXCHANGE")
        {
            string reason = scienceDataStore.ExchangeCount == 0
                ? "Detailed event physics could not be loaded"
                : "Detailed pre/post physics unavailable: a participant is missing from the nearest snapshot";
            lines.Add($"<color=#FFB86C>{reason}</color>");
        }
        lines.Add($"Event time: {eventTimeMyr:F3} Myr");
        lines.Add($"Time: {currentTimeMyr:F3} Myr");
        lines.Add($"Delta t: {deltaTimeMyr:+0.000;-0.000;0.000} Myr");
        lines.Add($"Frame: {frameName}");
        lines.Add("");
        lines.Add("Black holes");

        HashSet<int> displayedIds = new HashSet<int>();
        foreach (int id in currentSelectedEvent.ids)
        {
            if (!displayedIds.Add(id)) continue;

            if (bodyById.TryGetValue(id, out BodyData body))
            {
                float speed = GetDisplayedVelocity(body, currentTime).magnitude;
                lines.Add($"ID {id}: M = {body.mass:F2} {solarMassUnit}, |v| = {speed:F3} {velocityUnit}");
            }
            else
            {
                lines.Add($"ID {id}: not present in this snapshot");
            }
        }

        if (currentSelectedEvent.ids.Length >= 2)
        {
            lines.Add("");
            string firstPairName = currentSelectedEvent.type == "EXCHANGE" ? "Before pair" : "Pair";
            AppendPairScienceData(
                lines,
                firstPairName,
                currentSelectedEvent.ids[0],
                currentSelectedEvent.ids[1],
                currentTime,
                bodyById
            );
        }

        if (currentSelectedEvent.type == "EXCHANGE" && currentSelectedEvent.ids.Length >= 4)
        {
            lines.Add("");
            AppendPairScienceData(
                lines,
                "After pair",
                currentSelectedEvent.ids[2],
                currentSelectedEvent.ids[3],
                currentTime,
                bodyById
            );
        }

        return string.Join("\n", lines);
    }

    string BuildExchangeScienceDataText(
        float currentTime,
        Dictionary<int, BodyData> bodyById,
        ScienceExchangeRecord exchange)
    {
        List<string> lines = new List<string>();
        ClusterScienceRecord cluster = scienceDataStore.FindNearestCluster(
            currentTime,
            playbackTimeIsNbody
        );
        bool isBefore = playbackTimeIsNbody
            ? currentTime < exchange.EventTimeNbody
            : currentTime < exchange.EventTimeMyr;
        string viewFrame = isFollowMode
            ? (followBinaryCenterOfMass ? "Follow binary CoM" : $"Follow BH {followedBhId}")
            : (useCenterOfMass ? "Cluster CoM" : "Raw");

        lines.Add($"<b><color=#67D5FF>SCIENCE MODE — EXCHANGE #{exchange.ExchangeIndex}</color></b>");
        lines.Add($"Event: t = {exchange.EventTimeMyr:F3} Myr");
        if (cluster != null)
        {
            lines.Add($"Time: {cluster.TimeMyr:F3} Myr");
        }
        else
        {
            lines.Add($"Time: {GetDisplayTimeMyr(currentTime):F3} Myr");
        }
        lines.Add($"View frame: {viewFrame}; reported speeds: cluster CoM");
        lines.Add("");

        lines.Add($"<b><color=#FFF176>LIVE SNAPSHOT — {(isBefore ? "PRE" : "POST")}</color></b>");
        if (cluster == null)
        {
            lines.Add("Cluster science data unavailable");
        }
        else if (!cluster.QualityOk)
        {
            lines.Add($"<color=#FF6B6B>Cluster dynamics unavailable: {cluster.QualityReason}</color>");
        }
        else
        {
            lines.Add($"σ1D = {FormatNumber(cluster.Sigma1dKms, "F3")} {velocityUnit}; vrms,3D = {FormatNumber(cluster.Vrms3dKms, "F3")} {velocityUnit}");
            lines.Add($"Cluster VCoM = ({cluster.CenterVelocityKms.x:F3}, {cluster.CenterVelocityKms.y:F3}, {cluster.CenterVelocityKms.z:F3}) {velocityUnit}");
            AppendLiveExchangeKinematics(lines, bodyById, cluster, exchange, isBefore);
        }
        lines.Add("");

        lines.Add("<b><color=#FF8CE8>EVENT MEASUREMENTS — FIXED</color></b>");
        lines.Add($"Before: ID {exchange.BeforeId1} + {exchange.BeforeId2}; incoming ID {exchange.IncomingId}");
        lines.Add($"  masses: {FormatNumber(exchange.BeforeMass1, "F2")} + {FormatNumber(exchange.BeforeMass2, "F2")}; incoming {FormatNumber(exchange.IncomingMass, "F2")} {solarMassUnit}");
        lines.Add($"  snapshot @ {FormatNumber(exchange.BeforeSnapshotTimeMyr, "F3")} Myr; σ1D = {FormatNumber(exchange.SigmaBefore, "F3")} {velocityUnit}");
        lines.Add($"  binary: vcl = {FormatNumber(exchange.BinarySpeedBefore, "F3")}, vesc = {FormatNumber(exchange.BinaryEscapeBefore, "F3")} {velocityUnit}");
        lines.Add($"  incoming: vcl = {FormatNumber(exchange.SingleSpeedBefore, "F3")}, vesc = {FormatNumber(exchange.SingleEscapeBefore, "F3")} {velocityUnit}");
        lines.Add($"After: ID {exchange.AfterId1} + {exchange.AfterId2}; outgoing ID {exchange.OutgoingId}");
        lines.Add($"  masses: {FormatNumber(exchange.AfterMass1, "F2")} + {FormatNumber(exchange.AfterMass2, "F2")}; outgoing {FormatNumber(exchange.OutgoingMass, "F2")} {solarMassUnit}");
        lines.Add($"  snapshot @ {FormatNumber(exchange.AfterSnapshotTimeMyr, "F3")} Myr; σ1D = {FormatNumber(exchange.SigmaAfter, "F3")} {velocityUnit}");
        lines.Add($"  binary: vcl = {FormatNumber(exchange.BinarySpeedAfter, "F3")}, vesc = {FormatNumber(exchange.BinaryEscapeAfter, "F3")}, v/σ = {FormatNumber(exchange.BinarySigmaRatioAfter, "F2")}, v/vesc = {FormatEscapeRatio(exchange.BinaryEscapeRatioAfter)}");
        lines.Add($"  outgoing: vcl = {FormatNumber(exchange.SingleSpeedAfter, "F3")}, vesc = {FormatNumber(exchange.SingleEscapeAfter, "F3")}, v/σ = {FormatNumber(exchange.SingleSigmaRatioAfter, "F2")}, v/vesc = {FormatEscapeRatio(exchange.SingleEscapeRatioAfter)}");
        lines.Add("");

        lines.Add("<b>Binary orbit and binding energy</b>");
        lines.Add($"a: {FormatNumber(exchange.SemimajorAxisBeforeAu, "F3")} → {FormatNumber(exchange.SemimajorAxisAfterAu, "F3")} AU");
        lines.Add($"e: {FormatNumber(exchange.EccentricityBefore, "F3")} → {FormatNumber(exchange.EccentricityAfter, "F3")}");
        lines.Add($"|Eb|: {FormatScientific(exchange.BindingEnergyBefore)} → {FormatScientific(exchange.BindingEnergyAfter)} M☉ (km/s)²");
        lines.Add($"ΔEb = {FormatSignedScientific(exchange.BindingEnergyRelease)} M☉ (km/s)²");

        if (exchange.BindingEnergyReleasePositive &&
            exchange.PredictedBinaryRecoil.HasValue &&
            exchange.PredictedOutgoingRecoil.HasValue)
        {
            lines.Add("<b>Simple recoil model — final triple CoM</b>");
            lines.Add($"  predicted binary / outgoing = {exchange.PredictedBinaryRecoil.Value:F3} / {exchange.PredictedOutgoingRecoil.Value:F3} {velocityUnit}");
            lines.Add($"  measured binary / outgoing = {FormatNumber(exchange.MeasuredBinaryRecoil, "F3")} / {FormatNumber(exchange.MeasuredOutgoingRecoil, "F3")} {velocityUnit}");
            lines.Add($"  measured/predicted = {FormatNumber(exchange.MeasuredPredictedBinaryRatio, "F2")} / {FormatNumber(exchange.MeasuredPredictedOutgoingRatio, "F2")}");
            lines.Add($"  ΔK(measured) / ΔEb = {FormatNumber(exchange.KineticBindingRatio, "F3")}");
        }
        else
        {
            lines.Add("<color=#FFB86C>Simple recoil model: N/A because ΔEb ≤ 0</color>");
        }

        if (exchange.ChainBinaryVelocity.HasValue || exchange.ChainEscapeVelocity.HasValue)
        {
            lines.Add("<b>NBODY6 chain log</b>");
            if (exchange.ChainBinaryVelocity.HasValue)
            {
                lines.Add($"  CHAIN BINARY VCM = {exchange.ChainBinaryVelocity.Value:F3} {velocityUnit}");
            }
            if (exchange.ChainEscapeVelocity.HasValue)
            {
                lines.Add($"  CHAIN ESCAPE VF = {exchange.ChainEscapeVelocity.Value:F3} {velocityUnit} (chain frame/epoch)");
            }
        }

        lines.Add("<b>Prompt cluster escape ≤ 22.7 Myr</b>");
        lines.Add($"  binary: {FormatEscapeStatus(exchange.BinaryPromptEscape, exchange.BinaryEscapeLagMyr)}");
        lines.Add($"  outgoing: {FormatEscapeStatus(exchange.OutgoingPromptEscape, exchange.OutgoingEscapeLagMyr)}");
        lines.Add("");
        lines.Add("<size=80%>HDF5 before/after snapshots; local vesc is a spherical tidal-radius-truncated estimate. Event quantities are not interpolated.</size>");

        return string.Join("\n", lines);
    }

    void AppendLiveExchangeKinematics(
        List<string> lines,
        Dictionary<int, BodyData> bodyById,
        ClusterScienceRecord cluster,
        ScienceExchangeRecord exchange,
        bool isBefore)
    {
        int binaryId1 = isBefore ? exchange.BeforeId1 : exchange.AfterId1;
        int binaryId2 = isBefore ? exchange.BeforeId2 : exchange.AfterId2;
        int singleId = isBefore ? exchange.IncomingId : exchange.OutgoingId;
        string singleName = isBefore ? "incoming" : "outgoing";

        if (TryGetBinaryClusterSpeed(bodyById, binaryId1, binaryId2, cluster.CenterVelocityKms, out float binarySpeed))
        {
            string ratio = cluster.Sigma1dKms.HasValue && cluster.Sigma1dKms.Value > 0.0
                ? (binarySpeed / cluster.Sigma1dKms.Value).ToString("F2", CultureInfo.InvariantCulture)
                : "—";
            lines.Add($"Binary {binaryId1}+{binaryId2}: vcl = {binarySpeed:F3} {velocityUnit}, v/σ = {ratio}");
        }
        else
        {
            lines.Add($"Binary {binaryId1}+{binaryId2}: unavailable in this frame");
        }

        if (bodyById.TryGetValue(singleId, out BodyData single))
        {
            float singleSpeed = (single.velocity - cluster.CenterVelocityKms).magnitude;
            string ratio = cluster.Sigma1dKms.HasValue && cluster.Sigma1dKms.Value > 0.0
                ? (singleSpeed / cluster.Sigma1dKms.Value).ToString("F2", CultureInfo.InvariantCulture)
                : "—";
            lines.Add($"{singleName} {singleId}: vcl = {singleSpeed:F3} {velocityUnit}, v/σ = {ratio}");
        }
        else
        {
            lines.Add($"{singleName} {singleId}: unavailable in this frame");
        }
    }

    bool TryGetBinaryClusterSpeed(
        Dictionary<int, BodyData> bodyById,
        int id1,
        int id2,
        Vector3 clusterVelocity,
        out float speed)
    {
        speed = 0f;
        if (!bodyById.TryGetValue(id1, out BodyData body1) ||
            !bodyById.TryGetValue(id2, out BodyData body2)) return false;
        float totalMass = body1.mass + body2.mass;
        if (totalMass <= 0f) return false;
        Vector3 binaryVelocity = (body1.velocity * body1.mass + body2.velocity * body2.mass) / totalMass;
        speed = (binaryVelocity - clusterVelocity).magnitude;
        return true;
    }

    string FormatNumber(double? value, string format)
    {
        return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "—";
    }

    string FormatScientific(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.###E+0", CultureInfo.InvariantCulture) : "—";
    }

    string FormatSignedScientific(double? value)
    {
        if (!value.HasValue) return "—";
        return value.Value.ToString("+0.###E+0;-0.###E+0;0", CultureInfo.InvariantCulture);
    }

    string FormatEscapeRatio(double? value)
    {
        if (!value.HasValue) return "—";
        string formatted = value.Value.ToString("F2", CultureInfo.InvariantCulture);
        return value.Value > 1.0 ? $"<color=#FF6B6B>{formatted}</color>" : formatted;
    }

    string FormatEscapeStatus(bool escaped, double? lagMyr)
    {
        if (!escaped) return "not linked by prompt criterion";
        string lag = lagMyr.HasValue ? $", lag {lagMyr.Value:F3} Myr" : "";
        return $"<color=#7CFF9B>confirmed{lag}</color>";
    }

    void AppendPairScienceData(
        List<string> lines,
        string pairName,
        int idA,
        int idB,
        float currentTime,
        Dictionary<int, BodyData> bodyById)
    {
        lines.Add($"{pairName}: ID {idA} + ID {idB}");

        if (!bodyById.TryGetValue(idA, out BodyData bodyA) ||
            !bodyById.TryGetValue(idB, out BodyData bodyB))
        {
            lines.Add("Data unavailable in this snapshot");
            return;
        }

        float totalMass = bodyA.mass + bodyB.mass;
        float maximumMass = Mathf.Max(bodyA.mass, bodyB.mass);
        float massRatio = maximumMass > 0f ? Mathf.Min(bodyA.mass, bodyB.mass) / maximumMass : 0f;
        Vector3 relativePosition = bodyB.position - bodyA.position;
        Vector3 relativeVelocity = GetDisplayedVelocity(bodyB, currentTime) - GetDisplayedVelocity(bodyA, currentTime);
        float separation = relativePosition.magnitude;
        float relativeSpeed = relativeVelocity.magnitude;
        float radialVelocity = separation > 0f
            ? Vector3.Dot(relativeVelocity, relativePosition / separation)
            : 0f;
        string radialState = radialVelocity < 0f ? "approaching" : radialVelocity > 0f ? "receding" : "stationary";

        lines.Add($"Mtot = {totalMass:F2} {solarMassUnit}, q = {massRatio:F3}");
        lines.Add($"r = {separation:F3} {distanceUnit}");
        lines.Add($"vrel = {relativeSpeed:F3} {velocityUnit}");
        lines.Add($"vr = {radialVelocity:+0.000;-0.000;0.000} {velocityUnit} ({radialState})");
    }

    Vector3 GetDisplayedVelocity(BodyData body, float currentTime)
    {
        return body.velocity - GetDisplayedVelocityReference(currentTime, Vector3.zero);
    }

    bool TryGetFollowTarget(float time, out BodyData target)
    {
        target = null;
        if (!isFollowMode) return false;
        if (followBinaryCenterOfMass)
        {
            return TryGetSelectedBinaryCenterOfMass(time, out target);
        }
        if (followedBhId < 0) return false;
        if (!timeDataMap.TryGetValue(time, out List<BodyData> frameData)) return false;

        target = frameData.FirstOrDefault(body => body.id == followedBhId);
        if (target == null)
        {
            TryGetMergeSurvivor(time, frameData, out target);
        }
        return target != null;
    }

    bool TryGetSelectedBinaryCenterOfMass(float time, out BodyData center)
    {
        center = null;
        if (currentSelectedEvent == null || currentSelectedEvent.ids == null || currentSelectedEvent.ids.Length < 2) return false;
        if (!timeDataMap.TryGetValue(time, out List<BodyData> frameData)) return false;

        bool useAfterPair = currentSelectedEvent.type == "EXCHANGE"
            && currentSelectedEvent.ids.Length >= 4
            && time >= currentSelectedEvent.time;
        int id1 = useAfterPair ? currentSelectedEvent.ids[2] : currentSelectedEvent.ids[0];
        int id2 = useAfterPair ? currentSelectedEvent.ids[3] : currentSelectedEvent.ids[1];
        BodyData body1 = frameData.FirstOrDefault(body => body.id == id1);
        BodyData body2 = frameData.FirstOrDefault(body => body.id == id2);
        if (body1 == null || body2 == null) return false;

        float totalMass = body1.mass + body2.mass;
        if (totalMass <= 0f) return false;
        center = new BodyData
        {
            time = time,
            id = FollowBinarySentinel,
            mass = totalMass,
            position = (body1.position * body1.mass + body2.position * body2.mass) / totalMass,
            velocity = (body1.velocity * body1.mass + body2.velocity * body2.mass) / totalMass
        };
        return true;
    }

    bool TryGetMergeSurvivor(float time, List<BodyData> frameData, out BodyData survivor)
    {
        survivor = null;
        InteractionEvent mergeEvent = GetApplicableMergeEvent(time);
        if (mergeEvent == null) return false;

        List<BodyData> survivors = frameData
            .Where(body => body.id != followedBhId)
            .Where(body => ArrayUtility.IndexOf(mergeEvent.ids, body.id) >= 0)
            .GroupBy(body => body.id)
            .Select(group => group.First())
            .ToList();

        if (survivors.Count != 1) return false;
        survivor = survivors[0];
        return true;
    }

    InteractionEvent GetApplicableMergeEvent(float time)
    {
        if (currentSelectedEvent != null
            && currentSelectedEvent.type == "MERGE"
            && currentSelectedEvent.time <= time
            && ArrayUtility.IndexOf(currentSelectedEvent.ids, followedBhId) >= 0)
        {
            return currentSelectedEvent;
        }

        return loadedEvents
            .Where(evt => evt.type == "MERGE")
            .Where(evt => evt.time <= time)
            .Where(evt => ArrayUtility.IndexOf(evt.ids, followedBhId) >= 0)
            .OrderByDescending(evt => evt.time)
            .FirstOrDefault();
    }

    bool TryGetDisplayedPositionReference(float time, out Vector3 reference)
    {
        if (isFollowMode)
        {
            if (TryGetFollowTarget(time, out BodyData target))
            {
                reference = target.position - followAnchorPosition;
                return true;
            }

            reference = Vector3.zero;
            return false;
        }

        if (useCenterOfMass)
        {
            ClusterScienceRecord cluster = scienceDataStore.FindNearestCluster(time, playbackTimeIsNbody);
            if (cluster != null && cluster.QualityOk)
            {
                reference = cluster.CenterPc;
                return true;
            }
            if (centerOfMassMap.TryGetValue(time, out Vector3 centerOfMass))
            {
                reference = centerOfMass;
                return true;
            }
        }

        reference = Vector3.zero;
        return true;
    }

    Vector3 GetDisplayedPositionReference(float time, Vector3 fallback)
    {
        return TryGetDisplayedPositionReference(time, out Vector3 reference)
            ? reference
            : fallback;
    }

    Vector3 GetDisplayedVelocityReference(float time, Vector3 fallback)
    {
        if (isFollowMode)
        {
            return TryGetFollowTarget(time, out BodyData target)
                ? target.velocity
                : fallback;
        }

        if (useCenterOfMass)
        {
            ClusterScienceRecord cluster = scienceDataStore.FindNearestCluster(time, playbackTimeIsNbody);
            if (cluster != null && cluster.QualityOk)
            {
                return cluster.CenterVelocityKms;
            }
            if (centerOfMassVelocityMap.TryGetValue(time, out Vector3 centerOfMassVelocity))
            {
                return centerOfMassVelocity;
            }
        }

        return Vector3.zero;
    }

    float GetLabelDistanceScale(Vector3 worldPosition)
    {
        if (!keepLabelScreenSize || mainCamera == null || mainCamera.orthographic)
        {
            return 1.0f;
        }

        float cameraDepth = Mathf.Abs(
            Vector3.Dot(
                worldPosition - mainCamera.transform.position,
                mainCamera.transform.forward
            )
        );

        float referenceDistance = Mathf.Max(0.01f, labelReferenceDistance);
        float distanceScale = cameraDepth / referenceDistance;
        float minimumScale = Mathf.Max(0.01f, minimumLabelDistanceScale);
        float maximumScale = Mathf.Max(minimumScale, maximumLabelDistanceScale);
        return Mathf.Clamp(distanceScale, minimumScale, maximumScale);
    }

    List<Vector3> SmoothCurve(List<Vector3> rawPoints, int stepsPerSegment)
    {
        if (rawPoints == null || rawPoints.Count < 2) return rawPoints;

        List<Vector3> smoothedPoints = new List<Vector3>();

        for (int i = 0; i < rawPoints.Count - 1; i++)
        {
            Vector3 p0 = i == 0 ? rawPoints[i] : rawPoints[i - 1];
            Vector3 p1 = rawPoints[i];
            Vector3 p2 = rawPoints[i + 1];
            Vector3 p3 = i == rawPoints.Count - 2 ? rawPoints[i + 1] : rawPoints[i + 2];

            int steps = (i == rawPoints.Count - 2) ? stepsPerSegment + 1 : stepsPerSegment;
            for (int j = 0; j < steps; j++)
            {
                float t = j / (float)stepsPerSegment;
                Vector3 a = 2f * p1;
                Vector3 b = p2 - p0;
                Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
                Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;
                
                Vector3 interpolatedPos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
                smoothedPoints.Add(interpolatedPos);
            }
        }
        return smoothedPoints;
    }

    void LoadHistoryData(string[] lines)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Split(',');
            if (values.Length < 9) continue;

            BodyData body = new BodyData();
            float.TryParse(values[0], NumberStyles.Float, ci, out body.time);
            
            if(float.TryParse(values[1], NumberStyles.Float, ci, out float tempId))
            {
                body.id = (int)tempId;
                validHistoryIds.Add(body.id);
            }

            float.TryParse(values[2], NumberStyles.Float, ci, out body.mass);
            
            float vx=0, vy=0, vz=0;
            float.TryParse(values[3], NumberStyles.Float, ci, out vx);
            float.TryParse(values[4], NumberStyles.Float, ci, out vy);
            float.TryParse(values[5], NumberStyles.Float, ci, out vz);
            body.velocity = new Vector3(vx, vy, vz);

            float x=0, y=0, z=0;
            float.TryParse(values[6], NumberStyles.Float, ci, out x);
            float.TryParse(values[7], NumberStyles.Float, ci, out y);
            float.TryParse(values[8], NumberStyles.Float, ci, out z);
            body.position = new Vector3(x, y, z);

            if (!timeDataMap.ContainsKey(body.time)) timeDataMap[body.time] = new List<BodyData>();
            timeDataMap[body.time].Add(body);
        }
        sortedTimeKeys = timeDataMap.Keys.ToList();
        sortedTimeKeys.Sort();

        foreach (var kvp in timeDataMap)
        {
            Vector3 com = Vector3.zero;
            Vector3 comVelocity = Vector3.zero;
            float totalMass = 0f;
            foreach (var body in kvp.Value)
            {
                com += body.position * body.mass;
                comVelocity += body.velocity * body.mass;
                totalMass += body.mass;
            }
            if (totalMass > 0f)
            {
                com /= totalMass;
                comVelocity /= totalMass;
            }
            centerOfMassMap[kvp.Key] = com;
            centerOfMassVelocityMap[kvp.Key] = comVelocity;
        }
    }

    void LoadEventData(string[] lines)
    {
        if (lines.Length < 2) return;
        List<string> headers = lines[0].Split(',').Select(h => h.Trim()).ToList();

        int idxType = headers.IndexOf("event_list");
        int idxTime = headers.IndexOf("time_list");
        if (idxTime == -1) idxTime = headers.IndexOf("time_myr");
        
        int idxId1 = headers.IndexOf("id1_list");
        int idxId2 = headers.IndexOf("id2_list");
        int idxId3 = headers.IndexOf("id3_list");
        int idxId4 = headers.IndexOf("id4_list");

        if (idxType == -1 || idxTime == -1) return;

        CultureInfo ci = CultureInfo.InvariantCulture;
        HashSet<string> seenEventKeys = new HashSet<string>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] vals = lines[i].Split(',');
            if (vals.Length <= idxType || vals.Length <= idxTime) continue;
            
            string type = vals[idxType].Trim();
            if (type != "MERGE" && type != "EXCHANGE") continue;

            InteractionEvent evt = new InteractionEvent();
            evt.type = type;
            if (float.TryParse(vals[idxTime], NumberStyles.Float, ci, out float t)) evt.time = t;

            List<int> ids = new List<int>();
            int[] idIndices = { idxId1, idxId2, idxId3, idxId4 };
            foreach (int idx in idIndices)
            {
                if (idx != -1 && idx < vals.Length)
                {
                    string s = vals[idx].Trim();
                    if (!string.IsNullOrEmpty(s) && float.TryParse(s, NumberStyles.Float, ci, out float fid))
                    {
                        ids.Add((int)fid);
                    }
                }
            }
            evt.ids = ids.ToArray();
            
            if (evt.ids.Length == 0) continue;

            string eventKey = evt.type + "|" + evt.time.ToString("R", ci) + "|" + string.Join("+", evt.ids);
            if (!seenEventKeys.Add(eventKey)) continue;

            loadedEvents.Add(evt);
        }
    }

    void SetupUI()
    {
        ResolveScienceUIReferences();

        if (timeSlider != null && sortedTimeKeys.Count > 0)
        {
            timeSlider.minValue = 0;
            timeSlider.maxValue = sortedTimeKeys.Count - 1;
            timeSlider.wholeNumbers = true;
            timeSlider.onValueChanged.RemoveListener(OnSliderChanged);
            if (!HasPersistentSliderListener(timeSlider, nameof(OnSliderChanged)))
            {
                timeSlider.onValueChanged.AddListener(OnSliderChanged);
            }
        }
        if (speedSlider != null)
        {
            speedSlider.minValue = 0.0f;
            speedSlider.maxValue = 1.0f;
            speedSlider.wholeNumbers = false;

            float minimumSpeed = Mathf.Max(0.001f, minimumStepsPerSecond);
            float maximumSpeed = Mathf.Max(minimumSpeed, maximumStepsPerSecond);
            stepsPerSecond = Mathf.Clamp(initialStepsPerSecond, minimumSpeed, maximumSpeed);
            speedSlider.SetValueWithoutNotify(0.5f);

            speedSlider.onValueChanged.RemoveListener(OnSpeedSliderChanged);
            if (!HasPersistentSliderListener(speedSlider, nameof(OnSpeedSliderChanged)))
            {
                speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
            }
        }
        if (sizeSlider != null)
        {
            sizeSlider.minValue = sizeSliderMin;
            sizeSlider.maxValue = sizeSliderMax;
            sizeSlider.wholeNumbers = false;
            
            float midSize = (sizeSlider.minValue + sizeSlider.maxValue) / 2f;
            sizeSlider.value = midSize;
            scaleMultiplier = midSize;
            
            sizeSlider.onValueChanged.RemoveListener(OnSizeSliderChanged);
            if (!HasPersistentSliderListener(sizeSlider, nameof(OnSizeSliderChanged)))
            {
                sizeSlider.onValueChanged.AddListener(OnSizeSliderChanged);
            }
        }
        SetupPlayButton();
        SetupFollowModeUI();
        
        if (eventDropdown != null)
        {
            eventDropdown.ClearOptions();
            List<string> options = new List<string> { "Select Event..." };
            
            List<InteractionEvent> validDropdownEvents = new List<InteractionEvent>();

            foreach (var evt in loadedEvents)
            {
                List<string> idMassStrings = new List<string>();
                bool hasInvalidId = false;

                foreach (int id in evt.ids)
                {
                    float bestMass = -1f;
                    float closestDiff = float.MaxValue;

                    foreach (float t in sortedTimeKeys)
                    {
                        BodyData bd = timeDataMap[t].FirstOrDefault(b => b.id == id);
                        
                        if (bd != null && bd.mass > 0f)
                        {
                            float diff = Mathf.Abs(t - evt.time);
                            if (diff < closestDiff)
                            {
                                closestDiff = diff;
                                bestMass = bd.mass;
                            }
                        }
                    }

                    if (bestMass > 0f)
                    {
                        idMassStrings.Add($"{id}({bestMass:F1} {solarMassUnit})"); 
                    }
                    else
                    {
                        hasInvalidId = true;
                        break; 
                    }
                }

                if (hasInvalidId || idMassStrings.Count == 0)
                {
                    continue; 
                }

                string relationStr = "";
                if (idMassStrings.Count >= 4)
                {
                    relationStr = $"{idMassStrings[0]} & {idMassStrings[1]} → {idMassStrings[2]} & {idMassStrings[3]}";
                }
                else if (idMassStrings.Count == 3)
                {
                    relationStr = $"{idMassStrings[0]} & {idMassStrings[1]} → {idMassStrings[2]}";
                }
                else if (idMassStrings.Count == 2)
                {
                    relationStr = $"{idMassStrings[0]} & {idMassStrings[1]}";
                }
                else
                {
                    relationStr = string.Join(", ", idMassStrings);
                }

                double eventTimeMyr = GetDisplayTimeMyr(evt.time);
                string label = $"{evt.type} @ {eventTimeMyr:F0} Myr\n{relationStr}";
                options.Add(label);
                
                validDropdownEvents.Add(evt);
            }
            
            loadedEvents = validDropdownEvents;

            eventDropdown.AddOptions(options);
            eventDropdown.onValueChanged.RemoveListener(OnEventSelected);
            if (!HasPersistentDropdownListener(eventDropdown, nameof(OnEventSelected)))
            {
                eventDropdown.onValueChanged.AddListener(OnEventSelected);
            }
        }
        
        if (togglePosModeButton != null)
        {
            togglePosModeButton.onClick.RemoveListener(TogglePositionMode);
            if (!HasPersistentButtonListener(togglePosModeButton, nameof(TogglePositionMode)))
            {
                togglePosModeButton.onClick.AddListener(TogglePositionMode);
            }
            UpdateToggleButtonText();
        }

        if (scienceModeButton != null)
        {
            scienceModeButton.onClick.RemoveListener(ToggleScienceMode);
            if (!HasPersistentButtonListener(scienceModeButton, nameof(ToggleScienceMode)))
            {
                scienceModeButton.onClick.AddListener(ToggleScienceMode);
            }
        }
        UpdateScienceUIState();
    }

    bool HasPersistentSliderListener(Slider slider, string methodName)
    {
        if (slider == null) return false;
        for (int i = 0; i < slider.onValueChanged.GetPersistentEventCount(); i++)
        {
            if (slider.onValueChanged.GetPersistentTarget(i) == this &&
                slider.onValueChanged.GetPersistentMethodName(i) == methodName) return true;
        }
        return false;
    }

    bool HasPersistentDropdownListener(Dropdown dropdown, string methodName)
    {
        if (dropdown == null) return false;
        for (int i = 0; i < dropdown.onValueChanged.GetPersistentEventCount(); i++)
        {
            if (dropdown.onValueChanged.GetPersistentTarget(i) == this &&
                dropdown.onValueChanged.GetPersistentMethodName(i) == methodName) return true;
        }
        return false;
    }

    bool HasPersistentButtonListener(Button button, string methodName)
    {
        if (button == null) return false;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == methodName) return true;
        }
        return false;
    }

    public void TogglePositionMode()
    {
        if (isFollowMode) return;

        useCenterOfMass = !useCenterOfMass;
        UpdateToggleButtonText();
        
        if (timeSlider != null) UpdateVisuals((int)timeSlider.value);
    }

    void UpdateToggleButtonText()
    {
        if (togglePosModeButton != null)
        {
            string label = isFollowMode
                ? (followBinaryCenterOfMass ? "Pos: Follow Binary CoM" : $"Pos: Follow BH {followedBhId}")
                : (useCenterOfMass ? "Pos: CoM" : "Pos: Raw");

            Text btnText = togglePosModeButton.GetComponentInChildren<Text>(true);
            TMP_Text tmpText = togglePosModeButton.GetComponentInChildren<TMP_Text>(true);
            if (btnText != null) btnText.text = label;
            if (tmpText != null) tmpText.text = label;
            togglePosModeButton.interactable = !isFollowMode;
        }
    }

    void SetupFollowModeUI()
    {
        if (followModeButton != null)
        {
            bool hasPersistentListener = false;
            for (int i = 0; i < followModeButton.onClick.GetPersistentEventCount(); i++)
            {
                if (followModeButton.onClick.GetPersistentTarget(i) == this &&
                    followModeButton.onClick.GetPersistentMethodName(i) == nameof(ToggleFollowModeMenu))
                {
                    hasPersistentListener = true;
                    break;
                }
            }

            if (!hasPersistentListener)
            {
                followModeButton.onClick.RemoveListener(ToggleFollowModeMenu);
                followModeButton.onClick.AddListener(ToggleFollowModeMenu);
            }
        }

        if (followBhDropdown != null)
        {
            followBhDropdown.onValueChanged.RemoveListener(OnFollowBhSelected);
            followBhDropdown.onValueChanged.AddListener(OnFollowBhSelected);
            followBhDropdown.gameObject.SetActive(false);
        }

        ResolveFollowModeButtonText();
        UpdateFollowModeUI();
    }

    void ResolveFollowModeButtonText()
    {
        if (followModeButton == null) return;

        if (followModeButtonText == null)
        {
            followModeButtonText = followModeButton.GetComponentInChildren<Text>(true);
        }

        if (followModeButtonTmpText == null)
        {
            followModeButtonTmpText = followModeButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    void UpdateFollowModeUI()
    {
        ResolveFollowModeButtonText();

        string label = isFollowMode
            ? (followBinaryCenterOfMass ? "Exit Binary Follow" : "Exit Follow")
            : "Follow Mode";
        if (followModeButtonText != null) followModeButtonText.text = label;
        if (followModeButtonTmpText != null) followModeButtonTmpText.text = label;

        UpdateToggleButtonText();
    }

    public void ToggleFollowModeMenu()
    {
        if (isFollowMode)
        {
            ExitFollowMode(true);
            return;
        }

        if (followBhDropdown == null || timeSlider == null) return;

        bool shouldShow = !followBhDropdown.gameObject.activeSelf;
        followBhDropdown.gameObject.SetActive(shouldShow);
        if (shouldShow)
        {
            RefreshFollowDropdown(GetCurrentFrameData());
        }
    }

    public void OnFollowBhSelected(int index)
    {
        if (followBhDropdown == null || index <= 0 || timeSlider == null) return;

        List<BodyData> currentFrameData = GetCurrentFrameData();
        int idIndex = index - 1;
        if (idIndex < 0 || idIndex >= followDropdownIds.Count) return;

        int selectedId = followDropdownIds[idIndex];
        bool selectedBinary = selectedId == FollowBinarySentinel;
        BodyData selectedBody = null;
        if (selectedBinary)
        {
            float currentTime = sortedTimeKeys[Mathf.Clamp((int)timeSlider.value, 0, sortedTimeKeys.Count - 1)];
            TryGetSelectedBinaryCenterOfMass(currentTime, out selectedBody);
        }
        else
        {
            selectedBody = currentFrameData.FirstOrDefault(body => body.id == selectedId);
        }
        if (selectedBody == null) return;

        bool enteringFollowMode = !isFollowMode;
        isFollowMode = true;
        followBinaryCenterOfMass = selectedBinary;
        followedBhId = selectedBinary ? -1 : selectedId;
        followAnchorPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
        CaptureFollowBackgroundAnchor(selectedBody, enteringFollowMode);

        UpdateFollowModeUI();
        RefreshFollowDropdown(currentFrameData);
        UpdateVisuals((int)timeSlider.value);
    }

    void ExitFollowMode(bool refreshVisuals)
    {
        RestoreFollowBackgrounds();
        isFollowMode = false;
        followBinaryCenterOfMass = false;
        followedBhId = -1;

        if (followBhDropdown != null)
        {
            followBhDropdown.SetValueWithoutNotify(0);
            followBhDropdown.gameObject.SetActive(false);
        }

        UpdateFollowModeUI();

        if (refreshVisuals && timeSlider != null)
        {
            UpdateVisuals((int)timeSlider.value);
        }
    }

    List<BodyData> GetCurrentFrameData()
    {
        if (timeSlider == null || sortedTimeKeys.Count == 0)
        {
            return new List<BodyData>();
        }

        int index = Mathf.Clamp((int)timeSlider.value, 0, sortedTimeKeys.Count - 1);
        return timeDataMap[sortedTimeKeys[index]];
    }

    void RefreshFollowDropdown(List<BodyData> frameData)
    {
        if (followBhDropdown == null) return;

        List<BodyData> availableBodies = frameData
            .GroupBy(body => body.id)
            .Select(group => group.First())
            .ToList();

        List<BodyData> candidates;
        if (currentSelectedEvent != null)
        {
            Dictionary<int, BodyData> bodiesById = availableBodies.ToDictionary(body => body.id);
            candidates = currentSelectedEvent.ids
                .Distinct()
                .Where(id => bodiesById.ContainsKey(id))
                .Select(id => bodiesById[id])
                .ToList();
        }
        else
        {
            candidates = availableBodies.OrderBy(body => body.id).ToList();
        }

        followDropdownIds.Clear();
        List<string> options = new List<string> { "Select target..." };
        if (currentSelectedEvent != null && currentSelectedEvent.ids.Length >= 2)
        {
            followDropdownIds.Add(FollowBinarySentinel);
            options.Add(currentSelectedEvent.type == "EXCHANGE"
                ? "Binary CoM (auto-switch at EXCHANGE)"
                : "Binary CoM");
        }
        followDropdownIds.AddRange(candidates.Select(body => body.id));
        options.AddRange(candidates.Select(body => $"BH {body.id} ({body.mass:F1} {solarMassUnit})"));

        followBhDropdown.ClearOptions();
        followBhDropdown.AddOptions(options);

        int selectedIndex = 0;
        if (isFollowMode)
        {
            int selectedId = followBinaryCenterOfMass ? FollowBinarySentinel : followedBhId;
            int targetIndex = followDropdownIds.IndexOf(selectedId);
            if (targetIndex >= 0) selectedIndex = targetIndex + 1;
        }
        followBhDropdown.SetValueWithoutNotify(selectedIndex);
        followBhDropdown.RefreshShownValue();
    }

    void CaptureFollowBackgroundAnchor(BodyData target, bool captureOriginalPositions)
    {
        if (target == null) return;

        if (captureOriginalPositions)
        {
            followBackgroundOriginalPositions.Clear();
            if (followRelativeBackgrounds != null)
            {
                foreach (Transform background in followRelativeBackgrounds)
                {
                    if (background != null && !followBackgroundOriginalPositions.ContainsKey(background))
                    {
                        followBackgroundOriginalPositions.Add(background, background.position);
                    }
                }
            }
        }

        followBackgroundSegmentPositions.Clear();
        foreach (Transform background in followBackgroundOriginalPositions.Keys)
        {
            if (background != null)
            {
                followBackgroundSegmentPositions[background] = background.position;
            }
        }

        followBackgroundTargetAnchorPosition = target.position;
        hasFollowBackgroundAnchor = followBackgroundSegmentPositions.Count > 0;

        if (captureOriginalPositions)
        {
            BeginFollowSkyboxMotion(target);
        }
        else
        {
            followSkyboxPreviousTargetPosition = target.position;
        }
    }

    void UpdateFollowBackgroundMotion(float currentTime, float nextTime, float fraction)
    {
        if (!isFollowMode || (!hasFollowBackgroundAnchor && !hasFollowSkyboxMotion)) return;
        if (!TryGetFollowTarget(currentTime, out BodyData currentTarget)) return;

        BodyData nextTarget = currentTarget;
        TryGetFollowTarget(nextTime, out nextTarget);
        if (nextTarget == null) nextTarget = currentTarget;

        Vector3 targetPosition = Vector3.Lerp(currentTarget.position, nextTarget.position, fraction);
        Vector3 displacement = targetPosition - followBackgroundTargetAnchorPosition;

        foreach (var entry in followBackgroundSegmentPositions)
        {
            if (entry.Key != null)
            {
                entry.Key.position = entry.Value - displacement * followBackgroundMotionScale;
            }
        }

        UpdateFollowSkyboxMotion(targetPosition, currentTarget.velocity);
    }

    void BeginFollowSkyboxMotion(BodyData target)
    {
        RestoreFollowSkybox();
        if (!rotateSkyboxWhileFollowing || target == null || RenderSettings.skybox == null) return;
        if (!RenderSettings.skybox.HasProperty("_Rotation")) return;

        followOriginalSkyboxMaterial = RenderSettings.skybox;
        followRuntimeSkyboxMaterial = new Material(followOriginalSkyboxMaterial);
        followSkyboxRotation = followRuntimeSkyboxMaterial.GetFloat("_Rotation");
        followSkyboxPreviousTargetPosition = target.position;
        RenderSettings.skybox = followRuntimeSkyboxMaterial;
        hasFollowSkyboxMotion = true;
    }

    void UpdateFollowSkyboxMotion(Vector3 targetPosition, Vector3 targetVelocity)
    {
        if (!hasFollowSkyboxMotion || followRuntimeSkyboxMaterial == null) return;

        Vector3 step = targetPosition - followSkyboxPreviousTargetPosition;
        if (step.sqrMagnitude > 0.0f)
        {
            float direction = targetVelocity.sqrMagnitude > 0.0f && Vector3.Dot(step, targetVelocity) < 0.0f
                ? -1.0f
                : 1.0f;
            followSkyboxRotation = Mathf.Repeat(
                followSkyboxRotation + step.magnitude * followSkyboxDegreesPerDistance * direction,
                360.0f
            );
            followRuntimeSkyboxMaterial.SetFloat("_Rotation", followSkyboxRotation);
        }

        followSkyboxPreviousTargetPosition = targetPosition;
    }

    void RestoreFollowSkybox()
    {
        if (followRuntimeSkyboxMaterial != null)
        {
            if (RenderSettings.skybox == followRuntimeSkyboxMaterial)
            {
                RenderSettings.skybox = followOriginalSkyboxMaterial;
            }
            Destroy(followRuntimeSkyboxMaterial);
        }

        followOriginalSkyboxMaterial = null;
        followRuntimeSkyboxMaterial = null;
        hasFollowSkyboxMotion = false;
    }

    void RestoreFollowBackgrounds()
    {
        foreach (var entry in followBackgroundOriginalPositions)
        {
            if (entry.Key != null)
            {
                entry.Key.position = entry.Value;
            }
        }

        followBackgroundOriginalPositions.Clear();
        followBackgroundSegmentPositions.Clear();
        hasFollowBackgroundAnchor = false;
        RestoreFollowSkybox();
    }

    void OnDestroy()
    {
        RestoreFollowBackgrounds();
    }

    public void ToggleScienceMode()
    {
        isScienceMode = !isScienceMode;
        UpdateScienceUIState();

        if (timeSlider != null)
        {
            UpdateVisuals((int)timeSlider.value);
        }
    }

    void UpdateScienceUIState()
    {
        if (scienceModeButton != null)
        {
            Text buttonText = scienceModeButton.GetComponentInChildren<Text>(true);
            TMP_Text tmpButtonText = scienceModeButton.GetComponentInChildren<TMP_Text>(true);
            string label = isScienceMode ? "Science: ON" : "Science: OFF";
            if (buttonText != null) buttonText.text = label;
            if (tmpButtonText != null) tmpButtonText.text = label;
        }

        bool showPanel = isScienceMode && currentSelectedEvent != null;
        if (scienceDataPanel != null)
        {
            scienceDataPanel.SetActive(showPanel);
        }
        else if (scienceDataText != null)
        {
            scienceDataText.gameObject.SetActive(showPanel);
        }
        else if (scienceDataTmpText != null)
        {
            scienceDataTmpText.gameObject.SetActive(showPanel);
        }

        if (!showPanel)
        {
            HideScienceWorldVisuals();
        }
    }

    void SetupPlayButton()
    {
        if (playButton == null) return;

        bool hasPersistentToggleListener = false;
        for (int i = 0; i < playButton.onClick.GetPersistentEventCount(); i++)
        {
            if (playButton.onClick.GetPersistentTarget(i) == this &&
                playButton.onClick.GetPersistentMethodName(i) == nameof(TogglePlay))
            {
                hasPersistentToggleListener = true;
                break;
            }
        }

        if (!hasPersistentToggleListener)
        {
            playButton.onClick.RemoveListener(TogglePlay);
            playButton.onClick.AddListener(TogglePlay);
        }

        ResolvePlayButtonText();
        UpdateButtonText();
    }

    void ResolvePlayButtonText()
    {
        if (playButton == null) return;

        if (playButtonText == null)
        {
            playButtonText = playButton.GetComponentInChildren<Text>(true);
        }

        if (playButtonTmpText == null)
        {
            playButtonTmpText = playButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    public void TogglePlay()
    {
        isPlaying = !isPlaying;
        UpdateButtonText();
    }

    void UpdateButtonText()
    {
        ResolvePlayButtonText();

        string label = isPlaying ? "Stop" : "Play";
        if (playButtonText != null) playButtonText.text = label;
        if (playButtonTmpText != null) playButtonTmpText.text = label;
    }
    public void OnSliderChanged(float value) { UpdateVisuals((int)value); }
    public void OnSpeedSliderChanged(float value)
    {
        float minimumSpeed = Mathf.Max(0.001f, minimumStepsPerSecond);
        float maximumSpeed = Mathf.Max(minimumSpeed, maximumStepsPerSecond);
        float centerSpeed = Mathf.Clamp(initialStepsPerSecond, minimumSpeed, maximumSpeed);
        float normalizedValue = Mathf.Clamp01(value);

        if (normalizedValue <= 0.5f)
        {
            float t = normalizedValue / 0.5f;
            stepsPerSecond = Mathf.Exp(Mathf.Lerp(Mathf.Log(minimumSpeed), Mathf.Log(centerSpeed), t));
        }
        else
        {
            float t = (normalizedValue - 0.5f) / 0.5f;
            stepsPerSecond = Mathf.Exp(Mathf.Lerp(Mathf.Log(centerSpeed), Mathf.Log(maximumSpeed), t));
        }
    }
    public void OnSizeSliderChanged(float value) { scaleMultiplier = value; if (timeSlider != null) UpdateVisuals((int)timeSlider.value); }
    public void OnEventSelected(int index)
    {
        if (isHandlingEventSelection) return;

        InteractionEvent requestedEvent = index > 0 && index <= loadedEvents.Count
            ? loadedEvents[index - 1]
            : null;
        if (index == lastHandledEventSelection && currentSelectedEvent == requestedEvent) return;

        isHandlingEventSelection = true;
        try
        {
            if (isFollowMode)
            {
                ExitFollowMode(false);
            }

            if (requestedEvent == null)
            {
                currentSelectedEvent = null;
                currentEventIndex = -1;
                UpdateScienceUIState();
                if (followBhDropdown != null && followBhDropdown.gameObject.activeSelf)
                {
                    RefreshFollowDropdown(GetCurrentFrameData());
                }
                if (timeSlider != null) UpdateVisuals((int)timeSlider.value);
                lastHandledEventSelection = index;
                return;
            }

            currentSelectedEvent = requestedEvent;

            if (reduceBhSizeOnFirstEventSelection && !hasAppliedFirstEventSize)
            {
                float targetScale = Mathf.Clamp(firstEventScaleMultiplier, sizeSliderMin, sizeSliderMax);
                scaleMultiplier = targetScale;

                if (sizeSlider != null)
                {
                    sizeSlider.SetValueWithoutNotify(targetScale);
                }

                hasAppliedFirstEventSize = true;
            }

            float closestTime = -1f;
            float minDiff = float.MaxValue;
            int targetIndex = 0;
            for(int i=0; i<sortedTimeKeys.Count; i++)
            {
                float diff = Mathf.Abs(sortedTimeKeys[i] - currentSelectedEvent.time);
                if(diff < minDiff) { minDiff = diff; closestTime = sortedTimeKeys[i]; targetIndex = i; }
            }
            
            currentEventIndex = targetIndex;
            UpdateScienceUIState();
            
            int dynamicPreRollSteps = Mathf.RoundToInt(preRollSeconds * stepsPerSecond);
            int startIndex = Mathf.Max(0, targetIndex - dynamicPreRollSteps);
            
            if (timeSlider != null)
            {
                timeSlider.SetValueWithoutNotify(startIndex);
                UpdateVisuals(startIndex);
            }
            FocusPlayerOnSelectedEvent(startIndex);
            if (followBhDropdown != null && followBhDropdown.gameObject.activeSelf)
            {
                RefreshFollowDropdown(GetCurrentFrameData());
            }
            UpdateButtonText();
            lastHandledEventSelection = index;
        }
        finally
        {
            isHandlingEventSelection = false;
        }
    }

    void FocusPlayerOnSelectedEvent(int timeIndex)
    {
        if (!movePlayerCloserOnEventSelection || currentSelectedEvent == null || isFollowMode)
        {
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        ResolveXrOriginTransform();
        if (xrOriginTransform == null) return;

        if (!TryGetSelectedEventCenter(timeIndex, out Vector3 eventCenter)) return;

        Vector3 viewingDirection = mainCamera.transform.forward.normalized;
        if (viewingDirection.sqrMagnitude < 0.0001f)
        {
            viewingDirection = Vector3.forward;
        }

        Vector3 desiredCameraPosition =
            eventCenter - viewingDirection * Mathf.Max(0.1f, eventTargetViewDistance);
        Vector3 rigTranslation = desiredCameraPosition - mainCamera.transform.position;

        xrOriginTransform.position += rigTranslation;

        if (moveMainCanvasWithPlayer
            && mainCanvas != null
            && !mainCanvas.transform.IsChildOf(xrOriginTransform))
        {
            mainCanvas.transform.position += rigTranslation;
        }

        if (scaleBarRoot != null && !scaleBarRoot.transform.IsChildOf(xrOriginTransform))
        {
            scaleBarRoot.transform.position += rigTranslation;
        }
    }

    bool TryGetSelectedEventCenter(int timeIndex, out Vector3 eventCenter)
    {
        eventCenter = Vector3.zero;
        if (currentSelectedEvent == null || sortedTimeKeys.Count == 0) return false;

        int clampedIndex = Mathf.Clamp(timeIndex, 0, sortedTimeKeys.Count - 1);
        float currentTime = sortedTimeKeys[clampedIndex];
        List<BodyData> frameData = timeDataMap[currentTime];

        List<Vector3> eventPositions = frameData
            .Where(body => ArrayUtility.IndexOf(currentSelectedEvent.ids, body.id) >= 0)
            .Select(body => body.position - GetDisplayedPositionReference(currentTime, Vector3.zero))
            .ToList();

        if (eventPositions.Count == 0) return false;

        Bounds eventBounds = new Bounds(eventPositions[0], Vector3.zero);
        for (int i = 1; i < eventPositions.Count; i++)
        {
            eventBounds.Encapsulate(eventPositions[i]);
        }

        eventCenter = eventBounds.center;
        return true;
    }

    public void StepForward()
    {
        if (timeSlider != null && timeSlider.value < timeSlider.maxValue)
        {
            timeSlider.value += 1;
        }
    }

    public void StepBackward()
    {
        if (timeSlider != null && timeSlider.value > timeSlider.minValue)
        {
            timeSlider.value -= 1;
        }
    }

    public void ResetView()
    {
        if (xrOriginTransform != null)
        {
            xrOriginTransform.position = resetXrOriginPosition;
        }

        if (mainCanvas != null && hasResetMainCanvasPose)
        {
            mainCanvas.transform.SetPositionAndRotation(
                resetMainCanvasPosition,
                resetMainCanvasRotation
            );
        }

        if (scaleBarRoot != null && TryGetInitialBlackHoleBounds(out Bounds bounds))
        {
            PositionScaleBar(bounds);
        }
    }

    void UpdateVisuals(int timeIndex)
    {
        if (sortedTimeKeys.Count == 0 || timeIndex >= sortedTimeKeys.Count) return;
        float currentTime = sortedTimeKeys[timeIndex];
        List<BodyData> currentFrameData = timeDataMap[currentTime];

        if (isFollowMode && followBinaryCenterOfMass)
        {
            if (!TryGetSelectedBinaryCenterOfMass(currentTime, out BodyData binaryCenter))
            {
                ExitFollowMode(false);
            }
        }
        else if (isFollowMode && !currentFrameData.Any(body => body.id == followedBhId))
        {
            if (TryGetMergeSurvivor(currentTime, currentFrameData, out BodyData survivor))
            {
                followedBhId = survivor.id;
                UpdateFollowModeUI();
            }
            else
            {
                ExitFollowMode(false);
            }
        }

        if (followBhDropdown != null && followBhDropdown.gameObject.activeSelf)
        {
            RefreshFollowDropdown(currentFrameData);
        }

        if (timeLabel != null && !hasError)
        {
            timeLabel.text = $"Time: {GetDisplayTimeMyr(currentTime):F2} Myr";
        }
        
        HashSet<int> currentIds = new HashSet<int>();
        bool isEventMode = (currentSelectedEvent != null);
        HashSet<int> eventIds = isEventMode ? new HashSet<int>(currentSelectedEvent.ids) : null;
        
        foreach (var data in currentFrameData)
        {
            currentIds.Add(data.id);
            float currentScale = data.mass * scaleMultiplier;

            if (!activeObjects.ContainsKey(data.id))
            {
                GameObject newObj = Instantiate(spherePrefab, transform);
                newObj.name = $"BH_{data.id}";
                
                LineRenderer newLr = newObj.AddComponent<LineRenderer>();
                newLr.startWidth = 0.05f * vectorScale; 
                newLr.endWidth = 0.0f;
                newLr.material = new Material(Shader.Find("Sprites/Default"));
                newLr.enabled = false;

                GameObject orbitObj = new GameObject($"Orbit_{data.id}");
                orbitObj.transform.SetParent(transform);
                LineRenderer orbitLr = orbitObj.AddComponent<LineRenderer>();
                orbitLr.useWorldSpace = true; 
                orbitLr.material = new Material(Shader.Find("Sprites/Default"));
                orbitLr.enabled = false;
                
                activeOrbits.Add(data.id, orbitLr);
                activeObjects.Add(data.id, newObj);

                GameObject labelObj = new GameObject($"Label_{data.id}");
                labelObj.transform.SetParent(transform);
                TextMesh tm = labelObj.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = (int)labelFontSize;
                tm.characterSize = labelCharacterSize;
                tm.color = Color.white;
                
                LineRenderer calloutLr = labelObj.AddComponent<LineRenderer>();
                calloutLr.material = new Material(Shader.Find("Sprites/Default"));
                calloutLr.positionCount = 2;
                
                labelObj.SetActive(false);
                activeLabels.Add(data.id, labelObj);
            }
            
            GameObject obj = activeObjects[data.id];
            
            Renderer rend = obj.GetComponent<Renderer>();
            LineRenderer lr = obj.GetComponent<LineRenderer>();
            bool isHiddenFollowTarget = isFollowMode && hideFollowTarget && data.id == followedBhId;

            if (rend != null)
            {
                rend.enabled = !isHiddenFollowTarget;
            }

            if (isEventMode)
            {
                if (eventIds.Contains(data.id))
                {
                    bool isPostMerge = (currentSelectedEvent.type == "MERGE" && currentTime >= currentSelectedEvent.time);
                    Color targetColor;

                    if (isPostMerge) 
                    { 
                        targetColor = postMergeColor; 
                    }
                    else 
                    {
                        if (ArrayUtility.IndexOf(currentSelectedEvent.ids, data.id) < 2) targetColor = binaryColor;
                        else targetColor = interloperColor;
                    }

                    rend.material.color = targetColor; 
                    lr.startColor = Color.cyan; 
                    lr.endColor = Color.blue;
                    lr.enabled = showVelocityArrows;
                    lr.positionCount = showVelocityArrows ? 2 : 0;

                    if (activeOrbits.TryGetValue(data.id, out LineRenderer orbitLr))
                    {
                        orbitLr.enabled = true;
                        
                        float midSize = (sizeSliderMin + sizeSliderMax) / 2f;
                        float sizeRatio = scaleMultiplier / midSize;
                        orbitLr.startWidth = orbitLineWidth * vectorScale * sizeRatio;
                        orbitLr.endWidth = orbitLineWidth * vectorScale * sizeRatio;

                        float totalFrames = orbitPastFrames + orbitFutureFrames;
                        float currentRatio = totalFrames > 0 ? (float)orbitPastFrames / totalFrames : 0.5f;

                        Gradient gradient = new Gradient();
                        gradient.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(targetColor, 0.0f), new GradientColorKey(targetColor, 1.0f) },
                            new GradientAlphaKey[] { 
                                new GradientAlphaKey(0.0f, 0.0f),          
                                new GradientAlphaKey(1.0f, currentRatio),  
                                new GradientAlphaKey(0.0f, 1.0f)           
                            }
                        );
                        orbitLr.colorGradient = gradient;
                    }
                }
                else 
                { 
                    rend.material.color = dimColor; 
                    lr.enabled = false; 
                    lr.positionCount = 0; 
                    if (activeOrbits.TryGetValue(data.id, out LineRenderer orbitLr)) orbitLr.enabled = false;
                }
            }
            else 
            { 
                rend.material.color = normalColor; 
                lr.enabled = false; 
                lr.positionCount = 0;
                if (activeOrbits.TryGetValue(data.id, out LineRenderer orbitLr)) orbitLr.enabled = false;
            }

            if (activeLabels.TryGetValue(data.id, out GameObject labelObject))
            {
                if (isEventMode && eventIds.Contains(data.id))
                {
                    TextMesh tm = labelObject.GetComponent<TextMesh>();
                    LineRenderer calloutLr = labelObject.GetComponent<LineRenderer>();
                    
                    if (isScienceMode)
                    {
                        float speed = GetDisplayedVelocity(data, currentTime).magnitude;
                        tm.text = $"ID {data.id}\n{data.mass:F1} {solarMassUnit}\n|v| = {speed:F2} {velocityUnit}";
                    }
                    else
                    {
                        tm.text = $"{data.mass:F1} {solarMassUnit}";
                    }

                    tm.characterSize = labelCharacterSize;
                    
                    Color c = Color.white;
                    bool isPostMerge = (currentSelectedEvent.type == "MERGE" && currentTime >= currentSelectedEvent.time);
                    if (isPostMerge) c = postMergeColor;
                    else
                    {
                        if (ArrayUtility.IndexOf(currentSelectedEvent.ids, data.id) < 2) c = binaryColor;
                        else c = interloperColor;
                    }
                    
                    tm.color = c;
                    if (calloutLr != null && showCalloutLine)
                    {
                        // --- 引き出し線のアップデート ---
                        // 極細の太さを適用
                        calloutLr.startWidth = calloutLineWidth * vectorScale;
                        calloutLr.endWidth = calloutLineWidth * vectorScale;

                        // BH側を「透明（Alpha = 0）」、ラベル側を「半透明（Alpha = 0.8）」にするグラデーション
                        Gradient calloutGrad = new Gradient();
                        calloutGrad.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(c, 0.0f), new GradientColorKey(c, 1.0f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.8f, 1.0f) }
                        );
                        calloutLr.colorGradient = calloutGrad;
                        calloutLr.startColor = Color.white; // colorGradientを使う場合はstart/endColorより優先される
                    }
                    
                    labelObject.SetActive(true);
                }
                else
                {
                    labelObject.SetActive(false);
                }
            }

            if (isHiddenFollowTarget)
            {
                if (lr != null)
                {
                    lr.enabled = false;
                    lr.positionCount = 0;
                }

                if (activeOrbits.TryGetValue(data.id, out LineRenderer targetOrbit))
                {
                    targetOrbit.enabled = false;
                }

                if (activeLabels.TryGetValue(data.id, out GameObject targetLabel))
                {
                    targetLabel.SetActive(false);
                }
            }
        }
        foreach (var kvp in activeObjects) { kvp.Value.SetActive(currentIds.Contains(kvp.Key)); }
        foreach (var kvp in activeLabels)
        {
            bool showLabel = currentIds.Contains(kvp.Key)
                && isEventMode
                && eventIds != null
                && eventIds.Contains(kvp.Key)
                && !(isFollowMode && hideFollowTarget && kvp.Key == followedBhId);
            kvp.Value.SetActive(showLabel);
        }
        UpdateScienceDisplay(currentTime, currentFrameData);
    }
    public static class ArrayUtility
    {
        public static int IndexOf(int[] array, int value) { for(int i=0; i<array.Length; i++) if(array[i] == value) return i; return -1; }
    }
}
