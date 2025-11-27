// Author: cjtallman
// Copyright (c) 2025 Chris Tallman
// Last Modified: 2025/11/27
// License: MIT License
// Summary: Container for loading LDraw mesh data

using System.Collections.Generic;
using UnityEngine;

namespace LDraw.Editor
{
    public class PartMeshData
    {
        public float SmoothingAngleThreshold { get; set; } = 30f;
        private Mesh Mesh { get; set; } = new Mesh();
        private List<Vector3> vertices = new List<Vector3>();
        private List<int> triangles = new List<int>();

        public Mesh GenerateMesh()
        {
            var scaledVertices = new List<Vector3>(vertices.Count);
            foreach (var vertex in vertices)
            {
                // Scale from LDraw units (1 LDU = 0.4 mm) to Unity units (1 unit = 1 meter).
                scaledVertices.Add(vertex * LDrawSettings.ScaleFactor);
            }

            Mesh = new();
            Mesh.SetVertices(scaledVertices);
            Mesh.SetTriangles(triangles, 0);
            Mesh.RecalculateBounds();

            // Calculate normals using original-scale vertices (to avoid degenerate triangles)
            CalculateNormalsWithAngleThreshold(vertices);

            return Mesh;
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
