using DbmsApi.API;
using MathPackage;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Material = UnityEngine.Material;

public class ModelObjectScript : MonoBehaviour
{
    public ModelObject ModelObject
    {
        get
        {
            return modelObject;
        }
        set
        {
            modelObject = value;
            this.name = value.Name;
        }
    }
    private ModelObject modelObject;
    public List<ComponentScript> ComponentScripts;
    public bool IsHighlighted = false;

    public GameObject LinePrefab;
    private List<LineRenderer> LineRenderers;

    // Start is called before the first frame update
    void Start()
    {
        //LineRenderers = new List<LineRenderer>();
        //for (int i = 0; i < 3; i++)
        //{
        //    LineRenderer lineRenderer = Instantiate(LinePrefab, transform).GetComponent<LineRenderer>();

        //    Color color = i == 0 ? Color.red : (i == 1 ? Color.blue : Color.green);
        //    lineRenderer.startColor = color;
        //    lineRenderer.endColor = color;
        //    LineRenderers.Add(lineRenderer);
        //}

        //UpdateLines();
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.hasChanged)
        {
            //UpdateLines();
            transform.hasChanged = false;
        }
    }

    public void UpdateLines()
    {
        Vector3 location = GameController.VectorConvert(modelObject.Location);
        Matrix4 orientationMatrix = Utils.GetTranslationMatrixFromLocationOrientation(new Vector3D(0, 0, 0), modelObject.Orientation);
        Vector3D LeftDirectionX = Matrix4.Multiply(orientationMatrix, new Vector4D(1, 0, 0, 1));
        Vector3D ForwardDirectionY = Matrix4.Multiply(orientationMatrix, new Vector4D(0, 1, 0, 1));
        Vector3D UpDirectionZ = Matrix4.Multiply(orientationMatrix, new Vector4D(0, 0, 1, 1));
        Vector3 left = GameController.VectorConvert(LeftDirectionX);
        Vector3 forward = GameController.VectorConvert(ForwardDirectionY);
        Vector3 up = GameController.VectorConvert(UpDirectionZ);
        LineRenderers[0].SetPosition(0, location);
        LineRenderers[0].SetPosition(1, left * 0.5f + location);
        LineRenderers[1].SetPosition(0, location);
        LineRenderers[1].SetPosition(1, forward * 0.5f + location);
        LineRenderers[2].SetPosition(0, location);
        LineRenderers[2].SetPosition(1, up * 0.5f + location);
    }

    private Material currentHighlightMat = null;
    public void Highlight(Material material)
    {
        IsHighlighted = true;
        foreach (ComponentScript cs in ComponentScripts)
        {
            cs.Highlight(material);
        }
    }

    public void UnHighlight()
    {
        IsHighlighted = false;
        foreach (ComponentScript cs in ComponentScripts)
        {
            cs.UnHighlight();
        }
    }
}
