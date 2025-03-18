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
    string scheduleFile = null;

    private Model CurrentModel;
    public GameObject CurrentModelGameObj;
    private List<ModelObjectScript> ModelObjects = new List<ModelObjectScript>();
    private GameObject ViewingGameObject;

    private Dictionary<DateTime, List<ModelObjectScript>> ModelSchedule = new Dictionary<DateTime, List<ModelObjectScript>>();

    public float cameraRotateSpeed = 1000f;
    public float cameraMoveSpeed = 100f;
    public float cameraScrollSensitivity = 10f;

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
            foreach (ModelObject mo in CurrentModel.ModelObjects.OrderBy(mo=>mo.Location.z))
            {
                csvOut.Add(currentTime.ToString() + "," + mo.Id);
                currentTime += TimeSpan.FromDays(5);
            }

            File.WriteAllLines(Application.dataPath + "/..//Schedules/TEST.csv", csvOut);
            GetLocalSchedules();
        }
    }

    // Update is called once per frame
    void Update()
    {
        MoveCamera();
        if (this.ModelViewCanvas.activeInHierarchy)
        {
            ViewingMode();
        }
    }

    #region Camera Controls

    private void MoveCamera()
    {
        if (Input.GetMouseButton(1))
        {
            MainCamera.transform.Rotate(new Vector3(-Input.GetAxis("Mouse Y") * cameraRotateSpeed * Time.deltaTime, Input.GetAxis("Mouse X") * cameraRotateSpeed * Time.deltaTime, 0));
            float X = MainCamera.transform.rotation.eulerAngles.x;
            float Y = MainCamera.transform.rotation.eulerAngles.y;
            MainCamera.transform.rotation = Quaternion.Euler(X, Y, 0);
        }

        if (Input.GetMouseButton(2))
        {
            var newPosition = new Vector3();
            newPosition.x = Input.GetAxis("Mouse X") * cameraMoveSpeed * Time.deltaTime;
            newPosition.y = Input.GetAxis("Mouse Y") * cameraMoveSpeed * Time.deltaTime;
            MainCamera.transform.Translate(-newPosition);
        }

        MainCamera.transform.position += MainCamera.transform.forward * Input.GetAxis("Mouse ScrollWheel") * cameraScrollSensitivity;
    }

    private void SetupMainCamera()
    {
        JumpCameraToObject(ModelObjects);
    }

    public void JumpCameraToObject(List<ModelObjectScript> mos)
    {
        List<Vector3D> vList = mos.SelectMany(m => m.ModelObject.Components.SelectMany(c => c.Vertices.Select(v => Vector3D.Add(v, m.ModelObject.Location)))).ToList();
        Utils.GetXYZDimentions(vList, out Vector3D mid, out Vector3D dims);

        Vector3 center = VectorConvert(mid);
        Vector3 diment = VectorConvert(dims);

        MainCamera.orthographic = false;
        MainCamera.nearClipPlane = 0.1f;
        MainCamera.farClipPlane = 10000.0f;
        //MainCamera.transform.position = new Vector3(center.x, center.y + 2.0f * diment.y, center.z);
        MainCamera.transform.position = new Vector3(center.x, center.y + Mathf.Max(diment.x, diment.z) / 2.0f, center.z);
        MainCamera.transform.LookAt(center, Vector3.up);
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

    private void LoadLocalModel(string modelPath)
    {
        CurrentModel = DBMSReadWrite.JSONReadFromFile<Model>(modelPath);
        CurrentModel.Id = null; // Make sure the local models dont conflict with the existing (incase they happen to have the same ID)

        RemoveAllChidren(CurrentModelGameObj);

        ModelObjects = new List<ModelObjectScript>();
        foreach (ModelObject obj in CurrentModel.ModelObjects)
        {
            if (obj.GetType() == typeof(ModelCatalogObject))
            {
                string catId = (obj as ModelCatalogObject).CatalogId;
                Debug.LogWarning("Missing Item: " + catId);
            }

            ModelObjectScript script = CreateModelObject(obj, CurrentModelGameObj);
            ModelObjects.Add(script);
        }

        SetupMainCamera();

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
            UnityAction action = new UnityAction(() =>
            {
                scheduleFile = schedulePath;
                foreach (Button sb in scheduleButtons)
                {
                    sb.image.color = new Color(255, 255, 255);
                }
                newButton.image.color = new Color(0, 250, 0);
            });
            newButton.onClick.AddListener(action);
            scheduleButtons.Add(newButton);
        }
    }

    private void LoadLocalSchedule(string schedulePath)
    {
        ModelSchedule = new Dictionary<DateTime, List<ModelObjectScript>>();

        string[] lines = File.ReadAllLines(schedulePath);
        foreach (string line in lines)
        {
            string[] splitLine = line.Split(',');
            DateTime date = DateTime.Parse(splitLine[0]);
            string id = splitLine[1];
            if (!ModelSchedule.ContainsKey(date))
            {
                ModelSchedule.Add(date, new List<ModelObjectScript>());
            }
            ModelSchedule[date].AddRange(ModelObjects.FindAll(mo=>mo.ModelObject.Id == id).ToList());
        }
    }

    public async void StartClicked()
    {
        if (modelFile == null || scheduleFile == null)
        {
            Debug.LogWarning("No Model or Schedule Selected");
            return;
        }

        LoadingCanvas.SetActive(true);
        await Task.Delay(10);

        LoadLocalModel(modelFile);
        SetMaterialForTypes();
        LoadLocalSchedule(scheduleFile);

        DateSlider.minValue = (ModelSchedule.Min(kvp => kvp.Key) - TimeSpan.FromDays(1)).Ticks;
        DateSlider.maxValue = (ModelSchedule.Max(kvp => kvp.Key) + TimeSpan.FromDays(1)).Ticks;
        DateSlider.onValueChanged.AddListener(delegate { UpdateDisplayTime(); });
        DateSlider.value = ModelSchedule.Max(kvp => kvp.Key).Ticks;

        LoadingCanvas.SetActive(false);
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
        MaterialDict["Floor"] = DefaultMat;
        foreach (ModelObjectScript mos in ModelObjects)
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

        Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);
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
        DateTime displayTime = new DateTime((long) DateSlider.value);

        foreach (var kvpair in ModelSchedule)
        {
            if (kvpair.Key < displayTime)
            {
                foreach (ModelObjectScript mos in kvpair.Value)
                {
                    mos.UnHighlight();
                }
            }
            else
            {
                foreach (ModelObjectScript mos in kvpair.Value)
                {
                    mos.Highlight(InvisibleMat);
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
        foreach (ModelObjectScript mo in ModelObjects)
        {
            if (mo.IsHighlighted)
            {
                mo.UnHighlight();
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