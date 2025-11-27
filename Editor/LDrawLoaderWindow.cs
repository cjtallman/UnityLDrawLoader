// Author: cjtallman
// Copyright (c) 2025 Chris Tallman
// Last Modified: 2025/11/26
// License: MIT License
// Summary: GUI for loading LDraw parts

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LDraw.Editor
{
    public class LDrawLoaderWindow : EditorWindow
    {
        private const string CACHE_PREF_KEY = "LDrawLoader_LibraryPath";
        private const string CACHE_FILE_KEY = "LDrawLoader_CachedFiles";

        private string libraryPath = "";
        private string selectedFilePath = "";
        private string selectedModelFilePath = "";
        private LdrFile currentModelFile = null;
        private Vector2 scrollPosition;
        private Vector2 modelPartsScrollPosition;
        private int partsListTab = 0;
        private readonly string[] partsListTabNames = { "By Count", "By Color" };
        private Vector2 colorScrollPosition;
        private string[] datFiles;
        private string[] filteredFiles;
        private string searchFilter = "";
        private bool isScanning = false;
        private List<LoadMaterial.LDrawColor> ldrawColors = new List<LoadMaterial.LDrawColor>();
        private int selectedColorIndex = -1;
        private Color selectedRowColor = new Color(0.13f, 0.13f, 0.13f, 1f);

        private const int ITEMS_PER_PAGE = 50;
        private int currentPage = 0;

        // Tab selection state
        private int selectedTab = 0;
        private readonly string[] tabNames = { "📁 Model Loader", "🧩 Part Loader", "🎨 Material Loader" };



        /// <summary>
        /// Normalizes directory separators for consistent display in the GUI
        /// </summary>
        /// <param name="path">The file path to normalize</param>
        /// <returns>Path with normalized separators</returns>
        private string NormalizePathForDisplay(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Normalize all separators to forward slashes for consistency
            return path.Replace('\\', '/');
        }

        [MenuItem("Tools/LDraw Part Loader")]
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

            // Library Path Selection (always visible)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Parts Library:", GUILayout.Width(100));
            EditorGUILayout.TextField(NormalizePathForDisplay(libraryPath));
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel("Select LDraw Parts Library", libraryPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    libraryPath = path;
                    selectedFilePath = "";
                    selectedModelFilePath = "";
                    currentModelFile = null;
                    LoadDatFiles();
                }
            }
            GUI.enabled = !string.IsNullOrEmpty(libraryPath) && !isScanning;
            if (GUILayout.Button("Rescan", GUILayout.Width(80)))
            {
                selectedFilePath = "";
                selectedModelFilePath = "";
                currentModelFile = null;
                LoadDatFiles();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (isScanning)
            {
                EditorGUILayout.HelpBox("Scanning library...", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Tab Selection
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
            EditorGUILayout.Space();

            // Tab Content
            switch (selectedTab)
            {
                case 0: // Model Loader Tab
                    DrawModelLoaderTab();
                    break;
                case 1: // Part Loader Tab
                    DrawPartLoaderTab();
                    break;
                case 2: // Material Loader Tab
                    DrawMaterialLoaderTab();
                    break;
            }
        }

        private void DrawModelLoaderTab()
        {
            if (!string.IsNullOrEmpty(libraryPath))
            {
                EditorGUILayout.Space();

                // Model File Selection
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Model File:", GUILayout.Width(80));
                if (string.IsNullOrEmpty(selectedModelFilePath))
                {
                    EditorGUILayout.LabelField("No file selected", EditorStyles.helpBox);
                }
                else
                {
                    EditorGUILayout.LabelField(NormalizePathForDisplay(selectedModelFilePath), EditorStyles.helpBox);
                }
                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    string modelPath = EditorUtility.OpenFilePanel(
                        "Select LDraw Model File",
                        libraryPath,
                        "ldr,mpd"
                    );
                    if (!string.IsNullOrEmpty(modelPath))
                    {
                        selectedModelFilePath = modelPath;
                        ParseSelectedModel();
                        Debug.Log($"Model file selected: {modelPath}");
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // Model Information Section
                EditorGUILayout.LabelField("Model Information", EditorStyles.boldLabel);
                if (currentModelFile != null)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Name: {currentModelFile.ModelName}");
                    if (!string.IsNullOrEmpty(currentModelFile.ModelDescription))
                        EditorGUILayout.LabelField($"Description: {currentModelFile.ModelDescription}");
                    if (!string.IsNullOrEmpty(currentModelFile.ModelAuthor))
                        EditorGUILayout.LabelField($"Author: {currentModelFile.ModelAuthor}");
                    EditorGUILayout.LabelField($"Parts Count: {currentModelFile.ModelParts.Count}");
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.HelpBox("Model file information will appear here once a file is selected.\n\nNote: Loading a model will automatically create a prefab asset in Assets/LDraw/Models/ using the model filename.", MessageType.Info);
                }

                EditorGUILayout.Space();

                // Model Parts Section
                EditorGUILayout.LabelField("Model Parts", EditorStyles.boldLabel);
                if (currentModelFile != null && currentModelFile.ModelParts.Count > 0)
                {
                    // Parts list tabs
                    partsListTab = GUILayout.Toolbar(partsListTab, partsListTabNames);
                    EditorGUILayout.Space();

                    modelPartsScrollPosition = EditorGUILayout.BeginScrollView(modelPartsScrollPosition, GUILayout.ExpandHeight(true));

                    switch (partsListTab)
                    {
                        case 0: // By Count
                            DrawPartsByCount();
                            break;
                        case 1: // By Color
                            DrawPartsByColor();
                            break;
                    }

                    EditorGUILayout.EndScrollView();
                }
                else
                {
                    EditorGUILayout.HelpBox("Parts used in the model will be listed here.", MessageType.Info);
                }

                GUILayout.FlexibleSpace();

                // Load Model Button (bottom)
                GUI.enabled = !string.IsNullOrEmpty(selectedModelFilePath);
                if (GUILayout.Button("Load Model", GUILayout.Height(30)))
                {
                    LoadModel();
                }
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.HelpBox("Select a parts library path to enable model loading.", MessageType.Info);
            }
        }

        private void DrawPartLoaderTab()
        {
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
                    ApplyFilter();
                }
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    searchFilter = "";
                    selectedFilePath = "";
                    ApplyFilter();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
            }

            // File Selection List - ListView-style single selection with pagination
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

                // ListView-style selection with pagination
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

                for (int i = startIndex; i < endIndex; i++)
                {
                    string fileName = Path.GetFileName(filteredFiles[i]);
                    bool isSelected = (filteredFiles[i] == selectedFilePath);
                    Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(24));

                    // Draw selection background if selected
                    if (isSelected)
                    {
                        GUI.color = selectedRowColor;
                        GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }

                    // Detect click
                    if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                    {
                        selectedFilePath = filteredFiles[i];
                        Repaint();
                    }

                    EditorGUILayout.LabelField(fileName, GUILayout.ExpandWidth(true));

                    EditorGUILayout.EndHorizontal();
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

            GUILayout.FlexibleSpace();

            // Selected File Display
            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                EditorGUILayout.LabelField("Selected File:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(NormalizePathForDisplay(selectedFilePath), GUILayout.Height(20));
            }

            EditorGUILayout.Space();

            // Load Part Button (bottom)
            GUI.enabled = !string.IsNullOrEmpty(selectedFilePath);
            if (GUILayout.Button("Load Part", GUILayout.Height(30)))
            {
                LoadPart();
            }
            GUI.enabled = true;
        }

        private void DrawMaterialLoaderTab()
        {
            EditorGUILayout.Space();

            if (ldrawColors.Count > 0)
            {
                EditorGUILayout.LabelField($"Loaded {ldrawColors.Count} solid colors from LDConfig.ldr");

                // Multi-column list view header
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField("", GUILayout.Width(20)); // Color column header
                EditorGUILayout.LabelField("Code", GUILayout.Width(50));
                EditorGUILayout.LabelField("Name", GUILayout.Width(120));
                EditorGUILayout.LabelField("HEX Value", GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                colorScrollPosition = EditorGUILayout.BeginScrollView(colorScrollPosition, GUILayout.ExpandHeight(true));


                // Color list items
                for (int i = 0; i < ldrawColors.Count; i++)
                {
                    var color = ldrawColors[i];
                    bool isSelected = (i == selectedColorIndex);

                    // Get the rect for the current row position
                    Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(24));

                    // Draw selection background if selected
                    if (isSelected)
                    {
                        GUI.color = selectedRowColor;
                        GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }

                    // Detect click
                    if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                    {
                        selectedColorIndex = i;
                        Repaint();
                    }

                    // Draw content on top of the transparent button
                    // Color preview box (aligned with header)
                    Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
                    colorRect.y = rowRect.y + 2; // Center vertically in the 24px row
                    EditorGUI.DrawRect(colorRect, color.Color);
                    GUILayout.Space(4); // Add spacing after color box

                    // Code column (aligned with header)
                    EditorGUILayout.LabelField(color.Code.ToString(), GUILayout.Width(50));
                    GUILayout.Space(5);

                    // Name column (aligned with header)
                    EditorGUILayout.LabelField(color.Name, GUILayout.Width(120));
                    GUILayout.Space(5);

                    // HEX column (aligned with header)
                    EditorGUILayout.LabelField($"#{ColorUtility.ToHtmlStringRGB(color.Color)}", GUILayout.ExpandWidth(true));

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            else if (!string.IsNullOrEmpty(libraryPath))
            {
                EditorGUILayout.HelpBox("No LDConfig.ldr found or no solid colors loaded.", MessageType.Info);
            }

            GUILayout.FlexibleSpace();

            // Load Material Button (bottom)
            GUI.enabled = selectedColorIndex >= 0 && selectedColorIndex < ldrawColors.Count;
            if (GUILayout.Button("Load Material", GUILayout.Height(30)))
            {
                LoadMaterial.CreateMaterialFromColor(ldrawColors[selectedColorIndex], true);
            }
            GUI.enabled = true;
        }

        private void ParseSelectedModel()
        {
            try
            {
                if (string.IsNullOrEmpty(selectedModelFilePath) || string.IsNullOrEmpty(libraryPath))
                {
                    currentModelFile = null;
                    return;
                }

                currentModelFile = new LdrFile(selectedModelFilePath, libraryPath);
                // Parse the file to extract model information and parts
                currentModelFile.ParseFile();

                // Force UI repaint
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse model: {ex.Message}");
                currentModelFile = null;
            }
        }

        private void DrawPartsByCount()
        {
            // Group parts by filename and count occurrences
            var partGroups = currentModelFile.ModelParts
                .GroupBy(p => p.FileName)
                .OrderBy(g => g.Key)
                .ToList();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Part Number", GUILayout.Width(150));
            EditorGUILayout.LabelField("Count", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            foreach (var group in partGroups)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(group.Key), GUILayout.Width(150));
                EditorGUILayout.LabelField($"x{group.Count()}", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPartsByColor()
        {
            // Group parts by filename and color
            var partColorGroups = currentModelFile.ModelParts
                .GroupBy(p => new { p.FileName, p.Color })
                .OrderBy(g => g.Key.Color)
                .ThenBy(g => g.Key.FileName)
                .ToList();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Part Number", GUILayout.Width(150));
            EditorGUILayout.LabelField("Color", GUILayout.Width(120));
            EditorGUILayout.LabelField("Quantity", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            foreach (var group in partColorGroups)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(group.Key.FileName), GUILayout.Width(150));

                var colorInfo = ldrawColors.FirstOrDefault(c => c.Code == group.Key.Color);
                if (colorInfo != null)
                {
                    Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
                    EditorGUI.DrawRect(colorRect, colorInfo.Color);
                    GUILayout.Space(5);
                    EditorGUILayout.LabelField(colorInfo.Name, GUILayout.Width(120));
                }
                else
                {
                    EditorGUILayout.LabelField($"Color: {group.Key.Color}", GUILayout.Width(120));
                }

                EditorGUILayout.LabelField($"x{group.Count()}", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void LoadModel()
        {
            try
            {
                if (currentModelFile == null)
                {
                    currentModelFile = new LdrFile(selectedModelFilePath, libraryPath);
                }

                GameObject modelRoot = currentModelFile.LoadModelFromFile();

                // Automatically create prefab using model filename
                string modelName = Path.GetFileNameWithoutExtension(selectedModelFilePath);
                string prefabPath = $"{LDrawSettings.ModelAssetsFolder}/{modelName}.prefab";
                GameObject prefab = LdrFile.CreatePrefabFromModel(modelRoot, prefabPath);

                Debug.Log($"Model loaded and prefab created: {prefabPath}");

                // Select prefab in Project window
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);

                // Clean up the scene object since we have a prefab
                GameObject.DestroyImmediate(modelRoot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load model: {ex.Message}");
                EditorUtility.DisplayDialog("Load Error", $"Failed to load model: {ex.Message}", "OK");
            }
        }


        private void LoadCachedData()
        {
            libraryPath = EditorPrefs.GetString(CACHE_PREF_KEY, "");

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

                    // Sort lexicographically by filename
                    if (datFiles != null)
                    {
                        datFiles = datFiles
                            .OrderBy(path => Path.GetFileName(path), System.StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Length)
                            .ToArray();
                    }

                    selectedFilePath = "";
                    searchFilter = "";
                    ApplyFilter();
                    SaveCache();
                    LoadLDConfigColors();
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

        private void LoadLDConfigColors()
        {
            ldrawColors = LoadMaterial.LoadLDConfigColors(libraryPath);
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
                // When no search filter, use lexicographic sorting
                filteredFiles = datFiles
                    .OrderBy(path => Path.GetFileName(path), System.StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            else
            {
                // Filter and sort by relevance
                filteredFiles = datFiles
                    .Where(path => Path.GetFileName(path).IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(path => CalculateSearchRelevance(Path.GetFileName(path), searchFilter))
                    .ThenBy(path => Path.GetFileName(path), System.StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            currentPage = 0;
        }

        private float CalculateSearchRelevance(string filename, string searchText)
        {
            if (string.IsNullOrEmpty(searchText) || string.IsNullOrEmpty(filename))
                return 0f;

            filename = filename.ToLowerInvariant();
            searchText = searchText.ToLowerInvariant();

            // Exact match gets highest score
            if (filename.Equals(searchText))
                return 100f;

            // Exact match without extension
            string filenameWithoutExt = Path.GetFileNameWithoutExtension(filename);
            if (filenameWithoutExt.Equals(searchText))
                return 95f;

            // Starts with search text
            if (filename.StartsWith(searchText))
                return 90f;

            // Starts with search text (without extension)
            if (filenameWithoutExt.StartsWith(searchText))
                return 85f;

            // Contains exact search text as substring
            int exactMatchIndex = filename.IndexOf(searchText);
            if (exactMatchIndex >= 0)
            {
                // Score based on how early the match appears
                float positionScore = 80f - (exactMatchIndex * 2f);
                return Math.Max(0f, positionScore);
            }

            // Contains exact search text in filename (without extension)
            int exactMatchWithoutExtIndex = filenameWithoutExt.IndexOf(searchText);
            if (exactMatchWithoutExtIndex >= 0)
            {
                // Score based on how early the match appears
                float positionScore = 75f - (exactMatchWithoutExtIndex * 2f);
                return Math.Max(0f, positionScore);
            }

            // Check for word boundary matches
            string[] searchWords = searchText.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (searchWords.Length > 0)
            {
                float totalWordScore = 0f;
                int matchedWords = 0;

                string[] filenameWords = filename.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string searchWord in searchWords)
                {
                    foreach (string filenameWord in filenameWords)
                    {
                        if (filenameWord.Contains(searchWord))
                        {
                            matchedWords++;
                            if (filenameWord.Equals(searchWord))
                                totalWordScore += 20f; // Exact word match
                            else if (filenameWord.StartsWith(searchWord))
                                totalWordScore += 15f; // Word starts with search
                            else
                                totalWordScore += 10f; // Word contains search
                            break;
                        }
                    }
                }

                if (matchedWords > 0)
                {
                    // Bonus for matching more words
                    float wordMatchRatio = (float)matchedWords / searchWords.Length;
                    return totalWordScore * wordMatchRatio;
                }
            }

            // Character-level matching (as last resort)
            int matchedChars = 0;
            int searchIndex = 0;

            for (int i = 0; i < filename.Length && searchIndex < searchText.Length; i++)
            {
                if (filename[i] == searchText[searchIndex])
                {
                    matchedChars++;
                    searchIndex++;
                }
            }

            if (matchedChars > 0)
            {
                return (matchedChars * 2f); // Minimal score for partial character matches
            }

            return 0f; // No match
        }

        private void LoadPart()
        {
            try
            {
                Mesh partMesh = DatFile.LoadMeshFromFile(selectedFilePath, libraryPath);
                if (partMesh != null)
                {
                    // Ping the asset in the project window
                    EditorGUIUtility.PingObject(partMesh);
                }
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
