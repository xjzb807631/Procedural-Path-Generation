﻿

using System;
using System.Collections.Generic;
using UnityEngine;
//We can simply generate all Segments as a Bezier Curve with a equivalent length for all the tangent of nodes.
//Cubic Bezier Curve Equation Reference: https://en.wikipedia.org/wiki/B%C3%A9zier_curve
[ExecuteInEditMode]
public class RoadSegment: MonoBehaviour
{
    private static readonly float CurveElaborationCoefficient = 0.3f;
    private static Mesh2D _mesh2D;
    public static Mesh2D Mesh2D{
        get
        {
            if (_mesh2D == null || !_mesh2D)
            {
                _mesh2D=Resources.Load<Mesh2D>("Road Shape");
            }
            return _mesh2D;
        }
    }
    private static Material _roadMaterial;
    public static Material RoadMaterial{
        get
        {
            if (_roadMaterial == null || !_roadMaterial)
            {
                _roadMaterial=Resources.Load<Material>("");
            }
            return _roadMaterial;
        }
    }
    private static Material _blueprintMaterial;
    public static Material BlueprintMaterial{
        get
        {
            if (_blueprintMaterial == null || !_blueprintMaterial)
            {
                _blueprintMaterial=Resources.Load<Material>("blueprint");
            }
            return _blueprintMaterial;
        }
    }
    [SerializeField]
    public Transform head;
    [SerializeField]
    public Transform tail;
    [SerializeField]
    private float TextureTileScale=10f;
    [SerializeField]
    private float MeshTileScale=2f;
    private BezierCurve curve;
    private MeshFilter _meshFilter;
    private Mesh _mesh;
    [SerializeField] [HideInInspector]
    private int ownerID;
    private Mesh mesh
    {
        get
        {
            if (_meshFilter==null)
                _meshFilter = GetComponent<MeshFilter>();
            
            bool isOwner = ownerID == gameObject.GetInstanceID();
            bool filterHasMesh = _meshFilter.sharedMesh != null;
            if (!isOwner || !filterHasMesh)
            {
                ownerID = gameObject.GetInstanceID(); // Mark self as owner of this mesh
                _mesh = _meshFilter.sharedMesh = new Mesh();
                _mesh.name = "Mesh [" + ownerID + "]";
                _mesh.hideFlags = HideFlags.HideAndDontSave; 
                _mesh.MarkDynamic();
            }else if( isOwner && filterHasMesh && _mesh== null ) {
                _mesh =_meshFilter.sharedMesh;
            }

            return _mesh;
        }
    }

    public float Length => (head.position - tail.position).magnitude;
    public float OffsetV;
    public void SetRoadSegment()
    {
        if (head != null && tail != null)
        {
            curve = new BezierCurve(head, tail, transform,
                (head.position - tail.position).magnitude / 2 /*Hard - Code*/);
            GenerateMesh();
        }
    }
    

    public void OnValidate()
    {
        SetRoadSegment();
    }

    public void Update()
    {
        if (head.hasChanged || tail.hasChanged)
            OnValidate();
    }

    public void DestroySelf()
    {
        var node = tail.GetComponent<RoadNode>();
        if (node ==null || node.segmentForward == null )
            Destroy(tail.gameObject);
        node = head.GetComponent<RoadNode>();
        if (node ==null || node.segmentBackward == null)
            Destroy(head.gameObject);
        Destroy(gameObject);
    }


