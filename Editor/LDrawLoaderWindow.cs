using UnityEngine;
using UnityEditor;
using System;
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
        private const string SMOOTHING_ANGLE_KEY = "LDrawLoader_SmoothingAngle";
        
        private string libraryPath = "";
        private string selectedFilePath = "";
        private string selectedModelFilePath = "";
        private Vector2 scrollPosition;
        private Vector2 colorScrollPosition;
        private string[] datFiles;
        private string[] filteredFiles;
        private string searchFilter = "";
        private bool isScanning = false;
private bool showDuplicateDialog = true;
        private float smoothingAngleThreshold = 30f;
        private List<LDrawColor> ldrawColors = new List<LDrawColor>();
        private int selectedColorIndex = -1;
        private Color selectedRowColor = new Color(0.13f, 0.13f, 0.13f, 1f);

        private const int ITEMS_PER_PAGE = 50;
        private int currentPage = 0;

        // Tab selection state
        private int selectedTab = 0;
        private readonly string[] tabNames = { "📁 Model Loader", "🧩 Part Loader", "🎨 Material Loader" };

        // Model loader state
        private bool createPrefab = false;
        private string prefabName = "";

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
                    LoadDatFiles();
                }
            }
            GUI.enabled = !string.IsNullOrEmpty(libraryPath) && !isScanning;
            if (GUILayout.Button("Rescan", GUILayout.Width(80)))
            {
                selectedFilePath = "";
                selectedModelFilePath = "";
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
                        Debug.Log($"Model file selected: {modelPath}");
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // Model Information Section
                EditorGUILayout.LabelField("Model Information", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Model file information will appear here once a file is selected.", MessageType.Info);

EditorGUILayout.Space();

                // Prefab Creation Options
                EditorGUILayout.LabelField("Prefab Options", EditorStyles.boldLabel);
                createPrefab = EditorGUILayout.Toggle("Create Prefab", createPrefab);
                if (createPrefab)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Prefab Name:", GUILayout.Width(80));
                    prefabName = EditorGUILayout.TextField(prefabName);
                    EditorGUILayout.EndHorizontal();
                    
                    if (string.IsNullOrEmpty(prefabName))
                    {
                        EditorGUILayout.HelpBox("Enter a prefab name to enable prefab creation.", MessageType.Warning);
                    }
                }

                EditorGUILayout.Space();

                // Model Parts Section
                EditorGUILayout.LabelField("Model Parts", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Parts used in the model will be listed here.", MessageType.Info);

                EditorGUILayout.Space();

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
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

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

            EditorGUILayout.Space();

            // Selected File Display
            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                EditorGUILayout.LabelField("Selected File:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(NormalizePathForDisplay(selectedFilePath), GUILayout.Height(20));
            }

            EditorGUILayout.Space();

            // Options Section
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            bool newShowDialog = EditorGUILayout.Toggle("Show Duplicate Dialog", showDuplicateDialog);
            if (newShowDialog != showDuplicateDialog)
            {
                showDuplicateDialog = newShowDialog;
                EditorPrefs.SetBool(SHOW_DUPLICATE_DIALOG_KEY, showDuplicateDialog);
            }
            EditorGUILayout.EndHorizontal();

            // Smoothing settings
            EditorGUILayout.BeginHorizontal();
            float newSmoothingAngle = EditorGUILayout.Slider("Smoothing Angle", smoothingAngleThreshold, 0f, 180f);
            if (Mathf.Abs(newSmoothingAngle - smoothingAngleThreshold) > 0.01f)
            {
                smoothingAngleThreshold = newSmoothingAngle;
                EditorPrefs.SetFloat(SMOOTHING_ANGLE_KEY, smoothingAngleThreshold);
            }
            EditorGUILayout.EndHorizontal();

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
                
                colorScrollPosition = EditorGUILayout.BeginScrollView(colorScrollPosition, GUILayout.Height(300));


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

            EditorGUILayout.Space();

            // Load Material Button (bottom)
            GUI.enabled = selectedColorIndex >= 0 && selectedColorIndex < ldrawColors.Count;
            if (GUILayout.Button("Load Material", GUILayout.Height(30)))
            {
                LoadColorMaterial(ldrawColors[selectedColorIndex]);
            }
            GUI.enabled = true;
}

        private void LoadModel()
        {
            try
            {
                LdrFile ldrFile = new LdrFile(selectedModelFilePath, libraryPath);
                GameObject modelRoot = ldrFile.LoadModelFromFile();

                if (createPrefab && !string.IsNullOrEmpty(prefabName))
                {
                    string prefabPath = $"{LDrawSettings.ModelAssetsFolder}/{prefabName}.prefab";
                    GameObject prefab = LdrFile.CreatePrefabFromModel(modelRoot, prefabPath);
                    Debug.Log($"Model loaded and prefab created: {prefabPath}");
                    
                    // Select the prefab in Project window
                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);
                }
                else
                {
                    Debug.Log($"Model loaded: {modelRoot.name}");
                }

                // Select the created model in hierarchy
                Selection.activeGameObject = modelRoot;
                EditorGUIUtility.PingObject(modelRoot);
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
            showDuplicateDialog = EditorPrefs.GetBool(SHOW_DUPLICATE_DIALOG_KEY, true);
            smoothingAngleThreshold = EditorPrefs.GetFloat(SMOOTHING_ANGLE_KEY, 30f);
            
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
                    LoadLDConfig();
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

        private void LoadLDConfig()
        {
            ldrawColors.Clear();
            
            string ldconfigPath = Path.Combine(libraryPath, "LDConfig.ldr");
            if (!File.Exists(ldconfigPath))
            {
                Debug.LogWarning($"LDConfig.ldr not found at: {ldconfigPath}");
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(ldconfigPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (!line.StartsWith("0 !COLOUR"))
                            continue;

                        // Parse color definition
                        // Format: 0 !COLOUR <name> CODE <code> VALUE <hex> EDGE <hex> [ALPHA <value>] [LUMINANCE <value>] [CHROME/PEARLESCENT/RUBBER/etc]
                        var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        
                        string colorName = "";
                        int colorCode = -1;
                        string colorValue = "";
                        bool hasAlpha = false;
                        bool hasSpecialFinish = false;

                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (parts[i] == "!COLOUR" && i + 1 < parts.Length)
                            {
                                colorName = parts[i + 1];
                            }
                            else if (parts[i] == "CODE" && i + 1 < parts.Length)
                            {
                                int.TryParse(parts[i + 1], out colorCode);
                            }
                            else if (parts[i] == "VALUE" && i + 1 < parts.Length)
                            {
                                colorValue = parts[i + 1];
                            }
                            else if (parts[i] == "ALPHA")
                            {
                                hasAlpha = true;
                            }
                            else if (parts[i] == "CHROME" || parts[i] == "PEARLESCENT" || 
                                     parts[i] == "RUBBER" || parts[i] == "METAL" || 
                                     parts[i] == "MATERIAL")
                            {
                                hasSpecialFinish = true;
                            }
                        }

                        // Only add solid, opaque colors
                        if (!string.IsNullOrEmpty(colorName) && colorCode >= 0 && 
                            !string.IsNullOrEmpty(colorValue) && !hasAlpha && !hasSpecialFinish)
                        {
                            Color color = ParseHexColor(colorValue);
                            ldrawColors.Add(new LDrawColor
                            {
                                Code = colorCode,
                                Name = colorName,
                                Color = color
                            });
                        }
                    }
                }

                ldrawColors.Sort((a, b) => a.Code.CompareTo(b.Code));
                Debug.Log($"Loaded {ldrawColors.Count} solid colors from LDConfig.ldr");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load LDConfig.ldr: {ex.Message}");
            }
        }

        private Color ParseHexColor(string hex)
        {
            // Remove # if present
            hex = hex.TrimStart('#');
            
            if (hex.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
            {
                hex = hex.Substring(2);
            }

            if (hex.Length == 6)
            {
                int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }

            return Color.magenta; // Fallback for invalid colors
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
                // Check if asset already exists
                string fileName = Path.GetFileNameWithoutExtension(selectedFilePath);
                string partsFolder = LDrawSettings.PartAssetsFolder;
                string assetPath = $"{partsFolder}/{fileName}_mesh.asset";
                
                // Ensure Parts folder exists
                LDrawSettings.EnsureAssetsFolderExists(partsFolder);
                
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
                
                Mesh partMesh = DatFile.LoadMeshFromFile(selectedFilePath, libraryPath);

                if (partMesh == null)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Load Failed", "PartMesh.Mesh is null after loading.", "OK");
                    return;
                }

                // Create mesh asset
                AssetDatabase.CreateAsset(partMesh, assetPath);
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

        private void LoadColorMaterial(LDrawColor ldrawColor)
        {
            try
            {
                // Create materials folder if it doesn't exist
                string materialsFolder = LDrawSettings.MaterialAssetsFolder;
                LDrawSettings.EnsureAssetsFolderExists(materialsFolder);

                string materialPath = $"{materialsFolder}/{ldrawColor.Name}_{ldrawColor.Code}.mat";

                // Check if material already exists
                Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (existingMaterial != null)
                {
                    if (showDuplicateDialog)
                    {
                        EditorUtility.DisplayDialog("Material Already Exists",
                            $"Material already exists at:\n{materialPath}\n\nDelete it first if you want to recreate it.",
                            "OK");
                    }

                    EditorGUIUtility.PingObject(existingMaterial);
                    return;
                }

                // Create new material using URP Lit shader
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogWarning("URP Lit shader not found, trying Standard shader");
                    shader = Shader.Find("Standard");
                }
                if (shader == null)
                {
                    Debug.LogError("No suitable shader found!");
                    EditorUtility.DisplayDialog("Shader Not Found", "Could not find URP Lit or Standard shader.", "OK");
                    return;
                }
                
                Material material = new Material(shader);
                material.name = $"{ldrawColor.Name}_{ldrawColor.Code}";
                
                // Set URP Lit shader properties for LEGO-like plastic appearance
                material.SetColor("_BaseColor", ldrawColor.Color);
                material.SetFloat("_Smoothness", 0.65f); // Shiny plastic
                material.SetFloat("_Metallic", 0.05f);   // Not metallic
                material.SetFloat("_SpecularHighlights", 1.0f);
                material.SetFloat("_EnvironmentReflections", 1.0f);
                
                Debug.Log($"Created material with shader: {shader.name}, color: {ldrawColor.Color}");

                // Create the asset
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Success", $"Material created at:\n{materialPath}", "OK");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Material>(materialPath));
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Load Failed", $"Error creating material:\n{ex.Message}\n\nSee console for details.", "OK");
                Debug.LogError($"Failed to create material for color {ldrawColor.Name}: {ex}");
            }
        }

        [System.Serializable]
        private class FileCache
        {
            public string[] files;
        }

        private class LDrawColor
        {
            public int Code;
            public string Name;
            public Color Color;
        }
    }
}
