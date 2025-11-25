/// <summary>
/// Represent LDraw part mesh data.
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LDraw
{
    public class PartMesh
    {
        public Mesh Mesh { get; private set; }
        public Attribution Attribution { get; private set; }
        public List<string> SourceFiles { get; private set; }
        
        private List<Vector3> vertices = new List<Vector3>();
        private List<int> triangles = new List<int>();
        
        public float SmoothingAngleThreshold { get; set; } = 30f;

        public PartMesh()
        {
            Mesh = new Mesh();
            Attribution = new Attribution();
            SourceFiles = new List<string>();
        }

        public void LoadFromFile(string filePath, string libraryPath)
        {
            // Implementation for loading mesh data from an LDraw file.
            // This is a placeholder for actual loading logic.
            SourceFiles.Add(filePath);

            // Only accept .dat files for part meshes.
            if (!filePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only .dat files are supported for part meshes.", nameof(filePath));
            }

            try {
                // Flip the Y axis to convert from LDraw to Unity coordinate system.
                Matrix4x4 transform = Matrix4x4.Scale(new Vector3(1, -1, 1));

                DatFile file = new DatFile(filePath, libraryPath, this);
                file.Load(transform);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to load part mesh from file: {filePath}", ex);
            }

            GenerateMesh();
        }

        private void GenerateMesh()
        {
            var scaledVertices = new List<Vector3>(vertices.Count);
            foreach (var vertex in vertices)
            {
                // Scale from LDraw units (1 LDU = 0.4 mm) to Unity units (1 unit = 1 meter).
                scaledVertices.Add(vertex * 0.0004f);
            }
            
            Mesh.Clear();
            Mesh.SetVertices(scaledVertices);
            Mesh.SetTriangles(triangles, 0);
            Mesh.RecalculateBounds();
            
            // Calculate normals using original-scale vertices (to avoid degenerate triangles)
            CalculateNormalsWithAngleThreshold(vertices);
        }
        
        private class MeshTopology
        {
            // Maps position hash to list of vertex indices at that position
            public Dictionary<int, List<int>> PositionToVertices = new Dictionary<int, List<int>>();
            
            // Maps each vertex index to its neighboring vertices (sharing an edge)
            public Dictionary<int, HashSet<int>> VertexNeighbors = new Dictionary<int, HashSet<int>>();
            
            // Maps each vertex to the triangles it belongs to
            public Dictionary<int, List<int>> VertexToTriangles = new Dictionary<int, List<int>>();
            
            // Maps each edge (sorted vertex pair) to triangles that share it
            public Dictionary<(int, int), List<int>> EdgeToTriangles = new Dictionary<(int, int), List<int>>();
            
            // Stores face normals for each triangle
            public Vector3[] FaceNormals;
        }
        
        private MeshTopology BuildMeshTopology()
        {
            MeshTopology topology = new MeshTopology();
            const float positionEpsilon = 0.0001f;
            
            // Build position-to-vertices mapping
            for (int i = 0; i < vertices.Count; i++)
            {
                int posHash = GetPositionHash(vertices[i], positionEpsilon);
                if (!topology.PositionToVertices.ContainsKey(posHash))
                    topology.PositionToVertices[posHash] = new List<int>();
                topology.PositionToVertices[posHash].Add(i);
            }
            
            // Build vertex-to-triangles and edge-to-triangles mapping
            for (int triIdx = 0; triIdx < triangles.Count / 3; triIdx++)
            {
                int i0 = triangles[triIdx * 3];
                int i1 = triangles[triIdx * 3 + 1];
                int i2 = triangles[triIdx * 3 + 2];
                
                // Add to vertex-to-triangles
                AddToDict(topology.VertexToTriangles, i0, triIdx);
                AddToDict(topology.VertexToTriangles, i1, triIdx);
                AddToDict(topology.VertexToTriangles, i2, triIdx);
                
                // Add edges (use index-based for actual geometry)
                AddEdgeToTopology(topology, i0, i1, triIdx);
                AddEdgeToTopology(topology, i1, i2, triIdx);
                AddEdgeToTopology(topology, i2, i0, triIdx);
            }
            
            // Build vertex neighbors based on position proximity
            foreach (var posGroup in topology.PositionToVertices.Values)
            {
                foreach (int vertIdx in posGroup)
                {
                    if (!topology.VertexNeighbors.ContainsKey(vertIdx))
                        topology.VertexNeighbors[vertIdx] = new HashSet<int>();
                    
                    // Find all vertices at nearby positions
                    Vector3 pos = vertices[vertIdx];
                    foreach (var otherPosGroup in topology.PositionToVertices.Values)
                    {
                        foreach (int otherIdx in otherPosGroup)
                        {
                            if (otherIdx != vertIdx && Vector3.Distance(vertices[vertIdx], vertices[otherIdx]) < positionEpsilon)
                            {
                                topology.VertexNeighbors[vertIdx].Add(otherIdx);
                            }
                        }
                    }
                }
            }
            
            // Calculate face normals
            topology.FaceNormals = new Vector3[triangles.Count / 3];
            for (int triIdx = 0; triIdx < triangles.Count / 3; triIdx++)
            {
                Vector3 v0 = vertices[triangles[triIdx * 3]];
                Vector3 v1 = vertices[triangles[triIdx * 3 + 1]];
                Vector3 v2 = vertices[triangles[triIdx * 3 + 2]];
                
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
                topology.FaceNormals[triIdx] = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            }
            
            return topology;
        }
        
        private int GetPositionHash(Vector3 position, float epsilon)
        {
            // Quantize position to grid for consistent hashing
            int x = Mathf.RoundToInt(position.x / epsilon);
            int y = Mathf.RoundToInt(position.y / epsilon);
            int z = Mathf.RoundToInt(position.z / epsilon);
            return HashCode.Combine(x, y, z);
        }
        
        private void AddToDict<T>(Dictionary<int, List<T>> dict, int key, T value)
        {
            if (!dict.ContainsKey(key))
                dict[key] = new List<T>();
            dict[key].Add(value);
        }
        
        private void AddEdgeToTopology(MeshTopology topology, int v0, int v1, int triIdx)
        {
            var edge = v0 < v1 ? (v0, v1) : (v1, v0);
            if (!topology.EdgeToTriangles.ContainsKey(edge))
                topology.EdgeToTriangles[edge] = new List<int>();
            topology.EdgeToTriangles[edge].Add(triIdx);
        }
        
        private void CalculateNormalsWithAngleThreshold(List<Vector3> verts)
        {
            Vector3[] normals = new Vector3[verts.Count];
            float cosThreshold = Mathf.Cos(SmoothingAngleThreshold * Mathf.Deg2Rad);
            
            // Calculate face normals for each triangle
            Vector3[] faceNormals = new Vector3[triangles.Count / 3];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 v0 = verts[triangles[i]];
                Vector3 v1 = verts[triangles[i + 1]];
                Vector3 v2 = verts[triangles[i + 2]];
                
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
                if (normal.sqrMagnitude > 0.0001f)
                {
                    faceNormals[i / 3] = normal.normalized;
                }
                else
                {
                    faceNormals[i / 3] = Vector3.up;
                }
            }
            
            // For each vertex, calculate its normal by averaging nearby face normals within angle threshold
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 vertPos = verts[i];
                
                // Find the face this vertex belongs to
                int ownTriIndex = -1;
                for (int triIndex = 0; triIndex < triangles.Count; triIndex += 3)
                {
                    if (triangles[triIndex] == i || triangles[triIndex + 1] == i || triangles[triIndex + 2] == i)
                    {
                        ownTriIndex = triIndex / 3;
                        break;
                    }
                }
                
                if (ownTriIndex == -1)
                {
                    normals[i] = Vector3.up;
                    continue;
                }
                
                Vector3 ownNormal = faceNormals[ownTriIndex];
                
                // Find all nearby normals that are within the threshold of MY normal
                List<Vector3> smoothableNormals = new List<Vector3> { ownNormal };
                
                // Look at all other triangles and see if any vertices are close to this vertex
                for (int triIndex = 0; triIndex < triangles.Count; triIndex += 3)
                {
                    int triId = triIndex / 3;
                    if (triId == ownTriIndex) continue;
                    
                    // Check if this triangle has a vertex close to our vertex
                    bool isNearby = false;
                    for (int j = 0; j < 3; j++)
                    {
                        Vector3 otherVert = verts[triangles[triIndex + j]];
                        if (Vector3.Distance(vertPos, otherVert) < 0.0001f)
                        {
                            isNearby = true;
                            break;
                        }
                    }
                    
                    if (!isNearby) continue;
                    
                    // Check if the face normal is within the angle threshold of MY normal
                    Vector3 otherNormal = faceNormals[triId];
                    float dot = Vector3.Dot(ownNormal, otherNormal);
                    
                    if (dot >= cosThreshold)
                    {
                        smoothableNormals.Add(otherNormal);
                    }
                }
                
                // Average all smoothable normals
                Vector3 smoothNormal = Vector3.zero;
                foreach (var normal in smoothableNormals)
                {
                    smoothNormal += normal;
                }
                
                normals[i] = smoothNormal.sqrMagnitude > 0.0001f ? smoothNormal.normalized : Vector3.up;
            }
            
            Mesh.normals = normals;
        }

        public void AddTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int baseIndex = vertices.Count;
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
        }

        public void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
        {
            int baseIndex = vertices.Count;
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            vertices.Add(v4);
            // First triangle
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            // Second triangle
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }
    }
}