    public void GenerateMesh()
    {
        
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        // meshRenderer.sharedMaterial = new Material(RoadMaterial);
        
        //The Main part of Mesh Generation
        {
            mesh.Clear();
            int division = Mathf.CeilToInt(Length * (1 + curve.EstimatedCurvature * CurveElaborationCoefficient) * MeshTileScale);
            Vector3[] vertices = new Vector3[division * Mesh2D.VertexCount];
            int[] indices = new int[(division-1) * Mesh2D.VertexCount * 3 ];
            Vector3[] tangents = new Vector3[division * Mesh2D.VertexCount];
            Vector3[] normals = new Vector3[division * Mesh2D.VertexCount];
            Vector2[] uvs0 = new Vector2[division * Mesh2D.VertexCount];
            Vector2[] uvs1 = new Vector2[division * Mesh2D.VertexCount];
            float deltaT = 1f / (division-1);
            float t;
            var UVoffsetV = OffsetV;
            for (int i = 0; i < division; i++)
            {
                t = i * deltaT;
                var rootVertexIndex = i * Mesh2D.VertexCount;
                //ensure there would be smooth and no gap  between segments
                if (i == division - 1)
                    t = 1;
                var pointRef = curve.GetPoint(t);
                
                // Generate vertices & normals
                for (int innerIndex = 0; innerIndex < Mesh2D.VertexCount; innerIndex++)
                {
                    Quaternion rotation;
                    if (i == 0)
                        rotation = Quaternion.LookRotation(curve.GetTangent(0));
                    else
                        rotation= Quaternion.LookRotation(curve.GetTangent(t));
                    var mesh2DVertex = Mesh2D.vertices[innerIndex];
                    var offset = (rotation * new Vector3(mesh2DVertex.point.x, mesh2DVertex.point.y, 0));
                    vertices[rootVertexIndex + innerIndex] = pointRef + new Vector3(offset.x,offset.y,offset.z);
                    normals[rootVertexIndex + innerIndex] = rotation * mesh2DVertex.normal;
                }
                
                // Generate UVs
                if (i != 0)
                    // We need to keep middle lines in the same size for each tile
                {
                    //Find the mid point of the road
                    var currentMidPoint = (vertices[rootVertexIndex] + vertices[rootVertexIndex + Mesh2D.VertexCount - 1]) / 2;
                    var lastMidPoint = (vertices[rootVertexIndex - Mesh2D.VertexCount] + vertices[rootVertexIndex - 1]) / 2;
                    
                    UVoffsetV += Vector3.Distance(currentMidPoint, lastMidPoint) /
                                 TextureTileScale;
                }
                
                for (int innerIndex = 0; innerIndex < Mesh2D.VertexCount; innerIndex++)
                {
                    uvs0[rootVertexIndex + innerIndex] = new Vector2(Mesh2D.vertices[innerIndex].u, UVoffsetV);
                }
                
                // Generate Triangles for Mesh
                var rootTriangleIndex = i * Mesh2D.VertexCount * 3;
                if (i<division-1)
                    for (int innerIndex = 0; innerIndex < Mesh2D.VertexCount; innerIndex += 2)
                    {
                        // Generate a quad each loop
                        var indexTri = rootTriangleIndex + innerIndex * 3;
                        int lineIndexA = Mesh2D.lineIndices[innerIndex];
                        int lineIndexB = Mesh2D.lineIndices[innerIndex + 1];
                        indices[indexTri] = rootVertexIndex + lineIndexB;
                        indices[indexTri + 1] = rootVertexIndex+lineIndexA;
                        indices[indexTri + 2] = rootVertexIndex + lineIndexB + Mesh2D.LineCount;
                        indices[indexTri + 3] = rootVertexIndex + lineIndexA + Mesh2D.LineCount;
                        indices[indexTri + 4] = rootVertexIndex + lineIndexB + Mesh2D.LineCount;
                        indices[indexTri + 5] = rootVertexIndex + lineIndexA;
                    }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices,0);
            mesh.SetNormals(normals);
            mesh.SetUVs(0,uvs0);
        }
    }
    public void OnDrawGizmosSelected()
    {
        var vertices = new List<Vector3>();
        mesh.GetVertices(vertices);
        foreach (var vertex in vertices)
        {
            Gizmos.DrawSphere(transform.TransformPoint(vertex),0.1f);
        }
        curve.DrawGizmo();
    }

    public void GenerateShortSegment(float SegmentLength)
    {
        if (tail == null)
        {
            tail=new GameObject("Node").transform;
            tail.SetPositionAndRotation(head.position+head.forward*SegmentLength,head.rotation);
        }
        
        if (head == null)
        {
            head=new GameObject("Node").transform;
            head.SetPositionAndRotation(tail.position - tail.forward * SegmentLength,tail.rotation);
        }

        SetRoadSegment();
    }

    public void SetBlueprint()
    {
        GetComponent<MeshRenderer>().sharedMaterial = BlueprintMaterial;
    }

    public void SetNode(RoadNode node, bool isHead)
    {
        SetNode(node.transform, isHead);
    }
    public void SetNode(Transform node, bool isHead)
    {
        if (isHead)
            head = node;
        else
            tail = node;
    }
    
}