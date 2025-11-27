// Author: cjtallman
// Copyright (c) 2025 Chris Tallman
// Last Modified: 2025/11/27
// License: MIT License
// Summary: Manage global LDraw settings

using System.IO;
using UnityEditor;

namespace LDraw.Editor
{
    public static class LDrawSettings
    {
        public static float ScaleFactor { get; set; } = 0.0004f; // Scale from LDU to Unity units (meters)
        public static string PartAssetsFolder { get; set; } = "Assets/LDraw/Parts";
        public static string ModelAssetsFolder { get; set; } = "Assets/LDraw/Models";
        public static string MaterialAssetsFolder { get; set; } = "Assets/LDraw/Materials";

        public static void EnsureAssetsFolderExists(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parentFolder = Path.GetDirectoryName(folderPath);
                string newFolderName = Path.GetFileName(folderPath);
                EnsureAssetsFolderExists(parentFolder);
                AssetDatabase.CreateFolder(parentFolder, newFolderName);
            }
        }
    }
}
