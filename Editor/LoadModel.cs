// Author: cjtallman
// Copyright (c) 2025 Chris Tallman
// Last Modified: 2025/11/27
// License: MIT License
// Summary: Load LDraw .ldr files

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LDraw.Editor
{
    /// <summary>
    /// Represents a single LDraw part within a model file.
    /// </summary>
    public class LDrawPart
    {
        public string FileName { get; set; }
        public int Color { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public GameObject GameObject { get; set; }
    }

    public class LdrFile
    {
        private readonly string FilePath;
        private readonly string LibraryPath;
        private readonly List<LDrawPart> Parts = new List<LDrawPart>();

        public string ModelName => Path.GetFileNameWithoutExtension(FilePath);
        public string ModelDescription { get; private set; } = "";
        public string ModelAuthor { get; private set; } = "";
        public IReadOnlyList<LDrawPart> ModelParts => Parts.AsReadOnly();

        public LdrFile(string filePath, string libraryPath)
        {
            if (!filePath.EndsWith(".ldr", StringComparison.OrdinalIgnoreCase) &&
                !filePath.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only .ldr and .mpd files are supported.", nameof(filePath));
            }

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            if (string.IsNullOrEmpty(libraryPath))
            {
                throw new ArgumentException("Library path cannot be null or empty.", nameof(libraryPath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The specified .ldr file was not found.", filePath);
            }

            if (!Directory.Exists(libraryPath))
            {
                throw new DirectoryNotFoundException("The specified LDraw library path was not found: " + libraryPath);
            }

            FilePath = filePath.Replace('/', '\\');
            LibraryPath = libraryPath.Replace('/', '\\');
        }

        /// <summary>
        /// Loads the LDraw model file and creates a GameObject hierarchy in the scene.
        /// </summary>
        /// <returns>The root GameObject of the loaded model.</returns>
        public GameObject LoadModelFromFile()
        {
            // Clear material cache to ensure fresh color loading
            LoadMaterial.ClearCache();

            ParseFile();

            string modelName = Path.GetFileNameWithoutExtension(FilePath);
            GameObject rootObject = new GameObject(modelName);
            rootObject.transform.position = Vector3.zero;

            foreach (LDrawPart part in Parts)
            {
                CreatePartGameObject(part, rootObject.transform);
            }

            return rootObject;
        }

        /// <summary>
        /// Creates a prefab from an existing model GameObject.
        /// </summary>
        /// <param name="modelRoot">The root GameObject of the model.</param>
        /// <param name="prefabPath">The path where the prefab should be saved.</param>
        /// <returns>The created prefab GameObject.</returns>
        public static GameObject CreatePrefabFromModel(GameObject modelRoot, string prefabPath)
        {
            if (modelRoot == null)
            {
                throw new ArgumentNullException(nameof(modelRoot));
            }

            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new ArgumentException("Prefab path cannot be null or empty.", nameof(prefabPath));
            }

            LDrawSettings.EnsureAssetsFolderExists(Path.GetDirectoryName(prefabPath));

            string localPath = prefabPath.Replace(Application.dataPath, "Assets");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(modelRoot, localPath);

            return prefab;
        }

        public void ParseFile()
        {
            Parts.Clear();

            using (var reader = new StreamReader(FilePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    ParseLine(line);
                }
            }
        }

        private void ParseLine(string line)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
            {
                return;
            }

            switch (trimmedLine[0])
            {
                case '0':
                    HandleComment(trimmedLine);
                    break;
                case '1':
                    HandleSubFile(trimmedLine);
                    break;
                default:
                    break;
            }
        }

        private void HandleComment(string line)
        {
            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
                return;

            // Sanity check: We shouldn't get here without a '0' at the start.
            if (tokens[0] != "0")
                return;

            switch (tokens[1].ToUpperInvariant())
            {
                case "!MODEL":
                    if (tokens.Length > 2 && string.IsNullOrEmpty(ModelDescription))
                        ModelDescription = string.Join(" ", tokens.Skip(2));
                    break;
                case "!AUTHOR":
                    if (tokens.Length > 2 && string.IsNullOrEmpty(ModelAuthor))
                        ModelAuthor = string.Join(" ", tokens.Skip(2));
                    break;
                case "AUTHOR:":
                    if (tokens.Length > 2 && string.IsNullOrEmpty(ModelAuthor))
                        ModelAuthor = string.Join(" ", tokens.Skip(2));
                    break;
                default:
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
                return;
            }

            string fileName = match.Groups[15].Value.Trim();
            string filePath = FindPartFile(fileName);
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning($"Part file not found: {fileName} referenced in {FilePath}");
                return;
            }

            // Parse color and position
            int color = int.Parse(match.Groups[2].Value);
            float x = float.Parse(match.Groups[3].Value);
            float y = float.Parse(match.Groups[4].Value);
            float z = float.Parse(match.Groups[5].Value);

            // Parse transformation matrix
            float a = float.Parse(match.Groups[6].Value);
            float b = float.Parse(match.Groups[7].Value);
            float c = float.Parse(match.Groups[8].Value);
            float d = float.Parse(match.Groups[9].Value);
            float e = float.Parse(match.Groups[10].Value);
            float f = float.Parse(match.Groups[11].Value);
            float g = float.Parse(match.Groups[12].Value);
            float h = float.Parse(match.Groups[13].Value);
            float i = float.Parse(match.Groups[14].Value);

            Matrix4x4 transform = new Matrix4x4();
            transform.SetRow(0, new Vector4(a, b, c, x));
            transform.SetRow(1, new Vector4(d, e, f, y));
            transform.SetRow(2, new Vector4(g, h, i, z));
            transform.SetRow(3, new Vector4(0, 0, 0, 1));

            Quaternion rotation = transform.rotation;
            rotation.w = -rotation.w;
            rotation.y = -rotation.y;

            LDrawPart part = new LDrawPart
            {
                FileName = fileName,
                Color = color,
                Position = new Vector3(x, -y, z) * LDrawSettings.ScaleFactor,
                Rotation = rotation,
            };

            Parts.Add(part);
        }

        private string FindPartFile(string fileName)
        {
            // file is relative to library path
            string filePath = Path.Combine(LibraryPath, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            // try parts subdirectory
            filePath = Path.Combine(LibraryPath, "parts", fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            // try p subdirectory
            filePath = Path.Combine(LibraryPath, "p", fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            return null;
        }

        private void CreatePartGameObject(LDrawPart part, Transform parent)
        {
            string partName = Path.GetFileNameWithoutExtension(part.FileName);
            GameObject partObject = new GameObject(partName);
            partObject.transform.parent = parent;

            string partFilePath = FindPartFile(part.FileName);
            if (!string.IsNullOrEmpty(partFilePath))
            {
                Mesh mesh = DatFile.LoadMeshFromFile(partFilePath, LibraryPath);
                if (mesh != null)
                {
                    MeshFilter meshFilter = partObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = mesh;

                    MeshRenderer renderer = partObject.AddComponent<MeshRenderer>();
                    AssignMaterial(renderer, part.Color);
                }
            }

            partObject.transform.position = part.Position;
            partObject.transform.rotation = part.Rotation;

            part.GameObject = partObject;
        }

        private void AssignMaterial(MeshRenderer renderer, int colorCode)
        {
            Material material = LoadMaterial.GetMaterialForColor(colorCode, LibraryPath);
            renderer.sharedMaterial = material;
        }


    }
}
