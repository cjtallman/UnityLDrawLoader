using System;
using System.IO;
using UnityEditor;

namespace LDraw.Editor
{
    public static class LDrawSettings
    {
        public static string PartAssetsFolder { get; set; } = "Assets/LDraw/Parts";
        public static string ModelAssetsFolder { get; set; } = "Assets/LDraw/Models";
        public static string MaterialAssetsFolder { get; set; } = "Assets/LDraw/Materials";

        public static void EnsureAssetsFolderExists( string folderPath )
        {
            if ( !AssetDatabase.IsValidFolder( folderPath ) )
            {
                string parentFolder = Path.GetDirectoryName( folderPath );
                string newFolderName = Path.GetFileName( folderPath );
                EnsureAssetsFolderExists( parentFolder );
                AssetDatabase.CreateFolder( parentFolder, newFolderName );
            }
        }
    }
}