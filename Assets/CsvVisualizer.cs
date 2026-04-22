using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using UnityEngine.EventSystems;

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

    [Header("Colors")]
    public Color normalColor = Color.black;
    public Color binaryColor = Color.magenta;
    public Color interloperColor = Color.red;
    public Color postMergeColor = Color.cyan;
    public Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.05f);

    [Header("Label Settings")]
    public float labelFontSize = 100f;
    public float labelCharacterSize = 0.02f;
    public float labelHeightOffset = 1.0f;
    public float labelSpreadX = 1.5f;
    public float labelSpreadY = 0.5f;

    [Header("HUD Settings")]
    public GameObject mainCanvas;
    public float hudFontSize = 150f;
    public float hudCharacterSize = 0.03f;
    public Vector3 hudPositionOffset = Vector3.zero;

    [Header("UI Linking")]
    public Slider timeSlider;
    public Slider speedSlider;
    public Slider sizeSlider;
    public Text timeLabel;
    public Button playButton;
    public Dropdown eventDropdown;
    public Button togglePosModeButton;

    private Dictionary<float, List<BodyData>> timeDataMap = new Dictionary<float, List<BodyData>>();
    private List<float> sortedTimeKeys = new List<float>();
    private Dictionary<float, Vector3> centerOfMassMap = new Dictionary<float, Vector3>();
    private Dictionary<int, GameObject> activeObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> activeLabels = new Dictionary<int, GameObject>();
    private List<InteractionEvent> loadedEvents = new List<InteractionEvent>();
    private InteractionEvent currentSelectedEvent = null;
    
    private bool isPlaying = false;
    private bool useCenterOfMass = true;
    private float timer = 0f;
    private Camera mainCamera;

    private GameObject eventHudObj;
    private TextMesh eventHudText;
    private int currentEventIndex = -1;
    private bool hasError = false;
    
    [Range(0.1f, 30f)]
    public float stepsPerSecond = 5.0f; 

    IEnumerator Start()
    {
        mainCamera = Camera.main;
        SetupHUD();

        string historyPath = Path.Combine(Application.persistentDataPath, historyFile);
        string eventsPath = Path.Combine(Application.persistentDataPath, eventsFile);

        if (!File.Exists(historyPath))
        {
            ShowError($"Data missing!\nCannot find:\n{historyFile}");
            yield break;
        }

        string[] historyLines = null;
        yield return StartCoroutine(LoadFileSafe(historyPath, (lines) => { historyLines = lines; }));
        
        if (historyLines == null || historyLines.Length == 0)
        {
            if (!hasError) ShowError("History file is empty or could not be read.");
            yield break;
        }

        LoadHistoryData(historyLines);

        if (File.Exists(eventsPath))
        {
            string[] eventLines = null;
            yield return StartCoroutine(LoadFileSafe(eventsPath, (lines) => { eventLines = lines; }));
            if (eventLines != null && eventLines.Length > 0)
            {
                LoadEventData(eventLines);
            }
        }
        
        SetupUI();

        if (timeLabel != null)
        {
            timeLabel.text = $"Loaded: {sortedTimeKeys.Count} frames";
        }

        UpdateVisuals(0);
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

        Vector3 currentCoM = useCenterOfMass && centerOfMassMap.ContainsKey(currentTime) ? centerOfMassMap[currentTime] : Vector3.zero;
        Vector3 nextCoM = useCenterOfMass && centerOfMassMap.ContainsKey(nextTime) ? centerOfMassMap[nextTime] : Vector3.zero;

        foreach (var cData in currentData)
        {
            if (activeObjects.TryGetValue(cData.id, out GameObject obj))
            {
                if (!obj.activeSelf) continue;

                Vector3 startPos = cData.position - currentCoM;
                float startScale = cData.mass * scaleMultiplier;
                Vector3 startVel = cData.velocity;

                Vector3 endPos = startPos;
                float endScale = startScale;
                Vector3 endVel = startVel;

                if (nextDataDict.TryGetValue(cData.id, out BodyData nData))
                {
                    endPos = nData.position - nextCoM;
                    endScale = nData.mass * scaleMultiplier;
                    endVel = nData.velocity;
                }

                obj.transform.position = Vector3.Lerp(startPos, endPos, fraction);
                obj.transform.localScale = Vector3.Lerp(Vector3.one * startScale, Vector3.one * endScale, fraction);

                LineRenderer lr = obj.GetComponent<LineRenderer>();
                if (lr != null && lr.enabled && lr.positionCount == 2)
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

                            float currentSpreadX = labelSpreadX * sizeRatio;
                            float currentSpreadY = labelSpreadY * sizeRatio;
                            float currentHeightOffset = labelHeightOffset * sizeRatio;

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
                            if (tm != null)
                            {
                                tm.characterSize = labelCharacterSize * sizeRatio;
                            }

                            LineRenderer calloutLr = labelObj.GetComponent<LineRenderer>();
                            if (calloutLr != null)
                            {
                                calloutLr.startWidth = 0.02f * vectorScale * sizeRatio;
                                calloutLr.endWidth = 0.02f * vectorScale * sizeRatio;
                                calloutLr.SetPosition(0, basePos);
                                calloutLr.SetPosition(1, targetLabelPos);
                            }
                        }
                    }
                }
            }
        }

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
            if(float.TryParse(values[1], NumberStyles.Float, ci, out float tempId)) body.id = (int)tempId;
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
            float totalMass = 0f;
            foreach (var body in kvp.Value)
            {
                com += body.position * body.mass;
                totalMass += body.mass;
            }
            if (totalMass > 0f) com /= totalMass;
            centerOfMassMap[kvp.Key] = com;
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
                    if (!string.IsNullOrEmpty(s) && float.TryParse(s, NumberStyles.Float, ci, out float fid)) ids.Add((int)fid);
                }
            }
            evt.ids = ids.ToArray();
            loadedEvents.Add(evt);
        }
    }

    void SetupUI()
    {
        if (timeSlider != null && sortedTimeKeys.Count > 0)
        {
            timeSlider.minValue = 0;
            timeSlider.maxValue = sortedTimeKeys.Count - 1;
            timeSlider.wholeNumbers = true;
            timeSlider.onValueChanged.AddListener(OnSliderChanged);
        }
        if (speedSlider != null)
        {
            speedSlider.minValue = 0.1f;
            speedSlider.maxValue = 30.0f;
            speedSlider.wholeNumbers = false;
            
            float midSpeed = (speedSlider.minValue + speedSlider.maxValue) / 2f;
            speedSlider.value = midSpeed;
            stepsPerSecond = midSpeed;
            
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }
        if (sizeSlider != null)
        {
            sizeSlider.minValue = sizeSliderMin;
            sizeSlider.maxValue = sizeSliderMax;
            sizeSlider.wholeNumbers = false;
            
            float midSize = (sizeSlider.minValue + sizeSlider.maxValue) / 2f;
            sizeSlider.value = midSize;
            scaleMultiplier = midSize;
            
            sizeSlider.onValueChanged.AddListener(OnSizeSliderChanged);
        }
        if (playButton != null) { playButton.onClick.AddListener(TogglePlay); UpdateButtonText(); }
        if (eventDropdown != null)
        {
            eventDropdown.ClearOptions();
            List<string> options = new List<string> { "Select Event..." };
            foreach (var evt in loadedEvents)
            {
                string label = $"{evt.type} @ {evt.time:F0}: ({string.Join(",", evt.ids)})";
                options.Add(label);
            }
            eventDropdown.AddOptions(options);
            eventDropdown.onValueChanged.AddListener(OnEventSelected);
        }
        if (togglePosModeButton != null)
        {
            togglePosModeButton.onClick.AddListener(TogglePositionMode);
            UpdateToggleButtonText();
        }
    }

    public void TogglePositionMode()
    {
        useCenterOfMass = !useCenterOfMass;
        UpdateToggleButtonText();
    }

    void UpdateToggleButtonText()
    {
        if (togglePosModeButton != null)
        {
            Text btnText = togglePosModeButton.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = useCenterOfMass ? "Pos: CoM" : "Pos: Raw";
        }
    }

    public void TogglePlay() { isPlaying = !isPlaying; UpdateButtonText(); }
    void UpdateButtonText()
    {
        if (playButton != null)
        {
            Text btnText = playButton.GetComponentInChildren<Text>();
            if (btnText != null) btnText.text = isPlaying ? "Pause" : "Play";
        }
    }
    public void OnSliderChanged(float value) { UpdateVisuals((int)value); }
    public void OnSpeedSliderChanged(float value) { stepsPerSecond = value; }
    public void OnSizeSliderChanged(float value) { scaleMultiplier = value; if (timeSlider != null) UpdateVisuals((int)timeSlider.value); }
    public void OnEventSelected(int index)
    {
        if (index == 0) 
        { 
            currentSelectedEvent = null; 
            currentEventIndex = -1;
            UpdateVisuals((int)timeSlider.value); 
        }
        else
        {
            currentSelectedEvent = loadedEvents[index - 1];
            float closestTime = -1f;
            float minDiff = float.MaxValue;
            int targetIndex = 0;
            for(int i=0; i<sortedTimeKeys.Count; i++)
            {
                float diff = Mathf.Abs(sortedTimeKeys[i] - currentSelectedEvent.time);
                if(diff < minDiff) { minDiff = diff; closestTime = sortedTimeKeys[i]; targetIndex = i; }
            }
            
            currentEventIndex = targetIndex;
            
            int dynamicPreRollSteps = Mathf.RoundToInt(preRollSeconds * stepsPerSecond);
            int startIndex = Mathf.Max(0, targetIndex - dynamicPreRollSteps);
            
            if (timeSlider != null) timeSlider.value = startIndex;
            UpdateButtonText();
        }
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
            xrOriginTransform.position = Vector3.zero;
        }
    }

    void UpdateVisuals(int timeIndex)
    {
        if (sortedTimeKeys.Count == 0 || timeIndex >= sortedTimeKeys.Count) return;
        float currentTime = sortedTimeKeys[timeIndex];
        List<BodyData> currentFrameData = timeDataMap[currentTime];
        if (timeLabel != null && !hasError) timeLabel.text = $"Time: {currentTime:F2} Myr";
        
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
                calloutLr.startWidth = 0.02f * vectorScale; 
                calloutLr.endWidth = 0.02f * vectorScale;
                calloutLr.material = new Material(Shader.Find("Sprites/Default"));
                calloutLr.positionCount = 2;
                
                labelObj.SetActive(false);
                activeLabels.Add(data.id, labelObj);
            }
            
            GameObject obj = activeObjects[data.id];
            
            Renderer rend = obj.GetComponent<Renderer>();
            LineRenderer lr = obj.GetComponent<LineRenderer>();

            if (isEventMode)
            {
                if (eventIds.Contains(data.id))
                {
                    bool isPostMerge = (currentSelectedEvent.type == "MERGE" && currentTime >= currentSelectedEvent.time);
                    if (isPostMerge) { rend.material.color = postMergeColor; lr.startColor = postMergeColor; lr.endColor = postMergeColor; }
                    else {
                        if (ArrayUtility.IndexOf(currentSelectedEvent.ids, data.id) < 2) rend.material.color = binaryColor;
                        else rend.material.color = interloperColor;
                        lr.startColor = Color.cyan; lr.endColor = Color.blue;
                    }
                    lr.enabled = true;
                    lr.positionCount = 2;
                }
                else { 
                    rend.material.color = dimColor; 
                    lr.enabled = false; 
                    lr.positionCount = 0; 
                }
            }
            else { 
                rend.material.color = normalColor; 
                lr.enabled = false; 
                lr.positionCount = 0; 
            }

            if (activeLabels.TryGetValue(data.id, out GameObject labelObject))
            {
                if (isEventMode && eventIds.Contains(data.id))
                {
                    TextMesh tm = labelObject.GetComponent<TextMesh>();
                    LineRenderer calloutLr = labelObject.GetComponent<LineRenderer>();
                    
                    tm.text = $"ID:{data.id}\nM:{data.mass:F1}";

                    float midSize = (sizeSliderMin + sizeSliderMax) / 2f;
                    float sizeRatio = scaleMultiplier / midSize;
                    tm.characterSize = labelCharacterSize * sizeRatio;
                    
                    Color c = Color.white;
                    bool isPostMerge = (currentSelectedEvent.type == "MERGE" && currentTime >= currentSelectedEvent.time);
                    if (isPostMerge) c = postMergeColor;
                    else
                    {
                        if (ArrayUtility.IndexOf(currentSelectedEvent.ids, data.id) < 2) c = binaryColor;
                        else c = interloperColor;
                    }
                    
                    tm.color = c;
                    if (calloutLr != null)
                    {
                        calloutLr.startWidth = 0.02f * vectorScale * sizeRatio;
                        calloutLr.endWidth = 0.02f * vectorScale * sizeRatio;
                        calloutLr.startColor = c;
                        calloutLr.endColor = c;
                    }
                    
                    labelObject.SetActive(true);
                }
                else
                {
                    labelObject.SetActive(false);
                }
            }
        }
        foreach (var kvp in activeObjects) { kvp.Value.SetActive(currentIds.Contains(kvp.Key)); }
        foreach (var kvp in activeLabels) { kvp.Value.SetActive(currentIds.Contains(kvp.Key) && isEventMode && eventIds != null && eventIds.Contains(kvp.Key)); }
    }
    public static class ArrayUtility
    {
        public static int IndexOf(int[] array, int value) { for(int i=0; i<array.Length; i++) if(array[i] == value) return i; return -1; }
    }
}