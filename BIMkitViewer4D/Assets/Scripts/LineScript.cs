using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class LineScript : MonoBehaviour
{
    public Vector3 StartPoint;
    public Vector3 EndPoint;

    private LineRenderer LineRenderer;

    // Start is called before the first frame update
    void Start()
    {
        LineRenderer = GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        LineRenderer.SetPosition(0, StartPoint);
        LineRenderer.SetPosition(1, EndPoint);
    }
}