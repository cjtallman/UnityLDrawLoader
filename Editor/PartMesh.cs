/// <summary>
/// Represent LDraw part mesh data.
/// </summary>

using System;
using System.Collections.Generic;
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
            
            Mesh.SetVertices(scaledVertices);
            Mesh.SetTriangles(triangles, 0);
            Mesh.RecalculateNormals();
            Mesh.RecalculateBounds();
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
