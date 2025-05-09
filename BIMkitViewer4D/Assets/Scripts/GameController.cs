using DbmsApi;
using DbmsApi.API;
using GenerativeDesignAPI;
using GenerativeDesignPackage;
using MathPackage;
using ModelCheckAPI;
using ModelCheckPackage;
using RuleAPI;
using RuleAPI.Methods;
using RuleAPI.Models;
using RuleGeneratorPackage;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VoxelPackage;
using static UnityEngine.UI.Dropdown;
using Component = DbmsApi.API.Component;
using Debug = UnityEngine.Debug;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

public class GameController : MonoBehaviour
{
    #region UI Fields

    public Camera MainCamera;
    public GameObject LoadingCanvas;

    public GameObject ModelViewCanvas;
    public Text ObjectDataText;
    public Slider DateSlider;
    public Text DateText;

    public GameObject ModelSelectCanvas;
    public GameObject ModelLocalListViewContent;
    public GameObject ModelScheduleListViewContent;

    public Button StandardButtonPrefab;
    public GameObject ModelObjectPrefab;
    public GameObject ModelComponentPrefab;

    #endregion

    #region Game Fields

    private List<Button> modelButtons = new List<Button>();
    string modelFile = null;
    private List<Button> scheduleButtons = new List<Button>();
    Dictionary<string, Tuple<Button, bool>> scheduleFiles = new Dictionary<string, Tuple<Button, bool>>();

    private Model CurrentModel;
    public GameObject CurrentModelGameObj;
    private GameObject ViewingGameObject;

    private float modelGap = 50.0f;
    private List<Dictionary<string, ModelObjectScript>> ModelObjects = new List<Dictionary<string, ModelObjectScript>>();
    private List<Dictionary<DateTime, List<ModelObjectScript>>> ModelSchedules = new List<Dictionary<DateTime, List<ModelObjectScript>>>();
    private List<Camera> cameras = new List<Camera>();

    public float cameraRotateSpeed = 1000.0f;
    public float cameraMoveSpeed = 100.0f;
    public float cameraScrollSensitivity = 10.0f;
    public float timeSpeed = 1.0f;

    #endregion

    #region Materials

    public Material HighlightMatRed;
    public Material HighlightMatYellow;
    public Material HighlightMatGreen;
    public Material DefaultMat;
    public Material InvisibleMat;

    private Dictionary<string, Material> MaterialDict = new Dictionary<string, Material>();

    #endregion

    // Start is called before the first frame update
    void Start()
    {
        ResetCanvas();
        this.ModelSelectCanvas.SetActive(true);

        GetLocalModels();
        GetLocalSchedules();

        if (scheduleButtons.Count == 0)
        {
            string[] fileNames = Directory.GetFiles(Application.dataPath + "/..//Models", "*.bpm");
            CurrentModel = DBMSReadWrite.JSONReadFromFile<Model>(fileNames[0]);

            List<string> csvOut = new List<string>();
            DateTime currentTime = DateTime.Now;
            foreach (ModelObject mo in CurrentModel.ModelObjects.OrderBy(mo => mo.Location.z))
            {
                csvOut.Add(currentTime.ToString() + "," + mo.Id);
                currentTime += TimeSpan.FromDays(5);
            }

            File.WriteAllLines(Application.dataPath + "/..//Schedules/TEST1.csv", csvOut);

            csvOut = new List<string>();
            currentTime = DateTime.Now;
            foreach (ModelObject mo in CurrentModel.ModelObjects.OrderByDescending(mo => mo.Location.z))
            {
                csvOut.Add(currentTime.ToString() + "," + mo.Id);
                currentTime += TimeSpan.FromDays(5);
            }

            File.WriteAllLines(Application.dataPath + "/..//Schedules/TEST2.csv", csvOut);

            GetLocalSchedules();
        }

        DateSlider.value = DateSlider.minValue;
    }

    // Update is called once per frame
    void Update()
    {
        MoveCameras();
        if (this.ModelViewCanvas.activeInHierarchy)
        {
            ViewingMode();
        }

        if (DateSlider.value < DateSlider.maxValue)
        {
            DateSlider.value = Mathf.Min(DateSlider.maxValue, DateSlider.value + (new TimeSpan(7, 0, 0, 0)).Ticks * timeSpeed * Time.deltaTime);
        }
    }

    #region Camera Controls

