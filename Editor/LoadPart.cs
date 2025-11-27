// Author: cjtallman
// Copyright (c) 2025 Chris Tallman
// Last Modified: 2025/11/26
// License: MIT License
// Summary: Load LDraw .dat files

using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LDraw.Editor
{
    public class DatFile
    {
        private PartMeshData Part;
        private string FilePath;
        private string LibraryPath;
        private bool Invert = false;
        private bool InvertNext = false;
        private Matrix4x4 CurrentTransform = Matrix4x4.identity;

        public DatFile(string filePath, string libraryPath, PartMeshData partMesh = null)
        {
            if (!filePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only .dat files are supported.", nameof(filePath));
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (string.IsNullOrEmpty(libraryPath))
            {
                throw new ArgumentException("Library path cannot be null or empty.", nameof(libraryPath));
            }

            if (!System.IO.File.Exists(filePath))
            {
                throw new System.IO.FileNotFoundException("The specified .dat file was not found.", filePath);
            }

            if (!System.IO.Directory.Exists(libraryPath))
            {
                throw new System.IO.DirectoryNotFoundException("The specified LDraw library path was not found: " + libraryPath);
            }

            FilePath = filePath.Replace('/', '\\');
            LibraryPath = libraryPath.Replace('/', '\\');
            Part = partMesh;
        }

        public static Mesh LoadMeshFromFile(string filePath, string libraryPath)
        {
            // Only accept .dat files for part meshes.
            if (!filePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string assetPath = $"{LDrawSettings.PartAssetsFolder}/{fileName}_mesh.asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existingMesh != null)
            {
                return existingMesh;
            }

            PartMeshData partMesh = new();
            DatFile datFile = new DatFile(filePath, libraryPath, partMesh);

            try
            {
                // Flip the Y axis to convert from LDraw to Unity coordinate system.
                datFile.Load(Matrix4x4.Scale(new Vector3(1, -1, 1)), false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load part mesh from file: {filePath}", ex);
            }

            Mesh newMesh = partMesh.GenerateMesh();

            // Save the mesh as an asset
            SaveMeshAsAsset(newMesh, assetPath, fileName);

            return newMesh;
        }

        /// <summary>
        /// Saves a mesh as an asset in the specified path
        /// </summary>
        /// <param name="mesh">Mesh to save</param>
        /// <param name="assetPath">Path where to save the asset</param>
        /// <param name="fileName">Name of the file for logging</param>
        private static void SaveMeshAsAsset(Mesh mesh, string assetPath, string fileName)
        {
            try
            {
                // Ensure the Parts folder exists
                LDrawSettings.EnsureAssetsFolderExists(LDrawSettings.PartAssetsFolder);

                // Create the asset
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"Created mesh asset for part: {fileName} at {assetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save mesh asset for {fileName}: {ex}");
            }
        }

        public void Load(Matrix4x4 transform, bool invert = false)
        {
            Invert = invert;
            CurrentTransform = transform;

            // Load the file and parse its contents.
            using (var reader = new System.IO.StreamReader(FilePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    ParseLine(line);
                }
            }
        }

        /// <summary>
        /// Parses a single line from the LDraw file.
        /// </summary>
        /// <param name="line">The line to parse.</param>
        private void ParseLine(string line)
        {
            // Trim whitespace and split the line into parts.
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
            {
                return; // Skip empty lines.
            }

            switch (trimmedLine[0])
            {
                case '0':
                    HandleComment(trimmedLine);
                    break;
                case '1':
                    HandleSubFile(trimmedLine);
                    InvertNext = false;
                    break;
                case '2':
                case '5':
                    break;
                case '3':
                    HandleTriangle(trimmedLine);
                    InvertNext = false;
                    break;
                case '4':
                    HandleQuad(trimmedLine);
                    InvertNext = false;
                    break;
                default:
                    break;
            }
        }

        private string FindPartFile(string fileName)
        {
            // file is relative to library path
            string filePath = System.IO.Path.Combine(LibraryPath, fileName);
            if (System.IO.File.Exists(filePath))
            {
                return filePath;
            }

            // try parts subdirectory
            filePath = System.IO.Path.Combine(LibraryPath, "parts", fileName);
            if (System.IO.File.Exists(filePath))
            {
                return filePath;
            }

            // try p subdirectory
            filePath = System.IO.Path.Combine(LibraryPath, "p", fileName);
            if (System.IO.File.Exists(filePath))
            {
                return filePath;
            }

            return null;
        }

        private void HandleComment(string line)
        {
            string[] tokens = line.Split(' ');
            if (tokens.Length < 2)
            {
                return;
            }

            // Sanity check: We shouldn't get here without a '0' at the start.
            if (tokens[0] != "0")
            {
                return;
            }

            switch (tokens[1].ToUpperInvariant())
            {
                case "BFC":
                    HandleBfcCommand(line.Trim());
                    return;

                default:
                    return;
            }
        }

        private void HandleBfcCommand(string line)
        {
            // Match:
            // `0 BFC (NOCERTIFY | CERTIFY [CW|{CCW}])`
            // `0 BFC ((CW|CCW) | CLIP [(CW|CCW)] | NOCLIP | INVERTNEXT)`
            Regex regex = new Regex(@"^0\s+BFC\s+(NOCERTIFY|CERTIFY\s+(CW|CCW)?|(CW|CCW)|CLIP\s+(CW|CCW)?|NOCLIP|INVERTNEXT)$", RegexOptions.IgnoreCase);
            Match match = regex.Match(line);
            if (!match.Success)
            {
                return; // Invalid BFC command.
            }

            string command = match.Groups[1].Value.ToUpperInvariant();
            switch (command)
            {
                case "INVERTNEXT":
                    InvertNext = true;
                    break;
            }
        }

        private void HandleSubFile(string line)
        {
            // Match `1 <colour> x y z a b c d e f g h i <file>`
            Regex regex = new Regex(@"^(1)\s+(-?\d+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+(.+)$");
            Match match = regex.Match(line);
            if (!match.Success)
            {
                return; // Invalid sub-file line.
            }

            string fileName = match.Groups[15].Value.Trim();
            string filePath = FindPartFile(fileName);
            if (filePath == null)
            {
                Debug.LogWarning($"Sub-file not found: {fileName} referenced in {FilePath}");
                return;
            }

            // Parse transformation matrix components.
            float x = float.Parse(match.Groups[3].Value);
            float y = float.Parse(match.Groups[4].Value);
            float z = float.Parse(match.Groups[5].Value);
            float a = float.Parse(match.Groups[6].Value);
            float b = float.Parse(match.Groups[7].Value);
            float c = float.Parse(match.Groups[8].Value);
            float d = float.Parse(match.Groups[9].Value);
            float e = float.Parse(match.Groups[10].Value);
            float f = float.Parse(match.Groups[11].Value);
            float g = float.Parse(match.Groups[12].Value);
            float h = float.Parse(match.Groups[13].Value);
            float i = float.Parse(match.Groups[14].Value);
            Matrix4x4 transform = new();
            transform.SetRow(0, new Vector4(a, b, c, x));
            transform.SetRow(1, new Vector4(d, e, f, y));
            transform.SetRow(2, new Vector4(g, h, i, z));
            transform.SetRow(3, new Vector4(0, 0, 0, 1));

            bool shouldInvert = Invert ^ InvertNext;
            if (transform.determinant < 0)
            {
                shouldInvert = !shouldInvert;
            }

            DatFile subFile = new DatFile(filePath, LibraryPath, Part);
            subFile.Load(CurrentTransform * transform, shouldInvert);

            InvertNext = false; // Reset the flag.
        }

        private void HandleTriangle(string line)
        {
            // Match `3 <colour> x1 y1 z1 x2 y2 z2 x3 y3 z3`
            Regex regex = new Regex(@"^3\s+(-?\d+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)$");
            Match match = regex.Match(line);
            if (!match.Success)
            {
                return; // Invalid triangle line.
            }

            // Parse color and vertices here...
            float x1 = float.Parse(match.Groups[2].Value);
            float y1 = float.Parse(match.Groups[3].Value);
            float z1 = float.Parse(match.Groups[4].Value);
            float x2 = float.Parse(match.Groups[5].Value);
            float y2 = float.Parse(match.Groups[6].Value);
            float z2 = float.Parse(match.Groups[7].Value);
            float x3 = float.Parse(match.Groups[8].Value);
            float y3 = float.Parse(match.Groups[9].Value);
            float z3 = float.Parse(match.Groups[10].Value);

            // Transform vertices here if needed...
            Vector3 v1 = CurrentTransform.MultiplyPoint(new Vector3(x1, y1, z1));
            Vector3 v2 = CurrentTransform.MultiplyPoint(new Vector3(x2, y2, z2));
            Vector3 v3 = CurrentTransform.MultiplyPoint(new Vector3(x3, y3, z3));

            if (Invert ^ InvertNext)
            {
                Part.AddTriangle(v1, v2, v3);
            }
            else
            {
                Part.AddTriangle(v3, v2, v1);
            }
        }

        private void HandleQuad(string line)
        {
            // Match `4 <colour> x1 y1 z1 x2 y2 z2 x3 y3 z3 x4 y4 z4`
            Regex regex = new Regex(@"^4\s+(-?\d+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)$");
            Match match = regex.Match(line);
            if (!match.Success)
            {
                return; // Invalid quad line.
            }

            // Parse color and vertices here...
            float x1 = float.Parse(match.Groups[2].Value);
            float y1 = float.Parse(match.Groups[3].Value);
            float z1 = float.Parse(match.Groups[4].Value);
            float x2 = float.Parse(match.Groups[5].Value);
            float y2 = float.Parse(match.Groups[6].Value);
            float z2 = float.Parse(match.Groups[7].Value);
            float x3 = float.Parse(match.Groups[8].Value);
            float y3 = float.Parse(match.Groups[9].Value);
            float z3 = float.Parse(match.Groups[10].Value);
            float x4 = float.Parse(match.Groups[11].Value);
            float y4 = float.Parse(match.Groups[12].Value);
            float z4 = float.Parse(match.Groups[13].Value);

            // Transform vertices here if needed...
            Vector3 v1 = CurrentTransform.MultiplyPoint(new Vector3(x1, y1, z1));
            Vector3 v2 = CurrentTransform.MultiplyPoint(new Vector3(x2, y2, z2));
            Vector3 v3 = CurrentTransform.MultiplyPoint(new Vector3(x3, y3, z3));
            Vector3 v4 = CurrentTransform.MultiplyPoint(new Vector3(x4, y4, z4));

            if (Invert ^ InvertNext)
            {
                Part.AddQuad(v1, v2, v3, v4);
            }
            else
            {
                Part.AddQuad(v4, v3, v2, v1);
            }
        }
    }

}
