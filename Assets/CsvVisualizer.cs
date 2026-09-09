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
    public int[] temporaryIds = new int[0];
    public List<int[]> configurations = new List<int[]>();

    public int DiagnosticCount
    {
        get { return configurations != null && configurations.Count > 0 ? configurations.Count : 1; }
    }

    public bool IsTransientExchange
    {
        get { return type == "TRANSIENT EXCHANGE"; }
    }

    public bool IsGroupedStrongInteraction
    {
        get { return type == "EXCHANGE" && DiagnosticCount > 1; }
    }

    public int[] AllIds
    {
        get
        {
            IEnumerable<int> configuredIds = configurations == null || configurations.Count == 0
                ? ids ?? new int[0]
                : configurations.SelectMany(configuration => configuration);
            return configuredIds.Concat(temporaryIds ?? new int[0]).Distinct().ToArray();
        }
    }

    public bool IsTemporaryParticipant(int id)
    {
        return temporaryIds != null && temporaryIds.Contains(id);
    }
}

public class CsvVisualizer : MonoBehaviour
{
    [System.Serializable]
    public class DatasetManifest
    {
        public int format_version;
        public string dataset_name;
        public string history_file;
        public string events_file;
        public string science_events_file;
        public string science_cluster_file;
        public string time_unit;
        public string length_unit;
        public string mass_unit;
        public string velocity_unit;
    }

    public Dropdown datasetDropdown;
    string datasetRoot;
    bool datasetRejected;