    private void MoveCameras()
    {
        foreach (Camera c in cameras)
        {
            if (Input.GetMouseButton(1))
            {
                c.transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * cameraRotateSpeed * Time.deltaTime, Input.GetAxis("Mouse X") * cameraRotateSpeed * Time.deltaTime, 0));
                float X = c.transform.rotation.eulerAngles.x;
                float Y = c.transform.rotation.eulerAngles.y;
                c.transform.rotation = Quaternion.Euler(X, Y, 0);
            }

            if (Input.GetMouseButton(2))
            {
                var newPosition = new Vector3();
                newPosition.x = Input.GetAxis("Mouse X") * cameraMoveSpeed * Time.deltaTime;
                newPosition.y = Input.GetAxis("Mouse Y") * cameraMoveSpeed * Time.deltaTime;
                c.transform.Translate(-newPosition);
            }

            c.transform.position += c.transform.forward * Input.GetAxis("Mouse ScrollWheel") * cameraScrollSensitivity;
        }
    }

    private void SetupNewCamera(Dictionary<string, ModelObjectScript> newModelObjects, float count, float total)
    {
        Camera newCamera = Instantiate(MainCamera);
        newCamera.enabled = true;
        newCamera.rect = new Rect(count / total, 0.0f, 1.0f / total * 0.99f, 1.0f);

        JumpCameraToObject(newCamera, newModelObjects, count);
        cameras.Add(newCamera);
    }

    public void JumpCameraToObject(Camera camera, Dictionary<string, ModelObjectScript> mos, float count)
    {
        List<Vector3D> vList = mos.SelectMany(m => m.Value.ModelObject.Components.SelectMany(c => c.Vertices.Select(v => Vector3D.Add(v, m.Value.ModelObject.Location)))).ToList();
        Utils.GetXYZDimentions(vList, out Vector3D mid, out Vector3D dims);

        Vector3 center = VectorConvert(mid);
        center = center + new Vector3(0.0f, 0.0f, count * modelGap);
        Vector3 diment = VectorConvert(dims);

        camera.orthographic = false;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 10000.0f;
        //MainCamera.transform.position = new Vector3(center.x, center.y + 2.0f * diment.y, center.z);
        camera.transform.position = new Vector3(center.x + Mathf.Max(diment.x, diment.z) / 2.0f, center.y, center.z);
        camera.transform.LookAt(center, Vector3.up);
    }

    #endregion

    #region Model Select Mode

    private void GetLocalModels()
    {
        // Get all file names
        string modelsPath = Application.dataPath + "/..//Models";
        if (!Directory.Exists(modelsPath))
        {
            Directory.CreateDirectory(modelsPath);
        }
        //Debug.Log(Application.dataPath);
        string[] fileNames = Directory.GetFiles(modelsPath, "*.bpm");

        RemoveAllChidren(this.ModelLocalListViewContent);
        foreach (string modelPath in fileNames)
        {
            Button newButton = GameObject.Instantiate(this.StandardButtonPrefab, this.ModelLocalListViewContent.transform);
            newButton.GetComponentInChildren<Text>().text = Path.GetFileNameWithoutExtension(modelPath);
            UnityAction action = new UnityAction(() =>
            {
                modelFile = modelPath;
                foreach (Button mb in modelButtons)
                {
                    mb.image.color = new Color(255, 255, 255);
                }
                newButton.image.color = new Color(0, 250, 0);
            });
            newButton.onClick.AddListener(action);
            modelButtons.Add(newButton);
        }
    }

    private void LoadLocalModel(string modelPath, int count)
    {
        CurrentModel = DBMSReadWrite.JSONReadFromFile<Model>(modelPath);
        CurrentModel.Id = null; // Make sure the local models dont conflict with the existing (incase they happen to have the same ID)

        RemoveAllChidren(CurrentModelGameObj);

        for (int counter = 0; counter < count; counter++)
        {
            GameObject newGO = new GameObject(counter.ToString());
            Dictionary<string, ModelObjectScript> newModelObjects = new Dictionary<string, ModelObjectScript>();
            foreach (ModelObject obj in CurrentModel.ModelObjects)
            {
                if (obj.GetType() == typeof(ModelCatalogObject))
                {
                    string catId = (obj as ModelCatalogObject).CatalogId;
                    Debug.LogWarning("Missing Item: " + catId);
                }

                ModelObjectScript script = CreateModelObject(obj, newGO);
                newModelObjects.Add(obj.Id, script);
            }

            newGO.transform.parent = CurrentModelGameObj.transform;
            newGO.transform.position = new Vector3(0, 0, counter * modelGap);
            ModelObjects.Add(newModelObjects);
            SetupNewCamera(newModelObjects, counter, count);
        }

        ResetCanvas();
        ModelViewCanvas.SetActive(true);
    }

    private void GetLocalSchedules()
    {
        // Get all file names
        string schedulesPath = Application.dataPath + "/..//Schedules";
        if (!Directory.Exists(schedulesPath))
        {
            Directory.CreateDirectory(schedulesPath);
        }
        //Debug.Log(Application.dataPath);
        string[] fileNames = Directory.GetFiles(schedulesPath, "*.csv");

        RemoveAllChidren(this.ModelScheduleListViewContent);
        foreach (string schedulePath in fileNames)
        {
            Button newButton = GameObject.Instantiate(this.StandardButtonPrefab, this.ModelScheduleListViewContent.transform);
            newButton.GetComponentInChildren<Text>().text = Path.GetFileNameWithoutExtension(schedulePath);
            scheduleFiles.Add(schedulePath, new Tuple<Button, bool>(newButton, false));
            UnityAction action = new UnityAction(() =>
            {
                scheduleFiles[schedulePath] = new Tuple<Button, bool>(scheduleFiles[schedulePath].Item1, !scheduleFiles[schedulePath].Item2);
                newButton.image.color = scheduleFiles[schedulePath].Item2 ? new Color(0, 250, 0) : new Color(255, 255, 255);
            });
            newButton.onClick.AddListener(action);
            scheduleButtons.Add(newButton);
        }
    }

    private void LoadLocalSchedule(string schedulePath, int count)
    {
        Dictionary<DateTime, List<ModelObjectScript>> newModelSchedule = new Dictionary<DateTime, List<ModelObjectScript>>();

        string[] lines = File.ReadAllLines(schedulePath);
        foreach (string line in lines)
        {
            string[] splitLine = line.Split(',');
            DateTime date = DateTime.Parse(splitLine[0]);
            string id = splitLine[1];
            if (!newModelSchedule.ContainsKey(date))
            {
                newModelSchedule.Add(date, new List<ModelObjectScript>());
            }
            if (ModelObjects[count].ContainsKey(id))
            {
                newModelSchedule[date].Add(ModelObjects[count][id]);
            }
            else
            {
                Debug.Log("ID not found: " + id);
            }
        }

        ModelSchedules.Add(newModelSchedule);
    }

    public async void StartClicked()
    {
        var selectedShedules = scheduleFiles.Where(sf => sf.Value.Item2).ToList();
        if (modelFile == null || selectedShedules.Count() == 0)
        {
            Debug.LogWarning("No Model or Schedule Selected");
            return;
        }

        LoadingCanvas.SetActive(true);
        await Task.Delay(10);

        Stopwatch s = new Stopwatch();
        s.Start();
        LoadLocalModel(modelFile, selectedShedules.Count());
        Debug.LogWarning("A: " + s.Elapsed.ToString());
        for (int i = 0; i < selectedShedules.Count(); i++)
        {
            s.Restart();
            Debug.Log(selectedShedules[i].Key + ": " + i.ToString());
            LoadLocalSchedule(selectedShedules[i].Key, i);
            Debug.LogWarning("B: " + s.Elapsed.ToString());
        }
        s.Restart();
        SetMaterialForTypes();
        Debug.LogWarning("C: " + s.Elapsed.ToString());

        s.Restart();
        HideSchedulelessObjects();
        Debug.LogWarning("D: " + s.Elapsed.ToString());

        DateSlider.minValue = (ModelSchedules.SelectMany(ms => ms.Keys).Min() - TimeSpan.FromDays(1)).Ticks;
        DateSlider.maxValue = (ModelSchedules.SelectMany(ms => ms.Keys).Max() + TimeSpan.FromDays(1)).Ticks;
        DateSlider.onValueChanged.AddListener(delegate { UpdateDisplayTime(); });
        DateSlider.value = ModelSchedules.SelectMany(ms => ms.Keys).Max().Ticks;

        LoadingCanvas.SetActive(false);
    }

    private void HideSchedulelessObjects()
    {
        List<ModelObjectScript> scheduleMos = ModelSchedules.SelectMany(m => m.Values.SelectMany(mos => mos)).Distinct().ToList();
        foreach (ModelObjectScript mos in ModelObjects.SelectMany(m => m.Values))
        {
            if (!scheduleMos.Contains(mos))
            {
                foreach (ComponentScript cs in mos.ComponentScripts)
                {
                    cs.SetMainMaterial(HighlightMatRed);
                }
            }

            mos.UnHighlight();
        }
    }

    public void StartStopClicked()
    {
        if (timeSpeed == 0.0f)
        {
            timeSpeed = 1.0f;
        }
        else
        {
            if (timeSpeed > 0.0f)
            {
                timeSpeed = 0.0f;
            }
        }
    }

    #endregion

    #region Model Load Methods

    private ModelObjectScript CreateModelObject(ModelObject obj, GameObject parentObj)
    {
        obj.Id = obj.Id ?? Guid.NewGuid().ToString();
        obj.Orientation = obj.Orientation ?? Utils.GetQuaterion(new Vector3D(0, 0, 1), 0.0 * Math.PI / 180.0);
        obj.Location = obj.Location ?? new Vector3D(0, 0, 0);
        GameObject moGO = Instantiate(ModelObjectPrefab, parentObj.transform);
        ModelObjectScript script = moGO.GetComponent<ModelObjectScript>();
        script.ModelObject = obj;
        script.ComponentScripts = CreateComponents(obj.Components, script);

        moGO.transform.SetPositionAndRotation(VectorConvert(obj.Location), VectorConvert(obj.Orientation));
        return script;
    }

    private List<ComponentScript> CreateComponents(List<Component> components, ModelObjectScript parentScript)
    {
        List<ComponentScript> componentScripts = new List<ComponentScript>();
        foreach (Component c in components)
        {
            GameObject meshObject = Instantiate(ModelComponentPrefab, parentScript.gameObject.transform);
            ComponentScript cs = meshObject.AddComponent<ComponentScript>();
            cs.Component = c;
            cs.CreateGameObject();

            Material material;
            if (c.MaterialId == null)
            {
                c.MaterialId = DbmsApi.API.Material.Default().Name;
            }
            if (MaterialDict.TryGetValue(c.MaterialId, out material))
            {
                cs.SetMainMaterial(material);
            }
            else
            {
                if (MaterialDict.TryGetValue(parentScript.ModelObject.TypeId, out material))
                {
                    cs.SetMainMaterial(material);
                }
                else
                {
                    cs.SetMainMaterial(DefaultMat);
                }
            }

            componentScripts.Add(cs);
        }

        return componentScripts;
    }

    private void SetMaterialForTypes()
    {
        MaterialDict = new Dictionary<string, Material>();
        foreach (var mosGroup in ModelObjects)
        {
            foreach (ModelObjectScript mos in mosGroup.Values)
            {
                if (!MaterialDict.ContainsKey(mos.ModelObject.TypeId))
                {
                    Material newMat = new Material(DefaultMat);
                    newMat.color = UnityEngine.Random.ColorHSV(0f, 1f, 0.2f, 0.2f, 0.9f, 0.9f);
                    MaterialDict[mos.ModelObject.TypeId] = newMat;
                }

                foreach (ComponentScript cs in mos.ComponentScripts)
                {
                    cs.SetMainMaterial(MaterialDict[mos.ModelObject.TypeId]);
                }
            }
        }
    }

    public static Vector3 VectorConvert(Vector3D v)
    {
        return new Vector3((float)v.x, (float)v.z, (float)v.y);
    }
    public static Quaternion VectorConvert(Vector4D v)
    {
        return new Quaternion((float)v.x, (float)v.z, (float)v.y, (float)v.w);
    }
    public static Vector3D VectorConvert(Vector3 v)
    {
        return new Vector3D((float)v.x, (float)v.z, (float)v.y);
    }
    public static Vector4D VectorConvert(Quaternion v)
    {
        return new Vector4D((float)v.x, (float)v.z, (float)v.y, (float)v.w);
    }

    #endregion

    #region Model View Mode

    private void ViewingMode()
    {
        ModelObjectScript mos;
        if (ViewingGameObject != null)
        {
            mos = ViewingGameObject.GetComponent<ModelObjectScript>();
            if (mos != null)
            {
                //mos.UnHighlight();
            }
        }

        foreach (Camera cam in cameras)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitData;
            if (Physics.Raycast(ray, out hitData, 1000))
            {
                GameObject hitObject = hitData.collider.gameObject;
                // Hit a model object component
                if (hitObject.transform.parent == null)
                {
                    Debug.Log("Test Object");
                    return;
                }
                ViewingGameObject = hitObject.transform.parent.gameObject;

                mos = ViewingGameObject.GetComponent<ModelObjectScript>();
                if (mos != null)
                {
                    //mos.Highlight(HighlightMatYellow);
                    DisplayObjectInfo(mos);
                }

                ComponentScript cs = hitObject.GetComponent<ComponentScript>();
                if (cs != null)
                {
                    //cs.Highlight(HighlightMatRed);
                    DisplayComponentInfo(cs);
                }
            }
        }
    }

    private void DisplayObjectInfo(ModelObjectScript mos)
    {
        if (mos == null)
        {
            return;
        }

        ObjectDataText.text = "Name: " + mos.ModelObject.Name + "\n";
        ObjectDataText.text += "Id: " + mos.ModelObject.Id + "\n";

        if (mos.ModelObject.GetType() == typeof(ModelCatalogObject))
        {
            ObjectDataText.text += "Catalog Id: " + ((ModelCatalogObject)mos.ModelObject).CatalogId + "\n";
        }

        ObjectDataText.text += "TypeId: " + mos.ModelObject.TypeId + "\n\n";
        foreach (Property p in mos.ModelObject.Properties)
        {
            ObjectDataText.text += p.Name + ": " + p.GetValueString() + "\n";
        }
    }

    private void DisplayComponentInfo(ComponentScript cs)
    {
        if (cs == null)
        {
            return;
        }

        ObjectDataText.text += "\nComponent Material: " + cs.Component.MaterialId + "\n";
        foreach (Property p in cs.Component.Properties)
        {
            ObjectDataText.text += p.Name + ": " + p.GetValueString() + "\n";
        }
    }

    private void UpdateDisplayTime()
    {
        DateTime displayTime = new DateTime((long)DateSlider.value);
        Dictionary<ModelObjectScript, ModelObjectScript> highlightedObjs = new Dictionary<ModelObjectScript, ModelObjectScript>();

        DateText.text = displayTime.ToString();
        foreach (var kvpair in ModelSchedules.SelectMany(kvp => kvp).OrderBy(ms => ms.Key))
        {
            if (kvpair.Key < displayTime)
            {
                foreach (ModelObjectScript mos in kvpair.Value)
                {
                    if (!highlightedObjs.ContainsKey(mos))
                    {
                        highlightedObjs.Add(mos, mos);
                    }
                    mos.UnHighlight();
                }
            }
            else
            {
                foreach (ModelObjectScript mos in kvpair.Value)
                {
                    if (highlightedObjs.ContainsKey(mos))
                    {
                        mos.Highlight(HighlightMatYellow);
                    }
                    else
                    {
                        mos.Highlight(InvisibleMat);
                    }
                }
            }
        }
    }

    #endregion

    #region Random Methods

    private static void RemoveAllChidren(GameObject obj)
    {
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    private static void ResetAllModelObjectTagAndLayers(List<ModelObjectScript> modelObjects)
    {
        foreach (ModelObjectScript moScript in modelObjects)
        {
            ChangeAllChidrenTagsAndLayer(moScript.gameObject, "Untagged", 0);
        }
    }

    private static void ChangeAllChidrenTagsAndLayer(GameObject obj, string newTag, int newLayer)
    {
        if (obj == null)
        {
            Debug.LogError("GameObject is Null");
        }

        obj.transform.tag = newTag;
        obj.layer = newLayer;
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            ChangeAllChidrenTagsAndLayer(child.gameObject, newTag, newLayer);
        }
        obj.transform.tag = newTag;
        obj.layer = newLayer;
    }

    private void ResetCanvas()
    {
        this.ModelViewCanvas.SetActive(false);
        this.ModelSelectCanvas.SetActive(false);

        UnHighlightAllObjects();

        ViewingGameObject = null;
    }

    private void UnHighlightAllObjects()
    {
        foreach (var mosGroup in ModelObjects)
        {
            foreach (ModelObjectScript mo in mosGroup.Values)
            {
                if (mo.IsHighlighted)
                {
                    mo.UnHighlight();
                }
            }
        }
    }

    #endregion

    // TESTING METHODS NOT OFFICIAL =======================================================================================================================

    public GameObject LinePrefab;
    private GameObject LineObjects;
    private void DrawLine(Vector3 start, Vector3 end, GameObject parent)
    {
        GameObject lineObj = Instantiate(LinePrefab, parent.transform);
        LineScript lineScript = lineObj.GetComponent<LineScript>();
        lineScript.StartPoint = start;
        lineScript.EndPoint = end;
    }
}