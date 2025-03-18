using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DbmsApi.API;
using Component = DbmsApi.API.Component;
using Material = UnityEngine.Material;
using MathPackage;
using Mesh = UnityEngine.Mesh;
using Debug = UnityEngine.Debug;

public class ComponentScript : MonoBehaviour
{
    public Component Component;
    private Material MainMaterial;
    private MeshRenderer MeshRenderer;
    public bool IsHighlighted;

    private void Start()
    {
        MeshRenderer = GetComponent<MeshRenderer>();
    }

    private void Awake()
    {
        MeshRenderer = GetComponent<MeshRenderer>();
    }

    public void CreateGameObject()
    {
        Mesh mesh = new Mesh();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        //Debug.Log("Before: " + Component.Vertices.Count);
        Component.VertexAdderByTri();
        //Debug.Log("After: " + Component.Vertices.Count);

        mesh.SetVertices(Component.Vertices.Select(v => GameController.VectorConvert(v)).ToArray());
        mesh.SetTriangles(Component.Triangles.SelectMany(t => new List<int>() { t[0], t[1], t[2] }).Reverse().ToArray(), 0);
        //mesh.SetUVs(0,mesh.vertices.Select(v => new Vector2(v.x, v.y)).ToArray());
        mesh.SetUVs(0, CalculateUVs(mesh, mesh.vertices.ToList()));
        //mesh.SetNormals(CalculateNormals(mesh, mesh.vertices.ToList()));

        FixShape(mesh);
    }

    private void FixShape(Mesh mesh)
    {
        mesh.Optimize();

        //mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        mesh.Optimize();
        //mesh.OptimizeReorderVertexBuffer();
        //mesh.OptimizeIndexBuffers();
    }

    private static Vector3[] CalculateNormals(Mesh mesh, List<Vector3> vs)
    {
        Vector3[] normals = new Vector3[vs.Count];
        int len = mesh.GetTriangles(0).Length;
        int[] idxs = mesh.GetTriangles(0);
        for (int i = 0; i < len; i = i + 3)
        {
            Vector3 v1 = vs[idxs[i + 0]];
            Vector3 v2 = vs[idxs[i + 1]];
            Vector3 v3 = vs[idxs[i + 2]];
            Vector3 normal = Vector3.Cross(v3 - v1, v2 - v1);
            normal.Normalize();
            normals[idxs[i + 0]] = normal;
            normals[idxs[i + 1]] = normal;
            normals[idxs[i + 2]] = normal;
        }

        return normals;
    }

    private static Vector2[] CalculateUVs(Mesh mesh, List<Vector3> vs)
    {
        float scaleFactor = 0.5f;
        Vector2[] uvs = new Vector2[vs.Count];
        int len = mesh.GetTriangles(0).Length;
        int[] idxs = mesh.GetTriangles(0);
        for (int i = 0; i < len; i = i + 3)
        {
            Vector3 v1 = vs[idxs[i + 0]];
            Vector3 v2 = vs[idxs[i + 1]];
            Vector3 v3 = vs[idxs[i + 2]];
            Vector3 normal = Vector3.Cross(v3 - v1, v2 - v1);
            Quaternion rotation;
            if (normal == Vector3.zero)
                rotation = new Quaternion();
            else
                rotation = Quaternion.Inverse(Quaternion.LookRotation(normal));
            uvs[idxs[i + 0]] = (Vector2)(rotation * v1) * scaleFactor;
            uvs[idxs[i + 1]] = (Vector2)(rotation * v2) * scaleFactor;
            uvs[idxs[i + 2]] = (Vector2)(rotation * v3) * scaleFactor;
        }

        return uvs;
    }

    public void SetMainMaterial(Material material)
    {
        MainMaterial = material;
    }

    public void Highlight(Material material)
    {
        IsHighlighted = true;
        MeshRenderer.material = material;
    }

    public void UnHighlight()
    {
        IsHighlighted = false;
        MeshRenderer.material = MainMaterial;
    }
}