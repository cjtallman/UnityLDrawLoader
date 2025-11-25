using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace LDraw.Editor
{
    public class LDrawLoaderWindow : EditorWindow
    {
        private const string CACHE_PREF_KEY = "LDrawLoader_LibraryPath";
        private const string CACHE_FILE_KEY = "LDrawLoader_CachedFiles";
        private const string SHOW_DUPLICATE_DIALOG_KEY = "LDrawLoader_ShowDuplicateDialog";
        
        private string libraryPath = "";
        private string selectedFilePath = "";
        private Vector2 scrollPosition;
        private string[] datFiles;
        private string[] filteredFiles;
        private string searchFilter = "";
        private bool isScanning = false;
        private bool showDuplicateDialog = true;
        
        private const int ITEMS_PER_PAGE = 50;
        private int currentPage = 0;

        [MenuItem("Tools/LDraw/Part Loader")]
        public static void ShowWindow()
        {
            GetWindow<LDrawLoaderWindow>("LDraw Part Loader");
        }

        private void OnEnable()
        {
            LoadCachedData();
        }

        private void OnGUI()
        {
            GUILayout.Label("LDraw Part Loader", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Library Path Selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Parts Library:", GUILayout.Width(100));
            EditorGUILayout.TextField(libraryPath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel("Select LDraw Parts Library", libraryPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    libraryPath = path;
                    selectedFilePath = "";
                    LoadDatFiles();
                }
            }
            GUI.enabled = !string.IsNullOrEmpty(libraryPath) && !isScanning;
            if (GUILayout.Button("Rescan", GUILayout.Width(80)))
            {
                selectedFilePath = "";
                LoadDatFiles();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            if (isScanning)
            {
                EditorGUILayout.HelpBox("Scanning library...", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Search Filter
            if (datFiles != null && datFiles.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
                string newFilter = EditorGUILayout.TextField(searchFilter);
                if (newFilter != searchFilter)
                {
                    searchFilter = newFilter;
                    selectedFilePath = "";
                    scrollPosition = Vector2.zero;
                    ApplyFilter();
                }
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    searchFilter = "";
                    selectedFilePath = "";
                    scrollPosition = Vector2.zero;
                    ApplyFilter();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            // File Selection List
            if (filteredFiles != null && filteredFiles.Length > 0)
            {
                int totalPages = Mathf.CeilToInt(filteredFiles.Length / (float)ITEMS_PER_PAGE);
                currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);
                
                int startIndex = currentPage * ITEMS_PER_PAGE;
                int endIndex = Mathf.Min(startIndex + ITEMS_PER_PAGE, filteredFiles.Length);
                
                EditorGUILayout.LabelField($"Showing {startIndex + 1}-{endIndex} of {filteredFiles.Length} files (Page {currentPage + 1}/{totalPages})", EditorStyles.boldLabel);
                
                // Pagination controls
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = currentPage > 0;
                if (GUILayout.Button("◄◄ First", GUILayout.Width(70)))
                {
                    currentPage = 0;
                    scrollPosition = Vector2.zero;
                    selectedFilePath = "";
                }
                if (GUILayout.Button("◄ Prev", GUILayout.Width(70)))
                {
                    currentPage--;
                    scrollPosition = Vector2.zero;
                    selectedFilePath = "";
                }
                GUI.enabled = true;
                
                GUILayout.FlexibleSpace();
                
                EditorGUILayout.LabelField($"Page:", GUILayout.Width(40));
                int newPage = EditorGUILayout.IntField(currentPage + 1, GUILayout.Width(50)) - 1;
                if (newPage != currentPage && newPage >= 0 && newPage < totalPages)
                {
                    currentPage = newPage;
                    scrollPosition = Vector2.zero;
                    selectedFilePath = "";
                }
                EditorGUILayout.LabelField($"of {totalPages}", GUILayout.Width(50));
                
                GUILayout.FlexibleSpace();
                
                GUI.enabled = currentPage < totalPages - 1;
                if (GUILayout.Button("Next ►", GUILayout.Width(70)))
                {
                    currentPage++;
                    scrollPosition = Vector2.zero;
                    selectedFilePath = "";
                }
                if (GUILayout.Button("Last ►►", GUILayout.Width(70)))
                {
                    currentPage = totalPages - 1;
                    scrollPosition = Vector2.zero;
                    selectedFilePath = "";
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
                
                for (int i = startIndex; i < endIndex; i++)
                {
                    string fileName = Path.GetFileName(filteredFiles[i]);
                    bool isSelected = (filteredFiles[i] == selectedFilePath);
                    bool newSelected = GUILayout.Toggle(isSelected, fileName, EditorStyles.radioButton);
                    
                    if (newSelected && !isSelected)
                    {
                        selectedFilePath = filteredFiles[i];
                    }
                }
                
                EditorGUILayout.EndScrollView();
            }
            else if (datFiles != null && datFiles.Length > 0)
            {
                EditorGUILayout.HelpBox($"No files match '{searchFilter}'", MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(libraryPath))
            {
                EditorGUILayout.HelpBox("No .dat files found in selected folder.", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Selected File Display
            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                EditorGUILayout.LabelField("Selected File:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(selectedFilePath, GUILayout.Height(20));
            }

            EditorGUILayout.Space();

            // Options
            EditorGUILayout.BeginHorizontal();
            bool newShowDialog = EditorGUILayout.Toggle("Show Duplicate Dialog", showDuplicateDialog);
            if (newShowDialog != showDuplicateDialog)
            {
                showDuplicateDialog = newShowDialog;
                EditorPrefs.SetBool(SHOW_DUPLICATE_DIALOG_KEY, showDuplicateDialog);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Load Button
            GUI.enabled = !string.IsNullOrEmpty(selectedFilePath);
            if (GUILayout.Button("Load Part", GUILayout.Height(30)))
            {
                LoadPart();
            }
            GUI.enabled = true;
        }

        private void LoadCachedData()
        {
            libraryPath = EditorPrefs.GetString(CACHE_PREF_KEY, "");
            showDuplicateDialog = EditorPrefs.GetBool(SHOW_DUPLICATE_DIALOG_KEY, true);
            
            if (!string.IsNullOrEmpty(libraryPath) && Directory.Exists(libraryPath))
            {
                string cachedFilesJson = EditorPrefs.GetString(CACHE_FILE_KEY, "");
                if (!string.IsNullOrEmpty(cachedFilesJson))
                {
                    try
                    {
                        FileCache cache = JsonUtility.FromJson<FileCache>(cachedFilesJson);
                        datFiles = cache.files;
                        ApplyFilter();
                        return;
                    }
                    catch
                    {
                        // Cache corrupted, will rescan
                    }
                }
            }
        }

        private void SaveCache()
        {
            EditorPrefs.SetString(CACHE_PREF_KEY, libraryPath);
            if (datFiles != null)
            {
                FileCache cache = new FileCache { files = datFiles };
                string json = JsonUtility.ToJson(cache);
                EditorPrefs.SetString(CACHE_FILE_KEY, json);
            }
        }

        private void LoadDatFiles()
        {
            if (Directory.Exists(libraryPath))
            {
                isScanning = true;
                Repaint();
                
                try
                {
                    string partsPath = Path.Join(libraryPath, "parts");
                    if (Directory.Exists(partsPath))
                    {
                        datFiles = Directory.GetFiles(partsPath, "*.dat", SearchOption.TopDirectoryOnly);
                    }
                    else
                    {
                        datFiles = Directory.GetFiles(libraryPath, "*.dat", SearchOption.TopDirectoryOnly);
                    }
                    
                    selectedFilePath = "";
                    searchFilter = "";
                    ApplyFilter();
                    SaveCache();
                }
                finally
                {
                    isScanning = false;
                }
            }
            else
            {
                datFiles = null;
                filteredFiles = null;
            }
        }

        private void ApplyFilter()
        {
            if (datFiles == null)
            {
                filteredFiles = null;
                return;
            }

            if (string.IsNullOrEmpty(searchFilter))
            {
                filteredFiles = datFiles;
            }
            else
            {
                filteredFiles = datFiles
                    .Where(path => Path.GetFileName(path).IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
            }
            
            currentPage = 0;
        }

        private void LoadPart()
        {
            try
            {
                // Check if asset already exists
                string fileName = Path.GetFileNameWithoutExtension(selectedFilePath);
                string partsFolder = "Assets/Parts";
                string assetPath = $"{partsFolder}/{fileName}_mesh.asset";
                
                // Ensure Parts folder exists
                if (!AssetDatabase.IsValidFolder(partsFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "Parts");
                }
                
                Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (existingMesh != null)
                {
                    if (showDuplicateDialog)
                    {
                        EditorUtility.DisplayDialog("Asset Already Exists", 
                            $"Mesh asset already exists at:\n{assetPath}\n\nDelete it first if you want to recreate it.", 
                            "OK");
                    }
                    
                    // Ping the existing asset
                    EditorGUIUtility.PingObject(existingMesh);
                    return;
                }
                
                EditorUtility.DisplayProgressBar("Loading LDraw Part", "Parsing file...", 0.5f);

                PartMesh partMesh = new PartMesh();
                partMesh.LoadFromFile(selectedFilePath, libraryPath);

                if (partMesh.Mesh == null)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Load Failed", "PartMesh.Mesh is null after loading.", "OK");
                    return;
                }

                // Create mesh asset
                AssetDatabase.CreateAsset(partMesh.Mesh, assetPath);
                AssetDatabase.SaveAssets();

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Success", $"Mesh created at:\n{assetPath}", "OK");

                // Ping the asset in the project window
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Mesh>(assetPath));
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Load Failed", $"Error loading part:\n{ex.Message}\n\nSee console for details.", "OK");
                Debug.LogError($"Failed to load LDraw part from {selectedFilePath}: {ex}");
            }
        }

        [System.Serializable]
        private class FileCache
        {
            public string[] files;
        }
    }
}