    IEnumerator InitializeDataset()
    {
        string root = Path.Combine(Application.persistentDataPath, "datasets");
        List<string> detectedDatasets = new List<string>();
        if (Directory.Exists(root))
        {
            foreach (string folder in Directory.GetDirectories(root).OrderBy(path => path))
            {
                if (folder.Contains("_backup_") || folder.EndsWith("_uploading")) continue;
                if (!File.Exists(Path.Combine(folder, "manifest.json"))) continue;
                detectedDatasets.Add(folder);
            }
        }

        datasetRoot = detectedDatasets.FirstOrDefault() ?? "";
        if (datasetDropdown != null)
        {
            datasetDropdown.gameObject.SetActive(false);
        }
        if (string.IsNullOrEmpty(datasetRoot))
        {
            string legacyHistory = Path.Combine(Application.persistentDataPath, historyFile);
            if (File.Exists(legacyHistory))
            {
                datasetRoot = Application.persistentDataPath;
                yield break;
            }

            string sample = Path.Combine(Application.streamingAssetsPath, "NBODYVR_Data");
            string sampleManifest = Path.Combine(sample, "manifest.json");
            if (!sampleManifest.Contains("://"))
            {
                if (File.Exists(sampleManifest))
                {
                    datasetRoot = sample;
                    ApplyDatasetManifest(File.ReadAllText(sampleManifest));
                }
            }
            else using (UnityWebRequest request = UnityWebRequest.Get(sampleManifest))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    datasetRoot = sample;
                    ApplyDatasetManifest(request.downloadHandler.text);
                }
            }
            if (string.IsNullOrEmpty(datasetRoot))
                datasetRoot = File.Exists(Path.Combine(Application.persistentDataPath, historyFile))
                    ? Application.persistentDataPath : Application.streamingAssetsPath;
        }
        else
        {
            string manifestPath = Path.Combine(datasetRoot, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try { ApplyDatasetManifest(File.ReadAllText(manifestPath)); }
                catch (System.Exception error) { datasetRejected = true; ShowError("Dataset: " + error.Message); }
            }
        }
    }

    void ApplyDatasetManifest(string text)
    {
        try
        {
            DatasetManifest manifest = JsonUtility.FromJson<DatasetManifest>(text);
            if (manifest == null || manifest.format_version != 1)
                throw new System.Exception("Unsupported dataset format. Update the app or regenerate the dataset.");
            if (manifest.time_unit != "Myr" || manifest.length_unit != "pc" || manifest.velocity_unit != "km/s" || manifest.mass_unit != "M_sun")
                throw new System.Exception("Unsupported dataset units.");
            string[] files = { manifest.history_file, manifest.events_file, manifest.science_events_file, manifest.science_cluster_file };
            foreach (string file in files)
                if (!string.IsNullOrEmpty(file) && (file.Contains("/") || file.Contains("\\") || file.Contains(":") || file == ".."))
                    throw new System.Exception("Dataset filenames must be basenames.");
            if (string.IsNullOrEmpty(manifest.history_file)) throw new System.Exception("Missing history_file in manifest.");
            historyFile = manifest.history_file;
            eventsFile = manifest.events_file;
            scienceExchangeFile = manifest.science_events_file;
            scienceClusterFile = manifest.science_cluster_file;
            playbackTimeIsNbody = false;
        }
        catch (System.Exception error) { datasetRejected = true; ShowError("Dataset: " + error.Message); }
    }
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
    public Color transientExchangeColor = new Color(1.0f, 0.55f, 0.25f, 1.0f);
    public Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.05f);

    [Header("Label & Callout Settings")]
    public float labelFontSize = 100f;
    public float labelCharacterSize = 0.02f;
    public float labelHeightOffset = 1.0f;
    public float labelSpreadX = 1.5f;
    public float labelSpreadY = 0.5f;
    [Min(0.1f)]
    public float calloutDistanceMultiplier = 1.35f;
    [Min(1.0f)]
    public float scienceLabelDistanceMultiplier = 1.25f;
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
    public bool playbackTimeIsNbody = false;
    [Min(0.0001f)]
    public float scienceEventTimeTolerance = 0.05f;
    public bool groupSimultaneousExchangeDiagnostics = true;
    [Min(0.0f)]
    public float exchangeGroupingTimeTolerance = 0.001f;
    [Min(0.0f)]
    public float scienceCompleteSnapshotSearchWindow = 50f;
    [Range(0.001f, 0.2f)]
    public float ksCompositeMassTolerance = 0.02f;

    [Header("Science Panel Layout")]
    public bool positionSciencePanelAboveUi = true;
    public Vector2 sciencePanelTopOffset = new Vector2(0f, 20f);
    public Vector2 sciencePanelSize = new Vector2(760f, 300f);

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
    private bool followPostExchangeBinary = false;
    private int followedBhId = -1;
    private const int FollowPreBinarySentinel = int.MinValue;
    private const int FollowPostBinarySentinel = int.MinValue + 1;
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
    private KsCompositeState activeKsCompositeState;
    private InteractionEvent recoveredExchangeCacheEvent;
    private RecoveredExchange recoveredExchangeCache;
    private bool recoveredExchangeCacheAttempted;

    private sealed class RecoveredExchangeFrame
    {
        public float Time;
        public Dictionary<int, BodyData> Bodies;
    }

    private sealed class RecoveredExchange
    {
        public int[] Configuration;
        public RecoveredExchangeFrame Before;
        public RecoveredExchangeFrame After;
    }

    private sealed class KsCompositeState
    {
        public int CompositeId;
        public int MissingId;
        public float CompositeComponentMass;
        public float MissingComponentMass;
        public float StoredCompositeMass;
        public BodyData CenterOfMassBody;
    }
    
    [HideInInspector]
    public float stepsPerSecond = 1.0f; 

    IEnumerator Start()
    {
        lastShowVelocityArrows = showVelocityArrows;
        mainCamera = Camera.main;
        SetupHUD();
        yield return StartCoroutine(InitializeDataset());
        if (datasetRejected) yield break;

        string[] historyLines = null;
        yield return StartCoroutine(LoadOptionalDataFile(historyFile, lines => historyLines = lines));
        
        if (historyLines == null || historyLines.Length == 0)
        {
            if (!hasError) ShowError($"Data missing or unreadable:\n{historyFile}");
            yield break;
        }

        LoadHistoryData(historyLines);
        if (sortedTimeKeys.Count == 0)
        {
            ShowError("Dataset contains no readable BH frames.");
            yield break;
        }

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
        if (scienceModeButton != null && scienceDataStore.ClusterCount == 0 && scienceDataStore.ExchangeCount == 0)
        {
            scienceModeButton.interactable = false;
            isScienceMode = false;
        }
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
        if (string.IsNullOrEmpty(fileName)) { onLoaded?.Invoke(null); yield break; }
        if (!string.IsNullOrEmpty(datasetRoot))
        {
            string path = Path.Combine(datasetRoot, fileName);
            if (!path.Contains("://"))
            {
                if (File.Exists(path)) yield return StartCoroutine(LoadFileSafe(path, onLoaded));
                else onLoaded?.Invoke(null);
            }
            else
            {
                using (UnityWebRequest request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    onLoaded?.Invoke(request.result == UnityWebRequest.Result.Success
                        ? request.downloadHandler.text.Replace("\r\n", "\n").Split('\n') : null);
                }
            }
            yield break;
        }
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

    void ApplySciencePanelLayout()
    {
        if (!positionSciencePanelAboveUi || scienceDataPanel == null) return;

        RectTransform panelRect = scienceDataPanel.GetComponent<RectTransform>();
        if (panelRect == null) return;

        panelRect.anchorMin = new Vector2(0.5f, 1.0f);
        panelRect.anchorMax = new Vector2(0.5f, 1.0f);
        panelRect.pivot = new Vector2(0.5f, 0.0f);
        panelRect.anchoredPosition = sciencePanelTopOffset;
        panelRect.sizeDelta = sciencePanelSize;
        panelRect.SetAsLastSibling();
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

        Dictionary<int, BodyData> rawCurrentDataDict = new Dictionary<int, BodyData>();
        foreach (var d in currentData) rawCurrentDataDict[d.id] = d;
        Dictionary<int, BodyData> rawNextDataDict = new Dictionary<int, BodyData>();
        foreach (var d in nextData) rawNextDataDict[d.id] = d;
        TryDetectKsComposite(currentTime, rawCurrentDataDict, out KsCompositeState currentKsState);
        TryDetectKsComposite(nextTime, rawNextDataDict, out KsCompositeState nextKsState);
        currentData = AddMissingKsProxy(currentData, currentKsState);
        nextData = AddMissingKsProxy(nextData, nextKsState);
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

                BodyData displayCurrent = GetKsDisplayBody(cData, currentKsState);
                Vector3 startPos = displayCurrent.position - currentReference;
                float startScale = displayCurrent.mass * scaleMultiplier;
                Vector3 startVel = displayCurrent.velocity - currentVelocityReference;

                Vector3 endPos = startPos;
                float endScale = startScale;
                Vector3 endVel = startVel;

                if (nextDataDict.TryGetValue(cData.id, out BodyData nData))
                {
                    BodyData displayNext = GetKsDisplayBody(nData, nextKsState);
                    endPos = displayNext.position - nextReference;
                    endScale = displayNext.mass * scaleMultiplier;
                    endVel = displayNext.velocity - nextVelocityReference;
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
                        int[] allEventIds = currentSelectedEvent.AllIds;
                        int eventIndex = ArrayUtility.IndexOf(allEventIds, cData.id);
                        int totalIds = allEventIds.Length;

                        if (eventIndex != -1)
                        {
                            Vector3 camRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;
                            Vector3 camUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;

                            float midSize = (sizeSliderMin + sizeSliderMax) / 2f;
                            float sizeRatio = scaleMultiplier / midSize;

                            float scienceDistanceMultiplier = isScienceMode
                                ? Mathf.Max(1.0f, scienceLabelDistanceMultiplier)
                                : 1.0f;
                            float distanceMultiplier = Mathf.Max(0.1f, calloutDistanceMultiplier) * scienceDistanceMultiplier;
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
                eventHudText.color = currentSelectedEvent.IsTransientExchange
                    ? transientExchangeColor
                    : Color.yellow;
            }
            else
            {
                eventHudText.text = currentSelectedEvent.IsTransientExchange
                    ? "TRANSIENT EXCHANGE\nDETECTED"
                    : $"{currentSelectedEvent.type}\nOCCURRED!";
                eventHudText.color = currentSelectedEvent.IsTransientExchange
                    ? transientExchangeColor
                    : postMergeColor;
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
        if (activeKsCompositeState == null)
        {
            TryDetectKsComposite(currentTime, bodyById, out activeKsCompositeState);
        }

        for (int i = 0; i < sciencePairLines.Count; i++)
        {
            SetSciencePairVisible(i, false);
        }

        if (currentSelectedEvent.ids.Length >= 2)
        {
            string firstPairName = currentSelectedEvent.type == "EXCHANGE"
                ? "PRE-event binary"
                : (currentSelectedEvent.IsTransientExchange ? "Stable binary (before = after)" : "Pair");
            Color firstPairColor = currentSelectedEvent.IsTransientExchange
                ? transientExchangeColor
                : scienceBeforePairColor;
            UpdateSciencePairVisual(
                0,
                currentSelectedEvent.ids[0],
                currentSelectedEvent.ids[1],
                firstPairName,
                firstPairColor,
                bodyById
            );
        }

        if (currentSelectedEvent.type == "EXCHANGE" && currentSelectedEvent.ids.Length >= 4)
        {
            UpdateSciencePairVisual(
                1,
                currentSelectedEvent.ids[2],
                currentSelectedEvent.ids[3],
                "POST-event binary",
                scienceAfterPairColor,
                bodyById
            );
        }

        string scienceText = BuildScienceDataText(currentTime, bodyById);
        if (scienceDataTmpText != null)
        {
            scienceDataTmpText.richText = true;
            scienceDataTmpText.text = scienceText;
        }
        else if (scienceDataText != null)
        {
            scienceDataText.text = StripRichText(scienceText);
        }
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

        bool isCompositePair = activeKsCompositeState != null &&
            PairContains(idA, idB, activeKsCompositeState.CompositeId, activeKsCompositeState.MissingId);
        label.text = isCompositePair
            ? $"{pairName}: KS/CHAIN CoM proxy"
            : $"{pairName}: r = {separation:F3} {distanceUnit}";
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

    string GetPlainSolarMassUnit()
    {
        return "M☉";
    }

    string GetScienceSolarMassUnit()
    {
        bool fontSupportsOdot = scienceDataTmpText != null
            && scienceDataTmpText.font != null
            && scienceDataTmpText.font.HasCharacter('⊙');
        return fontSupportsOdot
            ? "M<sub>⊙</sub>"
            : "M<sub>sun</sub>";
    }

    string StripRichText(string richText)
    {
        if (string.IsNullOrEmpty(richText)) return richText;

        System.Text.StringBuilder plain = new System.Text.StringBuilder(richText.Length);
        bool insideTag = false;
        foreach (char character in richText)
        {
            if (character == '<')
            {
                insideTag = true;
                continue;
            }
            if (character == '>' && insideTag)
            {
                insideTag = false;
                continue;
            }
            if (!insideTag) plain.Append(character);
        }
        return plain.ToString();
    }

    double GetDisplayTimeMyr(double playbackTime)
    {
        if (!playbackTimeIsNbody) return playbackTime;
        double? converted = scienceDataStore.ConvertNbodyToMyr(playbackTime);
        return converted ?? playbackTime;
    }

    bool TryDetectKsComposite(
        float currentTime,
        Dictionary<int, BodyData> bodyById,
        out KsCompositeState state)
    {
        state = null;
        if (currentSelectedEvent == null || currentSelectedEvent.type != "EXCHANGE") return false;
        if (!TryRecoverExchange(currentSelectedEvent, out RecoveredExchange recovered)) return false;

        RecoveredExchangeFrame reference = new[] { recovered.Before, recovered.After }
            .Where(frame => frame != null)
            .OrderBy(frame => Mathf.Abs(frame.Time - currentTime))
            .FirstOrDefault();
        if (reference == null) return false;

        int[] participantIds = currentSelectedEvent.AllIds;
        foreach (int missingId in participantIds)
        {
            bool missing = !bodyById.TryGetValue(missingId, out BodyData missingBody) || !IsUsableBody(missingBody);
            if (!missing || !reference.Bodies.TryGetValue(missingId, out BodyData missingReference)) continue;

            foreach (int compositeId in participantIds)
            {
                if (compositeId == missingId || !ArePairedInExchange(currentSelectedEvent, compositeId, missingId)) continue;
                if (!bodyById.TryGetValue(compositeId, out BodyData compositeBody) || !IsUsableBody(compositeBody)) continue;
                if (!reference.Bodies.TryGetValue(compositeId, out BodyData compositeReference)) continue;

                float expectedTotal = compositeReference.mass + missingReference.mass;
                if (expectedTotal <= 0f) continue;
                float relativeDifference = Mathf.Abs(compositeBody.mass - expectedTotal) / expectedTotal;
                if (relativeDifference > ksCompositeMassTolerance) continue;

                state = new KsCompositeState
                {
                    CompositeId = compositeId,
                    MissingId = missingId,
                    CompositeComponentMass = compositeReference.mass,
                    MissingComponentMass = missingReference.mass,
                    StoredCompositeMass = compositeBody.mass,
                    CenterOfMassBody = compositeBody
                };
                return true;
            }
        }
        return false;
    }

    bool ArePairedInExchange(InteractionEvent evt, int idA, int idB)
    {
        IEnumerable<int[]> configurations = evt.configurations != null && evt.configurations.Count > 0
            ? evt.configurations
            : new[] { evt.ids };
        foreach (int[] configuration in configurations)
        {
            if (configuration == null) continue;
            if (configuration.Length >= 2 && PairContains(configuration[0], configuration[1], idA, idB)) return true;
            if (configuration.Length >= 4 && PairContains(configuration[2], configuration[3], idA, idB)) return true;
        }
        return false;
    }

    bool PairContains(int first, int second, int idA, int idB)
    {
        return (first == idA && second == idB) || (first == idB && second == idA);
    }

    BodyData GetKsDisplayBody(BodyData source, KsCompositeState state)
    {
        if (source == null || state == null) return source;
        if (source.id != state.CompositeId && source.id != state.MissingId) return source;
        float mass = source.id == state.CompositeId
            ? state.CompositeComponentMass
            : state.MissingComponentMass;
        return new BodyData
        {
            time = source.time,
            id = source.id,
            mass = mass,
            position = state.CenterOfMassBody.position,
            velocity = state.CenterOfMassBody.velocity
        };
    }

    List<BodyData> AddMissingKsProxy(List<BodyData> source, KsCompositeState state)
    {
        if (state == null || source.Any(body => body.id == state.MissingId)) return source;
        List<BodyData> result = new List<BodyData>(source);
        result.Add(new BodyData
        {
            time = state.CenterOfMassBody.time,
            id = state.MissingId,
            mass = state.MissingComponentMass,
            position = state.CenterOfMassBody.position,
            velocity = state.CenterOfMassBody.velocity
        });
        return result;
    }

    void AppendKsCompositeStatus(List<string> lines, KsCompositeState state)
    {
        if (state == null) return;
        lines.Add("<color=#FFB86C><b>KS/CHAIN BINARY COMPOSITE</b></color>");
        lines.Add($"Stored ID {state.CompositeId}: {state.StoredCompositeMass:F2} {GetScienceSolarMassUnit()} = {state.CompositeComponentMass:F2} + {state.MissingComponentMass:F2}");
        lines.Add($"IDs {state.CompositeId} and {state.MissingId} are displayed at one CoM proxy; separate position and velocity are unavailable in this frame.");
        lines.Add("This is not classified as a MERGE.");
    }

    string BuildRecoveredExchangeScienceDataText(
        float currentTime,
        Dictionary<int, BodyData> bodyById)
    {
        List<string> lines = new List<string>();
        string title = currentSelectedEvent.IsGroupedStrongInteraction
            ? $"STRONG INTERACTION — {currentSelectedEvent.DiagnosticCount} EXCHANGE diagnostics"
            : "EXCHANGE";
        lines.Add($"<b><color=#67D5FF>SCIENCE MODE — {title}</color></b>");
        lines.Add($"Event: <i>t</i> = {GetDisplayTimeMyr(currentSelectedEvent.time):F3} Myr");
        lines.Add($"Current playback: <i>t</i> = {GetDisplayTimeMyr(currentTime):F3} Myr");
        lines.Add("<color=#FFB86C>Detailed nearest-HDF5 physics is unavailable; complete bh_history frames are used below.</color>");
        lines.Add("");

        KsCompositeState ksState = activeKsCompositeState;
        if (ksState == null) TryDetectKsComposite(currentTime, bodyById, out ksState);
        AppendKsCompositeStatus(lines, ksState);
        if (ksState != null) lines.Add("");

        if (!TryRecoverExchange(currentSelectedEvent, out RecoveredExchange recovered))
        {
            lines.Add("No complete participant frame was found inside the configured search window.");
            lines.Add("Increase Science Complete Snapshot Search Window only if a larger temporal offset is scientifically acceptable.");
            return string.Join("\n", lines);
        }

        lines.Add($"Recovered diagnostic: {recovered.Configuration[0]}+{recovered.Configuration[1]} → {recovered.Configuration[2]}+{recovered.Configuration[3]}");
        lines.Add("");

        AppendRecoveredExchangeFrame(lines, recovered, true);
        lines.Add("");
        AppendRecoveredExchangeFrame(lines, recovered, false);
        lines.Add("");
        lines.Add("<size=80%>Recovered values are sampled, not interpolated. Orbital a/e, binding energy and local escape speed remain unavailable without the detailed HDF5 record.</size>");
        return string.Join("\n", lines);
    }

    void AppendRecoveredExchangeFrame(List<string> lines, RecoveredExchange recovered, bool before)
    {
        RecoveredExchangeFrame frame = before ? recovered.Before : recovered.After;
        string color = before ? "#FF8CE8" : "#72E5E5";
        string heading = before ? "PRE-EVENT COMPLETE FRAME — RECOVERED" : "POST-EVENT COMPLETE FRAME — RECOVERED";
        lines.Add($"<b><color={color}>{heading}</color></b>");
        if (frame == null)
        {
            lines.Add("No complete participant frame found inside the search window.");
            return;
        }

        int[] ids = recovered.Configuration;
        int pairId1 = before ? ids[0] : ids[2];
        int pairId2 = before ? ids[1] : ids[3];
        int singleId = before
            ? ids.Skip(2).Take(2).FirstOrDefault(id => id != ids[0] && id != ids[1])
            : ids.Take(2).FirstOrDefault(id => id != ids[2] && id != ids[3]);
        double lagMyr = System.Math.Abs(GetDisplayTimeMyr(frame.Time) - GetDisplayTimeMyr(currentSelectedEvent.time));
        lines.Add($"Snapshot: <i>t</i> = {GetDisplayTimeMyr(frame.Time):F3} Myr; event offset = {lagMyr:F3} Myr");
        lines.Add($"Binary: ID {pairId1} + {pairId2}; {(before ? "incoming" : "outgoing")} ID {singleId}");

        BodyData first = frame.Bodies[pairId1];
        BodyData second = frame.Bodies[pairId2];
        BodyData single = frame.Bodies.ContainsKey(singleId) ? frame.Bodies[singleId] : null;
        lines.Add($"Masses: {first.mass:F2} + {second.mass:F2}; {(before ? "incoming" : "outgoing")} {(single != null ? single.mass.ToString("F2", CultureInfo.InvariantCulture) : "—")} {GetScienceSolarMassUnit()}");

        ClusterScienceRecord cluster = scienceDataStore.FindNearestCluster(frame.Time, playbackTimeIsNbody);
        Vector3 clusterVelocity = cluster != null && cluster.QualityOk
            ? cluster.CenterVelocityKms
            : Vector3.zero;
        float totalMass = first.mass + second.mass;
        Vector3 binaryVelocity = totalMass > 0f
            ? (first.velocity * first.mass + second.velocity * second.mass) / totalMass
            : Vector3.zero;
        string clusterQualifier = cluster != null && cluster.QualityOk ? "cluster CoM" : "raw frame";
        lines.Add($"Binary |<i>v</i>| = {(binaryVelocity - clusterVelocity).magnitude:F3} {velocityUnit}; {(before ? "incoming" : "outgoing")} |<i>v</i>| = {(single != null ? (single.velocity - clusterVelocity).magnitude.ToString("F3", CultureInfo.InvariantCulture) : "—")} {velocityUnit} ({clusterQualifier})");
        float separation = Vector3.Distance(first.position, second.position);
        float relativeSpeed = (second.velocity - first.velocity).magnitude;
        lines.Add($"Component separation <i>r</i> = {separation:F3} {distanceUnit}; |<i>v</i><sub>rel</sub>| = {relativeSpeed:F3} {velocityUnit}");
        if (separation <= 0.000001f)
        {
            lines.Add("The component coordinates are coincident and represent a binary CoM proxy, not physical contact.");
        }
    }

    string BuildTransientExchangeScienceDataText(
        float currentTime,
        Dictionary<int, BodyData> bodyById)
    {
        List<string> lines = new List<string>();
        double eventTimeMyr = GetDisplayTimeMyr(currentSelectedEvent.time);
        double currentTimeMyr = GetDisplayTimeMyr(currentTime);
        string frameName = isFollowMode
            ? (followBinaryCenterOfMass ? "Follow stable binary CoM" : $"Follow BH {followedBhId}")
            : (useCenterOfMass ? "Cluster CoM" : "Raw");

        lines.Add("<b><color=#FF8C40>SCIENCE MODE — TRANSIENT EXCHANGE</color></b>");
        lines.Add($"Event: <i>t</i> = {eventTimeMyr:F3} Myr");
        lines.Add($"Current playback: <i>t</i> = {currentTimeMyr:F3} Myr");
        lines.Add($"View frame: {frameName}; reported speeds: cluster CoM");
        lines.Add("");
        lines.Add("<color=#FFB86C><b>Temporary exchange; no net membership change</b></color>");

        if (currentSelectedEvent.ids != null && currentSelectedEvent.ids.Length >= 4)
        {
            lines.Add($"Initial binary: ID {currentSelectedEvent.ids[0]} + {currentSelectedEvent.ids[1]}");
            lines.Add($"Final binary: ID {currentSelectedEvent.ids[2]} + {currentSelectedEvent.ids[3]}");
        }
        else if (currentSelectedEvent.ids != null && currentSelectedEvent.ids.Length >= 2)
        {
            lines.Add($"Stable binary: ID {currentSelectedEvent.ids[0]} + {currentSelectedEvent.ids[1]}");
        }

        if (currentSelectedEvent.temporaryIds != null && currentSelectedEvent.temporaryIds.Length > 0)
        {
            string interloperLabel = currentSelectedEvent.temporaryIds.Length == 1 ? "Interloper" : "Interlopers";
            lines.Add($"{interloperLabel}: " + string.Join(", ", currentSelectedEvent.temporaryIds.Select(id => $"ID {id}")));
        }
        else
        {
            lines.Add("Interloper: unavailable in this event CSV");
        }

        lines.Add("The event category is derived from grouped NBODY EXCHANGE diagnostics.");
        lines.Add("No net incoming or outgoing member is defined, so the standard recoil and binding-energy-release analysis is not applied.");
        lines.Add("");
        lines.Add("<b><color=#FFF176>CURRENT PLAYBACK</color></b>");

        ClusterScienceRecord cluster = scienceDataStore.FindNearestCluster(
            currentTime,
            playbackTimeIsNbody
        );
        if (cluster != null && cluster.QualityOk)
        {
            lines.Add($"σ<sub>1D</sub> = {FormatNumber(cluster.Sigma1dKms, "F3")} {velocityUnit}; <i>v</i><sub>rms,3D</sub> = {FormatNumber(cluster.Vrms3dKms, "F3")} {velocityUnit}");
            lines.Add($"Cluster <b>V</b><sub>CoM</sub> = ({cluster.CenterVelocityKms.x:F3}, {cluster.CenterVelocityKms.y:F3}, {cluster.CenterVelocityKms.z:F3}) {velocityUnit}");
        }
        else
        {
            lines.Add("Cluster science data unavailable");
        }

        HashSet<int> displayedIds = new HashSet<int>();
        foreach (int id in currentSelectedEvent.AllIds)
        {
            if (!displayedIds.Add(id)) continue;
            bool isTemporaryInterloper = currentSelectedEvent.IsTemporaryParticipant(id);
            string participantLabel = isTemporaryInterloper ? "Interloper ID" : "Stable binary ID";
            if (bodyById.TryGetValue(id, out BodyData body))
            {
                float speed = GetDisplayedVelocity(body, currentTime).magnitude;
                lines.Add($"{participantLabel} {id}: <i>M</i> = {body.mass:F2} {GetScienceSolarMassUnit()}, |<i>v</i>| = {speed:F3} {velocityUnit}");
            }
            else
            {
                lines.Add($"{participantLabel} {id}: not present in this snapshot");
            }
        }

        if (currentSelectedEvent.ids != null && currentSelectedEvent.ids.Length >= 2)
        {
            lines.Add("");
            AppendPairScienceData(
                lines,
                "Stable binary (before = after)",
                currentSelectedEvent.ids[0],
                currentSelectedEvent.ids[1],
                currentTime,
                bodyById
            );
        }

        return string.Join("\n", lines);
    }

    string BuildScienceDataText(float currentTime, Dictionary<int, BodyData> bodyById)
    {
        if (currentSelectedEvent.IsTransientExchange)
        {
            ScienceExchangeRecord transient = scienceDataStore.FindTransient(
                currentSelectedEvent,
                scienceEventTimeTolerance,
                playbackTimeIsNbody
            );
            if (transient != null)
            {
                return BuildDetailedTransientExchangeScienceDataText(currentTime, transient);
            }
            return BuildTransientExchangeScienceDataText(currentTime, bodyById);
        }

        if (currentSelectedEvent.type == "EXCHANGE")
        {
            ScienceExchangeRecord exchange = scienceDataStore.FindExchange(
                currentSelectedEvent,
                scienceEventTimeTolerance,
                playbackTimeIsNbody
            );
            if (exchange != null)
            {
                return BuildExchangeScienceDataText(currentTime, bodyById, exchange);
            }
            return BuildRecoveredExchangeScienceDataText(currentTime, bodyById);
        }

        if (currentSelectedEvent.type == "MERGE")
        {
            ScienceExchangeRecord merge = scienceDataStore.FindMerge(
                currentSelectedEvent,
                scienceEventTimeTolerance,
                playbackTimeIsNbody
            );
            if (merge != null)
            {
                return BuildMergeScienceDataText(currentTime, merge);
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
                lines.Add($"ID {id}: <i>M</i> = {body.mass:F2} {GetScienceSolarMassUnit()}, |<i>v</i>| = {speed:F3} {velocityUnit}");
            }
            else
            {
                lines.Add($"ID {id}: not present in this snapshot");
            }
        }

        if (currentSelectedEvent.ids.Length >= 2)
        {
            lines.Add("");
            string firstPairName = currentSelectedEvent.type == "EXCHANGE" ? "PRE-event binary" : "Pair";
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
                "POST-event binary",
                currentSelectedEvent.ids[2],
                currentSelectedEvent.ids[3],
                currentTime,
                bodyById
            );
        }

        return string.Join("\n", lines);
    }

    string BuildDetailedTransientExchangeScienceDataText(float currentTime, ScienceExchangeRecord record)
    {
        List<string> lines = new List<string>();
        double currentTimeMyr = GetDisplayTimeMyr(currentTime);
        bool occurring = record.DiagnosticStartTimeMyr.HasValue && record.DiagnosticEndTimeMyr.HasValue &&
            record.DiagnosticEndTimeMyr.Value > record.DiagnosticStartTimeMyr.Value &&
            currentTimeMyr >= record.DiagnosticStartTimeMyr.Value &&
            currentTimeMyr <= record.DiagnosticEndTimeMyr.Value;
        string solarMass = GetScienceSolarMassUnit();
        string title = occurring ? "TRANSIENT EXCHANGE — OCCURRING" : "TRANSIENT EXCHANGE";

        lines.Add($"<b><color=#FF8C40>SCIENCE MODE — {title}</color></b>");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> Event: <i>t</i> = {record.EventTimeMyr:F3} Myr ({record.EventTimeNbody:F3} N-body time)");
        lines.Add($"Current playback: <i>t</i> = {currentTimeMyr:F3} Myr");
        lines.Add("");
        lines.Add("<b><color=#FFF176>NBODY EVENT RECORD</color></b>");
        lines.Add($"Stable binary: ID {record.BeforeId1} + {record.BeforeId2}");
        lines.Add($"Interloper{((record.TemporaryIds?.Length ?? 0) == 1 ? "" : "s")}: {FormatIdList(record.TemporaryIds)}");
        lines.Add($"Diagnostic interval: {FormatNumber(record.DiagnosticStartTimeMyr, "F3")}–{FormatNumber(record.DiagnosticEndTimeMyr, "F3")} Myr; {record.RawDiagnosticCount} record(s)");
        lines.Add($"Recorded sequence: {FormatTransitionSequence(record.RawTransitions)}");
        lines.Add("<color=#FFB86C>The initial and final binary are identical; no net incoming or outgoing member is assigned.</color>");

        AppendScienceSnapshots(lines, record);
        AppendEventLocation(lines, "Stable-binary + interloper-system CoM", record.InteractionComRadiusBeforePc,
            record.InteractionComRadiusAfterPc, record.ClusterHalfMassRadiusBeforePc,
            record.ClusterHalfMassRadiusAfterPc, record.InteractionComHalfMassRatioBefore,
            record.InteractionComHalfMassRatioAfter);

        lines.Add("");
        lines.Add("<b>ORBIT AND BINDING ENERGY</b>");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> <i>a</i>: {FormatNumber(record.SemimajorAxisBeforeAu, "F3")} → {FormatNumber(record.SemimajorAxisAfterAu, "F3")} AU");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> <i>e</i>: {FormatNumber(record.EccentricityBefore, "F3")} → {FormatNumber(record.EccentricityAfter, "F3")}");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> <i>r</i><sub>peri</sub>: {FormatNumber(record.PericenterBeforeAu, "F3")} → {FormatNumber(record.PericenterAfterAu, "F3")} AU");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> |<i>E</i><sub>b</sub>| = <i>Gm</i><sub>1</sub><i>m</i><sub>2</sub>/(2<i>a</i>): {FormatScientific(record.BindingEnergyBefore)} → {FormatScientific(record.BindingEnergyAfter)} {solarMass} (km/s)<sup>2</sup>");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> Δ|<i>E</i><sub>b</sub>|/|<i>E</i><sub>b,pre</sub>| = {FormatSignedFixed(record.BindingEnergyFractionalChange, "F3")}");

        lines.Add("");
        lines.Add("<b>KINEMATICS AND REFERENCES</b>");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> Binary–interloper-system relative speed: {FormatNumber(record.BinarySingleRelativeSpeedBefore, "F3")} → {FormatNumber(record.BinarySingleRelativeSpeedAfter, "F3")} {velocityUnit}");
        AppendReferenceComparison(lines, "PRE stable binary", record.BinarySpeedBefore, record.BinaryEscapeBefore, SafeRatio(record.BinarySpeedBefore, record.SigmaBefore), SafeRatio(record.BinarySpeedBefore, record.BinaryEscapeBefore));
        AppendReferenceComparison(lines, "PRE interloper system", record.SingleSpeedBefore, record.SingleEscapeBefore, SafeRatio(record.SingleSpeedBefore, record.SigmaBefore), SafeRatio(record.SingleSpeedBefore, record.SingleEscapeBefore));
        AppendReferenceComparison(lines, "POST stable binary", record.BinarySpeedAfter, record.BinaryEscapeAfter, record.BinarySigmaRatioAfter, record.BinaryEscapeRatioAfter);
        AppendReferenceComparison(lines, "POST interloper system", record.SingleSpeedAfter, record.SingleEscapeAfter, record.SingleSigmaRatioAfter, record.SingleEscapeRatioAfter);

        lines.Add("");
        lines.Add("<b>DATA SOURCES</b>");
        lines.Add("<color=#B8E8FF>[NBODY LOG]</color> Diagnostic times, binary-member transitions, <i>a</i>, and <i>e</i>.");
        lines.Add("<color=#B8E8FF>[HDF5]</color> Masses and phase space at the nearest complete PRE/POST snapshots.");
        lines.Add("<color=#C9FFBF>[DERIVED]</color> System radii, velocities, binding energy, pericenter, and ratios.");
        lines.Add("<color=#FFD28A>[MODEL]</color> <i>v</i><sub>esc,sph</sub> is a spherical cluster-only reference.");
        return string.Join("\n", lines);
    }

    string BuildMergeScienceDataText(float currentTime, ScienceExchangeRecord record)
    {
        List<string> lines = new List<string>();
        string solarMass = GetScienceSolarMassUnit();
        lines.Add("<b><color=#FF6B7A>SCIENCE MODE — MERGE</color></b>");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> Event: <i>t</i> = {record.EventTimeMyr:F3} Myr ({record.EventTimeNbody:F3} N-body time)");
        lines.Add($"Current playback: <i>t</i> = {GetDisplayTimeMyr(currentTime):F3} Myr");
        lines.Add("");
        lines.Add("<b><color=#FFF176>NBODY EVENT RECORD</color></b>");
        lines.Add($"COAL IDs: {record.CoalId1} + {record.CoalId2}");
        lines.Add($"HDF5 continuity: survivor/remnant ID {record.MergeSurvivorId}; consumed ID {record.MergeConsumedId}");
        AppendScienceSnapshots(lines, record);

        lines.Add("");
        lines.Add("<b>MASSES</b>");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> Progenitors: {FormatNumber(record.MergePreMass1, "F2")} + {FormatNumber(record.MergePreMass2, "F2")} = {FormatNumber(record.MergePreTotalMass, "F2")} {solarMass}");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> Remnant: {FormatNumber(record.MergeRemnantMass, "F2")} {solarMass}");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> Δ<i>M</i> = {FormatSignedFixed(record.MergeMassChange, "F2")} {solarMass}; Δ<i>M</i>/<i>M</i><sub>pre</sub> = {FormatSignedFixed(record.MergeMassChangeFraction, "F3")}");

        AppendEventLocation(lines, "Progenitor-system / remnant radius", record.MergeRadiusBeforePc,
            record.MergeRadiusAfterPc, record.ClusterHalfMassRadiusBeforePc, record.ClusterHalfMassRadiusAfterPc, record.MergeHalfMassRatioBefore,
            record.MergeHalfMassRatioAfter);

        lines.Add("");
        lines.Add("<b>KINEMATICS AND REFERENCES</b>");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> Progenitor CoM <i>v</i><sub>cl</sub>: {FormatNumber(record.MergePreComSpeed, "F3")} {velocityUnit}");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> Remnant <i>v</i><sub>cl</sub>: {FormatNumber(record.MergeRemnantSpeed, "F3")} {velocityUnit}");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> |Δ<b>v</b><sub>cl</sub>| between sampled snapshots: {FormatNumber(record.MergeVelocityChange, "F3")} {velocityUnit}");
        lines.Add("<color=#FFB86C>This sampled velocity change is not an inferred gravitational-wave kick.</color>");
        AppendReferenceComparison(lines, "POST remnant", record.MergeRemnantSpeed, record.MergeRemnantEscape, record.MergeRemnantSigmaRatio, record.MergeRemnantEscapeRatio);

        lines.Add("");
        lines.Add("<b>LATER BH ESCAPE LOG</b>");
        lines.Add($"<color=#E5C3FF>[LOG MATCH]</color> Remnant ID {record.MergeSurvivorId}: {FormatActualEscapeStatus(record.MergeRemnantEscapeRecorded, record.MergeRemnantFirstEscapeLagMyr, record.EscapeLogSearchEndMyr)}");
        lines.Add("");
        lines.Add("<b>DATA SOURCES</b>");
        lines.Add("<color=#B8E8FF>[NBODY LOG]</color> COAL time and the two logged IDs.");
        lines.Add("<color=#B8E8FF>[HDF5]</color> Progenitor/remnant identity, masses, positions, and velocities from complete PRE/POST snapshots.");
        lines.Add("<color=#C9FFBF>[DERIVED]</color> Mass and sampled velocity changes, event radius, and ratios.");
        lines.Add("<color=#FFD28A>[MODEL]</color> <i>v</i><sub>esc,sph</sub> is a spherical cluster-only reference.");
        lines.Add("<color=#E5C3FF>[LOG MATCH]</color> A later escape is matched by ID only and does not establish merger causation.");
        return string.Join("\n", lines);
    }

    void AppendScienceSnapshots(List<string> lines, ScienceExchangeRecord record)
    {
        lines.Add("");
        lines.Add("<b><color=#FF8CE8>PRE/POST HDF5 SNAPSHOTS</color></b>");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> PRE <i>t</i> = {FormatNumber(record.BeforeSnapshotTimeMyr, "F3")} Myr ({FormatSignedFixed(record.BeforeSnapshotOffsetMyr, "F3")} Myr from event)");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> POST <i>t</i> = {FormatNumber(record.AfterSnapshotTimeMyr, "F3")} Myr ({FormatSignedFixed(record.AfterSnapshotOffsetMyr, "F3")} Myr from event)");
    }

    void AppendEventLocation(List<string> lines, string label, double? beforeRadius, double? afterRadius,
        double? beforeHalfMass, double? afterHalfMass, double? beforeRatio, double? afterRatio)
    {
        lines.Add("");
        lines.Add("<b>EVENT LOCATION</b>");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> {label}: {FormatNumber(beforeRadius, "F3")} → {FormatNumber(afterRadius, "F3")} pc");
        if (beforeHalfMass.HasValue || afterHalfMass.HasValue)
            lines.Add($"<color=#C9FFBF>[DERIVED]</color> Cluster <i>r</i><sub>h</sub>: {FormatNumber(beforeHalfMass, "F3")} → {FormatNumber(afterHalfMass, "F3")} pc");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> <i>R</i>/<i>r</i><sub>h</sub>: {FormatNumber(beforeRatio, "F3")} → {FormatNumber(afterRatio, "F3")}");
    }

    string FormatIdList(int[] ids)
    {
        if (ids == null || ids.Length == 0) return "unavailable";
        return string.Join(", ", ids.Select(id => $"ID {id}"));
    }

    string FormatTransitionSequence(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unavailable";
        return value.Replace("|", "  |  ").Replace(">", " → ");
    }

    string BuildExchangeScienceDataText(
        float currentTime,
        Dictionary<int, BodyData> bodyById,
        ScienceExchangeRecord exchange)
    {
        List<string> lines = new List<string>();
        string viewFrame = isFollowMode
            ? (followBinaryCenterOfMass
                ? (followPostExchangeBinary ? "Follow POST-event binary CoM" : "Follow PRE-event binary CoM")
                : $"Follow BH {followedBhId}")
            : (useCenterOfMass ? "Cluster CoM" : "Raw");
        string solarMass = GetScienceSolarMassUnit();
        double? beforeOffset = exchange.BeforeSnapshotOffsetMyr;
        double? afterOffset = exchange.AfterSnapshotOffsetMyr;
        if (!beforeOffset.HasValue && exchange.BeforeSnapshotTimeMyr.HasValue)
            beforeOffset = exchange.BeforeSnapshotTimeMyr.Value - exchange.EventTimeMyr;
        if (!afterOffset.HasValue && exchange.AfterSnapshotTimeMyr.HasValue)
            afterOffset = exchange.AfterSnapshotTimeMyr.Value - exchange.EventTimeMyr;

        bool hasLogBindingEnergy = exchange.BindingEnergyLogBefore.HasValue && exchange.BindingEnergyLogAfter.HasValue;
        double? primaryBindingBefore = hasLogBindingEnergy ? exchange.BindingEnergyLogBefore : exchange.BindingEnergyBefore;
        double? primaryBindingAfter = hasLogBindingEnergy ? exchange.BindingEnergyLogAfter : exchange.BindingEnergyAfter;
        double? fractionalBindingChange = exchange.BindingEnergyFractionalChange;
        if (!fractionalBindingChange.HasValue && primaryBindingBefore.HasValue &&
            primaryBindingAfter.HasValue && primaryBindingBefore.Value > 0.0)
        {
            fractionalBindingChange =
                (primaryBindingAfter.Value - primaryBindingBefore.Value) / primaryBindingBefore.Value;
        }

        string title = currentSelectedEvent.IsGroupedStrongInteraction
            ? $"STRONG INTERACTION — {currentSelectedEvent.DiagnosticCount} EXCHANGE diagnostics; record #{exchange.ExchangeIndex}"
            : $"EXCHANGE #{exchange.ExchangeIndex}";
        lines.Add($"<b><color=#67D5FF>SCIENCE MODE — {title}</color></b>");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> Event: <i>t</i> = {exchange.EventTimeMyr:F3} Myr ({exchange.EventTimeNbody:F3} N-body time)");
        lines.Add($"Current playback: <i>t</i> = {GetDisplayTimeMyr(currentTime):F3} Myr; view: {viewFrame}");
        lines.Add("");

        lines.Add("<b><color=#FFF176>NBODY EVENT RECORD</color></b>");
        lines.Add($"Binary: ID {exchange.BeforeId1} + {exchange.BeforeId2}; incoming ID {exchange.IncomingId}");
        lines.Add($"Binary: ID {exchange.AfterId1} + {exchange.AfterId2}; outgoing ID {exchange.OutgoingId}");

        lines.Add("");
        lines.Add("<b><color=#FF8CE8>PRE/POST HDF5 SNAPSHOTS</color></b>");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> PRE <i>t</i> = {FormatNumber(exchange.BeforeSnapshotTimeMyr, "F3")} Myr ({FormatSignedFixed(beforeOffset, "F3")} Myr from event)");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> POST <i>t</i> = {FormatNumber(exchange.AfterSnapshotTimeMyr, "F3")} Myr ({FormatSignedFixed(afterOffset, "F3")} Myr from event)");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> Masses PRE: {FormatNumber(exchange.BeforeMass1, "F2")} + {FormatNumber(exchange.BeforeMass2, "F2")}; incoming {FormatNumber(exchange.IncomingMass, "F2")} {solarMass}");
        lines.Add($"<color=#B8E8FF>[HDF5]</color> Masses POST: {FormatNumber(exchange.AfterMass1, "F2")} + {FormatNumber(exchange.AfterMass2, "F2")}; outgoing {FormatNumber(exchange.OutgoingMass, "F2")} {solarMass}");

        lines.Add("");
        lines.Add("<b>EVENT LOCATION</b>");
        if (exchange.InteractionComRadiusBeforePc.HasValue || exchange.InteractionComRadiusAfterPc.HasValue)
        {
            lines.Add($"<color=#C9FFBF>[DERIVED]</color> Three-body CoM <i>R</i><sub>123</sub>: {FormatNumber(exchange.InteractionComRadiusBeforePc, "F3")} → {FormatNumber(exchange.InteractionComRadiusAfterPc, "F3")} pc");
            lines.Add($"<color=#C9FFBF>[DERIVED]</color> Cluster half-mass radius <i>r</i><sub>h</sub>: {FormatNumber(exchange.ClusterHalfMassRadiusBeforePc, "F3")} → {FormatNumber(exchange.ClusterHalfMassRadiusAfterPc, "F3")} pc");
            lines.Add($"<color=#C9FFBF>[DERIVED]</color> <i>R</i><sub>123</sub>/<i>r</i><sub>h</sub>: {FormatNumber(exchange.InteractionComHalfMassRatioBefore, "F3")} → {FormatNumber(exchange.InteractionComHalfMassRatioAfter, "F3")}");
        }
        else
        {
            lines.Add("<color=#FFB86C>Event-location columns are absent. Regenerate the CSV with the updated nbody_bh_data.py.</color>");
        }

        lines.Add("");
        lines.Add("<b>ORBIT AND BINDING ENERGY</b>");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> <i>a</i>: {FormatNumber(exchange.SemimajorAxisBeforeAu, "F3")} → {FormatNumber(exchange.SemimajorAxisAfterAu, "F3")} AU");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> <i>e</i>: {FormatNumber(exchange.EccentricityBefore, "F3")} → {FormatNumber(exchange.EccentricityAfter, "F3")}");
        lines.Add($"<color=#B8E8FF>[NBODY LOG]</color> |<i>E</i><sub>b</sub>|: {FormatScientific(primaryBindingBefore)} → {FormatScientific(primaryBindingAfter)} {solarMass} (km/s)<sup>2</sup>{(hasLogBindingEnergy ? "" : " <color=#FFB86C>(log value unavailable; derived value shown)</color>")}");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> <i>r</i><sub>peri</sub> = <i>a</i>(1−<i>e</i>): {FormatNumber(exchange.PericenterBeforeAu, "F3")} → {FormatNumber(exchange.PericenterAfterAu, "F3")} AU");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> <i>Gm</i><sub>1</sub><i>m</i><sub>2</sub>/(2<i>a</i>): {FormatScientific(exchange.BindingEnergyBefore)} → {FormatScientific(exchange.BindingEnergyAfter)} {solarMass} (km/s)<sup>2</sup>");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> Δ|<i>E</i><sub>b</sub>|/|<i>E</i><sub>b,pre</sub>| = {FormatSignedFixed(fractionalBindingChange, "F3")}");

        lines.Add("");
        lines.Add("<b>KINEMATICS</b>");
        lines.Add($"<color=#C9FFBF>[DERIVED]</color> Binary–single relative speed: {FormatNumber(exchange.BinarySingleRelativeSpeedBefore, "F3")} → {FormatNumber(exchange.BinarySingleRelativeSpeedAfter, "F3")} {velocityUnit}");

        lines.Add("");
        lines.Add("<b>REFERENCE COMPARISONS</b>");
        AppendReferenceComparison(lines, "PRE binary", exchange.BinarySpeedBefore, exchange.BinaryEscapeBefore, SafeRatio(exchange.BinarySpeedBefore, exchange.SigmaBefore), SafeRatio(exchange.BinarySpeedBefore, exchange.BinaryEscapeBefore));
        AppendReferenceComparison(lines, "PRE incoming", exchange.SingleSpeedBefore, exchange.SingleEscapeBefore, SafeRatio(exchange.SingleSpeedBefore, exchange.SigmaBefore), SafeRatio(exchange.SingleSpeedBefore, exchange.SingleEscapeBefore));
        AppendReferenceComparison(lines, "POST binary", exchange.BinarySpeedAfter, exchange.BinaryEscapeAfter, exchange.BinarySigmaRatioAfter, exchange.BinaryEscapeRatioAfter);
        AppendReferenceComparison(lines, "POST outgoing", exchange.SingleSpeedAfter, exchange.SingleEscapeAfter, exchange.SingleSigmaRatioAfter, exchange.SingleEscapeRatioAfter);

        lines.Add("");
        lines.Add("<b>LATER BH ESCAPE LOG</b>");
        lines.Add($"<color=#E5C3FF>[LOG MATCH]</color> Final binary as exact pair: {FormatActualEscapeStatus(exchange.BinaryExactEscapeRecorded, exchange.BinaryFirstEscapeLagMyr, exchange.EscapeLogSearchEndMyr)}");
        lines.Add($"<color=#E5C3FF>[LOG MATCH]</color> Outgoing ID {exchange.OutgoingId}: {FormatActualEscapeStatus(exchange.OutgoingEscapeRecorded, exchange.OutgoingFirstEscapeLagMyr, exchange.EscapeLogSearchEndMyr)}");

        lines.Add("");
        lines.Add("<b>DATA SOURCES</b>");
        lines.Add("<color=#B8E8FF>[NBODY LOG]</color> Event time, member IDs, <i>a</i>, <i>e</i>, and logged binding energy.");
        lines.Add("<color=#B8E8FF>[HDF5]</color> Masses, positions, and velocities at the nearest complete PRE/POST snapshots; sampled, not interpolated.");
        lines.Add("<color=#C9FFBF>[DERIVED]</color> Three-body CoM radius, cluster <i>r</i><sub>h</sub>, velocities, <i>r</i><sub>peri</sub>, binding energy, ratios, and changes.");
        lines.Add("<color=#FFD28A>[MODEL]</color> <i>v</i><sub>esc,sph</sub> uses a spherical cluster-only potential; it is a reference, not the NBODY escape criterion.");
        lines.Add("<color=#E5C3FF>[LOG MATCH]</color> Later BH ESCAPE records are associated by object ID; a match does not prove causation by this exchange.");
        if (!exchange.EscapeLogSearchEndMyr.HasValue)
            lines.Add("<color=#FFB86C>New Science columns are absent. Regenerate the CSV with the updated nbody_bh_data.py.</color>");

        return string.Join("\n", lines);
    }

    void AppendReferenceComparison(
        List<string> lines,
        string label,
        double? speed,
        double? escapeSpeed,
        double? sigmaRatio,
        double? escapeRatio)
    {
        lines.Add($"<b>{label}</b>");
        lines.Add($"  <color=#C9FFBF>[DERIVED]</color> <i>v</i><sub>cl</sub> = {FormatNumber(speed, "F3")} {velocityUnit} (from HDF5 phase space)");
        lines.Add($"  <color=#FFD28A>[MODEL]</color> <i>v</i><sub>esc,sph</sub> = {FormatNumber(escapeSpeed, "F3")} {velocityUnit}");
        lines.Add($"  <color=#C9FFBF>[DERIVED]</color> <i>v</i>/σ<sub>1D</sub> = {FormatNumber(sigmaRatio, "F2")}; <i>v</i>/<i>v</i><sub>esc,sph</sub> = {FormatEscapeRatio(escapeRatio)}");
    }

    void AppendCurrentExchangeKinematics(
        List<string> lines,
        Dictionary<int, BodyData> bodyById,
        ClusterScienceRecord cluster,
        ScienceExchangeRecord exchange)
    {
        int[] participantIds =
        {
            exchange.BeforeId1,
            exchange.BeforeId2,
            exchange.IncomingId,
            exchange.AfterId1,
            exchange.AfterId2,
            exchange.OutgoingId
        };

        foreach (int id in participantIds.Distinct())
        {
            if (!bodyById.TryGetValue(id, out BodyData body))
            {
                if (activeKsCompositeState != null && id == activeKsCompositeState.MissingId)
                {
                    lines.Add($"ID {id}: {activeKsCompositeState.MissingComponentMass:F2} {GetScienceSolarMassUnit()}; phase-space uses the binary CoM proxy");
                }
                else
                {
                    lines.Add($"ID {id}: not present in the current frame");
                }
                continue;
            }

            BodyData displayBody = GetKsDisplayBody(body, activeKsCompositeState);
            float speed = (displayBody.velocity - cluster.CenterVelocityKms).magnitude;
            string ratio = cluster.Sigma1dKms.HasValue && cluster.Sigma1dKms.Value > 0.0
                ? (speed / cluster.Sigma1dKms.Value).ToString("F2", CultureInfo.InvariantCulture)
                : "—";
            string qualifier = activeKsCompositeState != null &&
                (id == activeKsCompositeState.CompositeId || id == activeKsCompositeState.MissingId)
                    ? "; KS CoM proxy"
                    : "";
            lines.Add($"ID {id}: {displayBody.mass:F2} {GetScienceSolarMassUnit()}, |<i>v</i><sub>cl</sub>| = {speed:F3} {velocityUnit}, <i>v</i>/σ = {ratio}{qualifier}");
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

    double? SafeRatio(double? numerator, double? denominator)
    {
        if (!numerator.HasValue || !denominator.HasValue || denominator.Value <= 0.0) return null;
        return numerator.Value / denominator.Value;
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

    string FormatSignedFixed(double? value, string format)
    {
        if (!value.HasValue) return "—";
        string magnitude = System.Math.Abs(value.Value).ToString(format, CultureInfo.InvariantCulture);
        if (value.Value > 0.0) return "+" + magnitude;
        if (value.Value < 0.0) return "−" + magnitude;
        return magnitude;
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

    string FormatActualEscapeStatus(bool recorded, double? lagMyr, double? searchEndMyr)
    {
        if (recorded)
        {
            string lag = lagMyr.HasValue ? $"; lag {lagMyr.Value:F3} Myr" : "";
            return $"<color=#7CFF9B>recorded{lag}</color>";
        }
        if (searchEndMyr.HasValue)
            return $"not recorded in available NBODY output through {searchEndMyr.Value:F3} Myr";
        return "not available in this Science CSV";
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

        lines.Add($"<i>M</i><sub>tot</sub> = {totalMass:F2} {GetScienceSolarMassUnit()}, <i>q</i> = {massRatio:F3}");
        lines.Add($"<i>r</i> = {separation:F3} {distanceUnit}");
        lines.Add($"<i>v</i><sub>rel</sub> = {relativeSpeed:F3} {velocityUnit}");
        lines.Add($"<i>v</i><sub>r</sub> = {radialVelocity:+0.000;-0.000;0.000} {velocityUnit} ({radialState})");
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

        bool useAfterPair = followPostExchangeBinary
            && currentSelectedEvent.type == "EXCHANGE"
            && currentSelectedEvent.ids.Length >= 4;
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
            id = followPostExchangeBinary ? FollowPostBinarySentinel : FollowPreBinarySentinel,
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
        int idxTemporaryIds = headers.IndexOf("temporary_ids");

        if (idxType == -1 || idxTime == -1) return;

        CultureInfo ci = CultureInfo.InvariantCulture;
        HashSet<string> seenEventKeys = new HashSet<string>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] vals = lines[i].Split(',');
            if (vals.Length <= idxType || vals.Length <= idxTime) continue;
            
            string type = vals[idxType].Trim();
            if (type != "MERGE" && type != "EXCHANGE" && type != "TRANSIENT EXCHANGE") continue;

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
            List<int> temporaryIds = new List<int>();
            if (idxTemporaryIds != -1 && idxTemporaryIds < vals.Length)
            {
                foreach (string token in vals[idxTemporaryIds].Trim().Split(new[] { ';', '|', ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    if (float.TryParse(token, NumberStyles.Float, ci, out float temporaryId))
                    {
                        temporaryIds.Add((int)temporaryId);
                    }
                }
            }
            evt.temporaryIds = temporaryIds.Distinct().ToArray();
            evt.configurations.Add(evt.ids.ToArray());
            
            if (evt.ids.Length == 0) continue;

            string eventKey = evt.type + "|" + evt.time.ToString("R", ci) + "|" + string.Join("+", evt.ids) + "|" + string.Join("+", evt.temporaryIds);
            if (!seenEventKeys.Add(eventKey)) continue;

            loadedEvents.Add(evt);
        }

        if (groupSimultaneousExchangeDiagnostics)
        {
            GroupSimultaneousExchangeDiagnostics();
        }
    }

    void GroupSimultaneousExchangeDiagnostics()
    {
        List<InteractionEvent> grouped = new List<InteractionEvent>();
        foreach (InteractionEvent evt in loadedEvents.OrderBy(item => item.time))
        {
            if (evt.type != "EXCHANGE")
            {
                grouped.Add(evt);
                continue;
            }

            InteractionEvent target = grouped.LastOrDefault(item =>
                item.type == "EXCHANGE" &&
                Mathf.Abs(item.time - evt.time) <= exchangeGroupingTimeTolerance &&
                item.AllIds.Intersect(evt.AllIds).Any());

            if (target == null)
            {
                grouped.Add(evt);
                continue;
            }

            foreach (int[] configuration in evt.configurations)
            {
                string key = string.Join("+", configuration);
                if (!target.configurations.Any(existing => string.Join("+", existing) == key))
                {
                    target.configurations.Add(configuration.ToArray());
                }
            }
        }
        loadedEvents = grouped.OrderBy(item => item.time).ToList();
    }

    float FindEventReferenceMass(InteractionEvent evt, int id)
    {
        if (TryRecoverExchange(evt, out RecoveredExchange recovered))
        {
            RecoveredExchangeFrame[] frames = { recovered.Before, recovered.After };
            foreach (RecoveredExchangeFrame frame in frames
                .Where(item => item != null)
                .OrderBy(item => Mathf.Abs(item.Time - evt.time)))
            {
                if (frame.Bodies.TryGetValue(id, out BodyData recoveredBody) && recoveredBody.mass > 0f)
                {
                    return recoveredBody.mass;
                }
            }
        }

        float bestMass = -1f;
        float closestDifference = float.MaxValue;
        foreach (float time in sortedTimeKeys)
        {
            BodyData body = timeDataMap[time]
                .Where(item => item.id == id && IsUsableBody(item))
                .OrderByDescending(item => item.mass)
                .FirstOrDefault();
            if (body == null) continue;
            float difference = Mathf.Abs(time - evt.time);
            if (difference < closestDifference)
            {
                closestDifference = difference;
                bestMass = body.mass;
            }
        }
        return bestMass;
    }

    bool TryRecoverExchange(InteractionEvent evt, out RecoveredExchange recovered)
    {
        recovered = null;
        if (evt == null || evt.type != "EXCHANGE") return false;
        if (recoveredExchangeCacheEvent == evt && recoveredExchangeCacheAttempted)
        {
            recovered = recoveredExchangeCache;
            return recovered != null;
        }

        IEnumerable<int[]> configurations = evt.configurations != null && evt.configurations.Count > 0
            ? evt.configurations
            : new[] { evt.ids };
        int bestCompleteness = -1;
        float bestLag = float.MaxValue;

        foreach (int[] configuration in configurations)
        {
            if (configuration == null || configuration.Length < 4) continue;
            int[] ids = configuration.Distinct().ToArray();
            TryFindCompleteExchangeFrame(ids, evt.time, true, out RecoveredExchangeFrame before);
            TryFindCompleteExchangeFrame(ids, evt.time, false, out RecoveredExchangeFrame after);
            int completeness = (before != null ? 1 : 0) + (after != null ? 1 : 0);
            float lag = (before != null ? Mathf.Abs(before.Time - evt.time) : scienceCompleteSnapshotSearchWindow)
                + (after != null ? Mathf.Abs(after.Time - evt.time) : scienceCompleteSnapshotSearchWindow);
            if (completeness > bestCompleteness || (completeness == bestCompleteness && lag < bestLag))
            {
                bestCompleteness = completeness;
                bestLag = lag;
                recovered = new RecoveredExchange
                {
                    Configuration = configuration.ToArray(),
                    Before = before,
                    After = after
                };
            }
        }
        if (bestCompleteness <= 0) recovered = null;
        recoveredExchangeCacheEvent = evt;
        recoveredExchangeCache = recovered;
        recoveredExchangeCacheAttempted = true;
        return recovered != null;
    }

    bool TryFindCompleteExchangeFrame(
        int[] ids,
        float eventTime,
        bool before,
        out RecoveredExchangeFrame recoveredFrame)
    {
        recoveredFrame = null;
        IEnumerable<float> candidates = before
            ? sortedTimeKeys.Where(time => time < eventTime).OrderByDescending(time => time)
            : sortedTimeKeys.Where(time => time > eventTime).OrderBy(time => time);

        foreach (float time in candidates)
        {
            if (scienceCompleteSnapshotSearchWindow > 0f &&
                Mathf.Abs(time - eventTime) > scienceCompleteSnapshotSearchWindow) break;

            Dictionary<int, BodyData> bodies = new Dictionary<int, BodyData>();
            bool complete = true;
            foreach (int id in ids)
            {
                BodyData body = timeDataMap[time]
                    .Where(item => item.id == id && IsUsableBody(item))
                    .OrderByDescending(item => item.mass)
                    .FirstOrDefault();
                if (body == null)
                {
                    complete = false;
                    break;
                }
                bodies[id] = body;
            }
            if (!complete) continue;

            recoveredFrame = new RecoveredExchangeFrame
            {
                Time = time,
                Bodies = bodies
            };
            return true;
        }
        return false;
    }

    bool IsUsableBody(BodyData body)
    {
        return body != null &&
            body.mass > 0.000001f &&
            IsFinite(body.mass) &&
            IsFinite(body.position.x) && IsFinite(body.position.y) && IsFinite(body.position.z) &&
            IsFinite(body.velocity.x) && IsFinite(body.velocity.y) && IsFinite(body.velocity.z);
    }

    bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    void SetupUI()
    {
        ResolveScienceUIReferences();
        ApplySciencePanelLayout();

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
            if (eventDropdown.captionText != null) eventDropdown.captionText.supportRichText = true;
            if (eventDropdown.itemText != null) eventDropdown.itemText.supportRichText = true;
            eventDropdown.ClearOptions();
            List<string> options = new List<string> { "Select Event..." };
            
            List<InteractionEvent> validDropdownEvents = new List<InteractionEvent>();

            foreach (var evt in loadedEvents)
            {
                List<string> idMassStrings = new List<string>();
                bool hasInvalidId = false;

                IEnumerable<int> idsForLabel = evt.IsTransientExchange
                    ? evt.ids.Take(2).Concat(evt.temporaryIds ?? new int[0]).Distinct()
                    : (evt.IsGroupedStrongInteraction ? evt.AllIds : evt.ids);
                foreach (int id in idsForLabel)
                {
                    float bestMass = FindEventReferenceMass(evt, id);

                    if (bestMass > 0f)
                    {
                        idMassStrings.Add($"{id}({bestMass:F1} {GetPlainSolarMassUnit()})"); 
                    }
                    else if (evt.IsTransientExchange && evt.IsTemporaryParticipant(id))
                    {
                        idMassStrings.Add($"{id}(not in snapshot)");
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
                if (evt.IsTransientExchange && idMassStrings.Count >= 2)
                {
                    string interloperText = idMassStrings.Count > 2
                        ? "; interloper: " + string.Join(", ", idMassStrings.Skip(2))
                        : "; interloper unavailable";
                    relationStr = $"{idMassStrings[0]} & {idMassStrings[1]} → {idMassStrings[0]} & {idMassStrings[1]}; no net member change{interloperText}";
                }
                else if (evt.IsGroupedStrongInteraction)
                {
                    relationStr = $"{evt.DiagnosticCount} diagnostic configurations; " + string.Join(", ", idMassStrings);
                }
                else if (idMassStrings.Count >= 4)
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
                string eventName = evt.IsGroupedStrongInteraction ? "STRONG INTERACTION" : evt.type;
                string label = $"{eventName} @ {eventTimeMyr:F0} Myr\n{relationStr}";
                Color dropdownColor = evt.IsTransientExchange
                    ? transientExchangeColor
                    : (evt.type == "MERGE" ? postMergeColor : binaryColor);
                label = $"<color=#{ColorUtility.ToHtmlStringRGB(dropdownColor)}>{label}</color>";
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
                ? (followBinaryCenterOfMass
                    ? (followPostExchangeBinary ? "Pos: Follow POST binary CoM" : "Pos: Follow PRE binary CoM")
                    : $"Pos: Follow BH {followedBhId}")
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
            ? (followBinaryCenterOfMass
                ? (followPostExchangeBinary ? "Exit POST-binary Follow" : "Exit PRE-binary Follow")
                : "Exit Follow")
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
        bool selectedBinary = selectedId == FollowPreBinarySentinel
            || selectedId == FollowPostBinarySentinel;
        followPostExchangeBinary = selectedId == FollowPostBinarySentinel;
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
        followPostExchangeBinary = false;
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
            candidates = currentSelectedEvent.AllIds
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
            followDropdownIds.Add(FollowPreBinarySentinel);
            options.Add(currentSelectedEvent.type == "EXCHANGE"
                ? "PRE-event binary CoM"
                : (currentSelectedEvent.IsTransientExchange ? "Stable binary CoM" : "Binary CoM"));

            if (currentSelectedEvent.type == "EXCHANGE" && currentSelectedEvent.ids.Length >= 4)
            {
                followDropdownIds.Add(FollowPostBinarySentinel);
                options.Add("POST-event binary CoM");
            }
        }
        followDropdownIds.AddRange(candidates.Select(body => body.id));
        options.AddRange(candidates.Select(body => $"BH {body.id} ({body.mass:F1} {GetPlainSolarMassUnit()})"));

        followBhDropdown.ClearOptions();
        followBhDropdown.AddOptions(options);

        int selectedIndex = 0;
        if (isFollowMode)
        {
            int selectedId = followBinaryCenterOfMass
                ? (followPostExchangeBinary ? FollowPostBinarySentinel : FollowPreBinarySentinel)
                : followedBhId;
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
            .Where(body => ArrayUtility.IndexOf(currentSelectedEvent.AllIds, body.id) >= 0)
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
        HashSet<int> eventIds = isEventMode ? new HashSet<int>(currentSelectedEvent.AllIds) : null;
        Dictionary<int, BodyData> currentBodiesById = currentFrameData
            .GroupBy(body => body.id)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(body => body.mass).First());
        TryDetectKsComposite(currentTime, currentBodiesById, out activeKsCompositeState);
        currentFrameData = AddMissingKsProxy(currentFrameData, activeKsCompositeState);
        
        foreach (var data in currentFrameData)
        {
            currentIds.Add(data.id);
            BodyData displayData = GetKsDisplayBody(data, activeKsCompositeState);

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

                    if (currentSelectedEvent.IsTransientExchange)
                    {
                        targetColor = currentSelectedEvent.IsTemporaryParticipant(data.id)
                            ? interloperColor
                            : transientExchangeColor;
                    }
                    else if (isPostMerge) 
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
                        float speed = GetDisplayedVelocity(displayData, currentTime).magnitude;
                        string proxy = activeKsCompositeState != null &&
                            (data.id == activeKsCompositeState.CompositeId || data.id == activeKsCompositeState.MissingId)
                                ? "\nKS CoM proxy"
                                : "";
                        string transientRole = currentSelectedEvent.IsTransientExchange && currentSelectedEvent.IsTemporaryParticipant(data.id)
                            ? "\nTRANSIENT INTERLOPER"
                            : "";
                        tm.text = $"ID {data.id}{transientRole}\n{displayData.mass:F1} {GetPlainSolarMassUnit()}\n|v| = {speed:F2} {velocityUnit}{proxy}";
                    }
                    else
                    {
                        tm.text = $"{displayData.mass:F1} {GetPlainSolarMassUnit()}";
                    }

                    tm.characterSize = labelCharacterSize;
                    
                    Color c = Color.white;
                    bool isPostMerge = (currentSelectedEvent.type == "MERGE" && currentTime >= currentSelectedEvent.time);
                    if (currentSelectedEvent.IsTransientExchange)
                    {
                        c = currentSelectedEvent.IsTemporaryParticipant(data.id)
                            ? interloperColor
                            : transientExchangeColor;
                    }
                    else if (isPostMerge) c = postMergeColor;
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
