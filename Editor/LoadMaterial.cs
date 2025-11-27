using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LDraw.Editor
{
    /// <summary>
    /// Supported Unity render pipelines
    /// </summary>
    enum RenderPipeline
    {
        BuiltIn,
        URP,
        HDRP
    }



    /// <summary>
    /// Handles loading and creation of LDraw materials from color definitions
    /// </summary>
    public static class LoadMaterial
    {
        /// <summary>
        /// LDraw material finish types (internal)
        /// </summary>
        public enum MaterialFinishType
        {
            Solid,          // Regular opaque plastic
            Transparent,     // Transparent plastic
            Chrome,         // Chrome/metallic finish
            Pearlescent,    // Pearlescent finish
            Rubber,         // Rubber finish
            Metallic,       // Metallic finish
            Glitter,        // Glitter/sparkle finish
            Speckle,       // Speckled finish
            Matte,          // Matte finish
            Metalized        // Metalized finish
        }

        /// <summary>
        /// Represents an LDraw color definition
        /// </summary>
        [System.Serializable]
        public class LDrawColor
        {
            public int Code;
            public string Name;
            public Color Color;

            /// <summary>
            /// Material finish type (Solid, Chrome, Transparent, etc.)
            /// </summary>
            public MaterialFinishType Finish;

            public bool IsTransparent;
            public float Alpha = 1.0f;
        }

        /// <summary>
        /// Loads colors from LDConfig.ldr file including finish information
        /// </summary>
        /// <param name="libraryPath">Path to the LDraw library</param>
        /// <returns>List of LDraw colors with finish information</returns>
        public static List<LDrawColor> LoadLDConfigColors(string libraryPath)
        {
            List<LDrawColor> colors = new List<LDrawColor>();

            string ldconfigPath = Path.Combine(libraryPath, "LDConfig.ldr");
            if (!File.Exists(ldconfigPath))
            {
                Debug.LogWarning($"LDConfig.ldr not found at: {ldconfigPath}");
                return colors;
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
                        LDrawColor ldrawColor = ParseLDConfigColorLine(line);
                        if (ldrawColor != null)
                        {
                            colors.Add(ldrawColor);
                        }
                    }
                }

                colors.Sort((a, b) => a.Code.CompareTo(b.Code));
                Debug.Log($"Loaded {colors.Count} colors from LDConfig.ldr (including special finishes)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load LDConfig.ldr: {ex.Message}");
            }

            return colors;
        }

        /// <summary>
        /// Parses a single LDConfig color line into LDrawColor object
        /// </summary>
        /// <param name="line">LDConfig color line</param>
        /// <returns>Parsed LDrawColor or null if invalid</returns>
        private static LDrawColor ParseLDConfigColorLine(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string colorName = "";
            int colorCode = -1;
            string colorValue = "";
            MaterialFinishType finish = MaterialFinishType.Solid;
            bool hasAlpha = false;
            float alphaValue = 1.0f;

            for (int i = 0; i < parts.Length; i++)
            {
                switch (parts[i])
                {
                    case "!COLOUR":
                        if (i + 1 < parts.Length)
                            colorName = parts[i + 1];
                        break;
                    case "CODE":
                        if (i + 1 < parts.Length)
                            int.TryParse(parts[i + 1], out colorCode);
                        break;
                    case "VALUE":
                        if (i + 1 < parts.Length)
                            colorValue = parts[i + 1];
                        break;
                    case "ALPHA":
                        hasAlpha = true;
                        if (i + 1 < parts.Length)
                            float.TryParse(parts[i + 1], out alphaValue);
                        break;
                    case "CHROME":
                        finish = MaterialFinishType.Chrome;
                        break;
                    case "PEARLESCENT":
                        finish = MaterialFinishType.Pearlescent;
                        break;
                    case "RUBBER":
                        finish = MaterialFinishType.Rubber;
                        break;
                    case "METAL":
                        finish = MaterialFinishType.Metallic;
                        break;
                    case "MATERIAL":
                        // Check next token for specific material type
                        if (i + 1 < parts.Length)
                        {
                            switch (parts[i + 1])
                            {
                                case "GLITTER":
                                    finish = MaterialFinishType.Glitter;
                                    break;
                                case "SPECKLE":
                                    finish = MaterialFinishType.Speckle;
                                    break;
                                case "MATTE_METAL":
                                    finish = MaterialFinishType.Metalized;
                                    break;
                                case "MATTE":
                                    finish = MaterialFinishType.Matte;
                                    break;
                            }
                        }
                        break;
                }
            }

            // Validate required fields
            if (string.IsNullOrEmpty(colorName) || colorCode < 0 || string.IsNullOrEmpty(colorValue))
            {
                return null;
            }

            // Ignore certain colors
            if (colorName.StartsWith("Modulex_"))
            {
                return null;
            }

            Color color = ParseHexColor(colorValue);
            return new LDrawColor
            {
                Code = colorCode,
                Name = colorName.Replace('_', ' '),
                Color = color,
                Finish = finish,
                IsTransparent = hasAlpha,
                Alpha = alphaValue
            };
        }

        /// <summary>
        /// Creates a Unity material from an LDraw color
        /// </summary>
        /// <param name="ldrawColor">The LDraw color to create a material for</param>
        /// <param name="showDuplicateDialog">Whether to show dialog for existing materials</param>
        public static void CreateMaterialFromColor(LDrawColor ldrawColor, bool showDuplicateDialog = true)
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

                // Create new material using the appropriate render pipeline
                Material material = CreateNewMaterial(ldrawColor);

                EditorUtility.DisplayDialog("Success", $"Material created at:\n{materialPath}", "OK");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Material>(materialPath));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Load Failed", $"Error creating material:\n{ex.Message}\n\nSee console for details.", "OK");
                Debug.LogError($"Failed to create material for color {ldrawColor.Name}: {ex}");
            }
        }

        /// <summary>
        /// Gets or creates a material for a specific LDraw color code
        /// </summary>
        /// <param name="colorCode">LDraw color code</param>
        /// <param name="libraryPath">Path to LDraw library for loading colors</param>
        /// <returns>Material for the color code</returns>
        public static Material GetMaterialForColor(int colorCode, string libraryPath)
        {
            // Load colors if not already loaded
            if (s_loadedColors == null)
            {
                s_loadedColors = LoadLDConfigColors(libraryPath);
            }

            // Try to find existing material asset
            LDrawColor ldrawColor = s_loadedColors.FirstOrDefault(c => c.Code == colorCode);
            if (ldrawColor != null)
            {
                Material existingMaterial = FindExistingMaterial(ldrawColor);
                if (existingMaterial != null)
                {
                    return existingMaterial;
                }

                // Create new material if it doesn't exist
                return CreateNewMaterial(ldrawColor);
            }

            // Handle special color codes
            return GetSpecialColorMaterial(colorCode);
        }

        /// <summary>
        /// Finds an existing material asset for an LDraw color
        /// </summary>
        /// <param name="ldrawColor">LDraw color to find material for</param>
        /// <returns>Existing material or null</returns>
        private static Material FindExistingMaterial(LDrawColor ldrawColor)
        {
            string materialsFolder = LDrawSettings.MaterialAssetsFolder;
            string materialPath = $"{materialsFolder}/{ldrawColor.Name}_{ldrawColor.Code}.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        /// <summary>
        /// Creates a new material from an LDraw color using the appropriate render pipeline
        /// </summary>
        /// <param name="ldrawColor">LDraw color to create material for</param>
        /// <returns>New material</returns>
        private static Material CreateNewMaterial(LDrawColor ldrawColor)
        {
            RenderPipeline renderPipeline = GetCurrentRenderPipeline();

            Material material = renderPipeline switch
            {
                RenderPipeline.URP => CreateURPMaterial(ldrawColor),
                RenderPipeline.HDRP => CreateHDRPMaterial(ldrawColor),
                RenderPipeline.BuiltIn => CreateBuiltInMaterial(ldrawColor),
                _ => CreateBuiltInMaterial(ldrawColor)
            };

            // Save the material as an asset
            SaveMaterialAsAsset(material, ldrawColor);

            return material;
        }

        /// <summary>
        /// Creates a URP material from an LDraw color
        /// </summary>
        /// <param name="ldrawColor">LDraw color to create material for</param>
        /// <returns>URP material</returns>
        private static Material CreateURPMaterial(LDrawColor ldrawColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("URP Lit shader not found, falling back to Standard shader");
                return CreateBuiltInMaterial(ldrawColor);
            }

            Material material = new Material(shader);
            material.name = $"{ldrawColor.Name}_{ldrawColor.Code}_{ldrawColor.Finish}";

            // Apply finish-specific properties
            ApplyURPFinishProperties(material, ldrawColor);

            return material;
        }

        /// <summary>
        /// Applies finish-specific properties to URP materials
        /// </summary>
        /// <param name="material">Material to modify</param>
        /// <param name="ldrawColor">LDraw color with finish info</param>
        private static void ApplyURPFinishProperties(Material material, LDrawColor ldrawColor)
        {
            // Set base color
            material.SetColor("_BaseColor", ldrawColor.Color);

            // Apply finish-specific properties
            switch (ldrawColor.Finish)
            {
                case MaterialFinishType.Solid:
                    ApplySolidURPProperties(material);
                    break;
                case MaterialFinishType.Transparent:
                    ApplyTransparentURPProperties(material, ldrawColor);
                    break;
                case MaterialFinishType.Chrome:
                    ApplyChromeURPProperties(material);
                    break;
                case MaterialFinishType.Pearlescent:
                    ApplyPearlescentURPProperties(material);
                    break;
                case MaterialFinishType.Rubber:
                    ApplyRubberURPProperties(material);
                    break;
                case MaterialFinishType.Metallic:
                    ApplyMetallicURPProperties(material);
                    break;
                case MaterialFinishType.Glitter:
                    ApplyGlitterURPProperties(material);
                    break;
                case MaterialFinishType.Speckle:
                    ApplySpeckleURPProperties(material);
                    break;
                case MaterialFinishType.Matte:
                    ApplyMatteURPProperties(material);
                    break;
                case MaterialFinishType.Metalized:
                    ApplyMetalizedURPProperties(material);
                    break;
                default:
                    ApplySolidURPProperties(material); // Fallback to solid
                    break;
            }
        }

        /// <summary>
        /// Applies solid plastic properties to URP material
        /// </summary>
        private static void ApplySolidURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.65f); // Shiny plastic
            material.SetFloat("_Metallic", 0.05f);   // Not metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
            material.SetFloat("_EnvironmentReflections", 1.0f);
        }

        /// <summary>
        /// Applies transparent properties to URP material
        /// </summary>
        private static void ApplyTransparentURPProperties(Material material, LDrawColor ldrawColor)
        {
            material.SetFloat("_Surface", 1.0f); // Transparent
            material.SetFloat("_Blend", 0.0f); // Alpha blending
            material.SetFloat("_AlphaClipping", 0.0f);
            material.SetFloat("_Smoothness", 0.8f); // Very smooth for transparent
            material.SetFloat("_Metallic", 0.0f);
            material.SetFloat("_SpecularHighlights", 0.5f);
        }

        /// <summary>
        /// Applies chrome properties to URP material
        /// </summary>
        private static void ApplyChromeURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.95f); // Very shiny
            material.SetFloat("_Metallic", 0.9f);   // Highly metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
            material.SetFloat("_EnvironmentReflections", 1.0f);
        }

        /// <summary>
        /// Applies pearlescent properties to URP material
        /// </summary>
        private static void ApplyPearlescentURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.85f); // Very shiny
            material.SetFloat("_Metallic", 0.3f);   // Slightly metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
            material.SetFloat("_EnvironmentReflections", 1.0f);
        }

        /// <summary>
        /// Applies rubber properties to URP material
        /// </summary>
        private static void ApplyRubberURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.2f);  // Not shiny
            material.SetFloat("_Metallic", 0.0f);   // Not metallic
            material.SetFloat("_SpecularHighlights", 0.3f);
            material.SetFloat("_EnvironmentReflections", 0.2f);
        }

        /// <summary>
        /// Applies metallic properties to URP material
        /// </summary>
        private static void ApplyMetallicURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.8f);  // Shiny
            material.SetFloat("_Metallic", 0.7f);   // Metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
            material.SetFloat("_EnvironmentReflections", 1.0f);
        }

        /// <summary>
        /// Applies glitter properties to URP material
        /// </summary>
        private static void ApplyGlitterURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.9f);  // Very shiny
            material.SetFloat("_Metallic", 0.4f);   // Slightly metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
            material.SetFloat("_EnvironmentReflections", 1.0f);
        }

        /// <summary>
        /// Applies speckle properties to URP material
        /// </summary>
        private static void ApplySpeckleURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.6f);  // Moderately shiny
            material.SetFloat("_Metallic", 0.1f);   // Slightly metallic
            material.SetFloat("_SpecularHighlights", 0.8f);
            material.SetFloat("_EnvironmentReflections", 0.8f);
        }

        /// <summary>
        /// Applies matte properties to URP material
        /// </summary>
        private static void ApplyMatteURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.1f);  // Not shiny
            material.SetFloat("_Metallic", 0.0f);   // Not metallic
            material.SetFloat("_SpecularHighlights", 0.2f);
            material.SetFloat("_EnvironmentReflections", 0.1f);
        }

        /// <summary>
        /// Applies metalized properties to URP material
        /// </summary>
        private static void ApplyMetalizedURPProperties(Material material)
        {
            material.SetFloat("_Smoothness", 0.75f); // Shiny
            material.SetFloat("_Metallic", 0.6f);   // Metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
            material.SetFloat("_EnvironmentReflections", 1.0f);
        }

        /// <summary>
        /// Creates an HDRP material from an LDraw color
        /// </summary>
        /// <param name="ldrawColor">LDraw color to create material for</param>
        /// <returns>HDRP material</returns>
        private static Material CreateHDRPMaterial(LDrawColor ldrawColor)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                Debug.LogWarning("HDRP Lit shader not found, falling back to Standard shader");
                return CreateBuiltInMaterial(ldrawColor);
            }

            Material material = new Material(shader);
            material.name = $"{ldrawColor.Name}_{ldrawColor.Code}_{ldrawColor.Finish}_HDRP";

            // Apply finish-specific properties
            ApplyHDRPFinishProperties(material, ldrawColor);

            return material;
        }

        /// <summary>
        /// Applies finish-specific properties to HDRP materials
        /// </summary>
        /// <param name="material">Material to modify</param>
        /// <param name="ldrawColor">LDraw color with finish info</param>
        private static void ApplyHDRPFinishProperties(Material material, LDrawColor ldrawColor)
        {
            // Set base color
            material.SetColor("_BaseColor", ldrawColor.Color);
            material.SetColor("_BaseColorMap", ldrawColor.Color);

            // Apply finish-specific properties
            switch (ldrawColor.Finish)
            {
                case MaterialFinishType.Solid:
                    ApplySolidHDRPProperties(material);
                    break;
                case MaterialFinishType.Transparent:
                    ApplyTransparentHDRPProperties(material, ldrawColor);
                    break;
                case MaterialFinishType.Chrome:
                    ApplyChromeHDRPProperties(material);
                    break;
                case MaterialFinishType.Pearlescent:
                    ApplyPearlescentHDRPProperties(material);
                    break;
                case MaterialFinishType.Rubber:
                    ApplyRubberHDRPProperties(material);
                    break;
                case MaterialFinishType.Metallic:
                    ApplyMetallicHDRPProperties(material);
                    break;
                case MaterialFinishType.Glitter:
                    ApplyGlitterHDRPProperties(material);
                    break;
                case MaterialFinishType.Speckle:
                    ApplySpeckleHDRPProperties(material);
                    break;
                case MaterialFinishType.Matte:
                    ApplyMatteHDRPProperties(material);
                    break;
                case MaterialFinishType.Metalized:
                    ApplyMetalizedHDRPProperties(material);
                    break;
                default:
                    ApplySolidHDRPProperties(material); // Fallback to solid
                    break;
            }
        }

        /// <summary>
        /// Applies solid plastic properties to HDRP material
        /// </summary>
        private static void ApplySolidHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.05f);   // Not metallic
            material.SetFloat("_Smoothness", 0.65f); // Shiny plastic
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f); // Back face culling
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f); // LEqual
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1.0f);

            // Normal mapping setup (flat normals for LEGO parts)
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission (typically disabled for solid colors)
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies transparent properties to HDRP material
        /// </summary>
        private static void ApplyTransparentHDRPProperties(Material material, LDrawColor ldrawColor)
        {
            material.SetFloat("_SurfaceType", 1.0f); // Transparent
            material.SetFloat("_AlphaDstBlend", 10.0f); // OneMinusSrcAlpha
            material.SetFloat("_AlphaSrcBlend", 5.0f);  // SrcAlpha
            material.SetFloat("_ZWrite", 0.0f); // No depth write for transparent
            material.SetFloat("_CullMode", 2.0f); // Back face culling
            material.SetFloat("_ZTestDepthEqualForOpaque", 8.0f); // LEqual
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1.0f);
            material.SetFloat("_AlphaRemap", ldrawColor.Alpha);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies chrome properties to HDRP material
        /// </summary>
        private static void ApplyChromeHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.9f);   // Highly metallic
            material.SetFloat("_Smoothness", 0.95f); // Very shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1.0f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies pearlescent properties to HDRP material
        /// </summary>
        private static void ApplyPearlescentHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.3f);   // Slightly metallic
            material.SetFloat("_Smoothness", 0.85f); // Very shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1.0f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies rubber properties to HDRP material
        /// </summary>
        private static void ApplyRubberHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.0f);   // Not metallic
            material.SetFloat("_Smoothness", 0.2f); // Not shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 0.5f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies metallic properties to HDRP material
        /// </summary>
        private static void ApplyMetallicHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.7f);   // Metallic
            material.SetFloat("_Smoothness", 0.8f); // Shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1.0f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies glitter properties to HDRP material
        /// </summary>
        private static void ApplyGlitterHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.4f);   // Slightly metallic
            material.SetFloat("_Smoothness", 0.9f); // Very shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1.0f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies speckle properties to HDRP material
        /// </summary>
        private static void ApplySpeckleHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.1f);   // Slightly metallic
            material.SetFloat("_Smoothness", 0.6f); // Moderately shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 0.8f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies matte properties to HDRP material
        /// </summary>
        private static void ApplyMatteHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.0f);   // Not metallic
            material.SetFloat("_Smoothness", 0.1f); // Not shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 0.2f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Applies metalized properties to HDRP material
        /// </summary>
        private static void ApplyMetalizedHDRPProperties(Material material)
        {
            material.SetFloat("_Metallic", 0.6f);   // Metallic
            material.SetFloat("_Smoothness", 0.75f); // Shiny
            material.SetFloat("_SurfaceType", 0.0f); // Opaque
            material.SetFloat("_AlphaDstBlend", 0.0f);
            material.SetFloat("_AlphaSrcBlend", 1.0f);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetFloat("_CullMode", 2.0f);
            material.SetFloat("_ZTestDepthEqualForOpaque", 4.0f);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1.0f);

            // Normal mapping setup
            material.SetVector("_NormalMap", new Vector4(0.5f, 0.5f, 1.0f, 1.0f));
            material.SetFloat("_NormalScale", 1.0f);

            // Emission disabled
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_EmissionIntensity", 0.0f);
        }

        /// <summary>
        /// Creates a Built-in Render Pipeline material from an LDraw color
        /// </summary>
        /// <param name="ldrawColor">LDraw color to create material for</param>
        /// <returns>Built-in material</returns>
        private static Material CreateBuiltInMaterial(LDrawColor ldrawColor)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("Standard shader not found!");
                shader = Shader.Find("Diffuse");
            }
            if (shader == null)
            {
                Debug.LogError("No suitable shader found!");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            Material material = new Material(shader);
            material.name = $"{ldrawColor.Name}_{ldrawColor.Code}_{ldrawColor.Finish}_Standard";

            // Apply finish-specific properties
            ApplyBuiltInFinishProperties(material, ldrawColor);

            return material;
        }

        /// <summary>
        /// Applies finish-specific properties to Built-in materials
        /// </summary>
        /// <param name="material">Material to modify</param>
        /// <param name="ldrawColor">LDraw color with finish info</param>
        private static void ApplyBuiltInFinishProperties(Material material, LDrawColor ldrawColor)
        {
            // Set base color
            material.SetColor("_Color", ldrawColor.Color);

            // Apply finish-specific properties
            switch (ldrawColor.Finish)
            {
                case MaterialFinishType.Solid:
                    ApplySolidBuiltInProperties(material);
                    break;
                case MaterialFinishType.Transparent:
                    ApplyTransparentBuiltInProperties(material, ldrawColor);
                    break;
                case MaterialFinishType.Chrome:
                    ApplyChromeBuiltInProperties(material);
                    break;
                case MaterialFinishType.Pearlescent:
                    ApplyPearlescentBuiltInProperties(material);
                    break;
                case MaterialFinishType.Rubber:
                    ApplyRubberBuiltInProperties(material);
                    break;
                case MaterialFinishType.Metallic:
                    ApplyMetallicBuiltInProperties(material);
                    break;
                case MaterialFinishType.Glitter:
                    ApplyGlitterBuiltInProperties(material);
                    break;
                case MaterialFinishType.Speckle:
                    ApplySpeckleBuiltInProperties(material);
                    break;
                case MaterialFinishType.Matte:
                    ApplyMatteBuiltInProperties(material);
                    break;
                case MaterialFinishType.Metalized:
                    ApplyMetalizedBuiltInProperties(material);
                    break;
                default:
                    ApplySolidBuiltInProperties(material); // Fallback to solid
                    break;
            }
        }

        /// <summary>
        /// Applies solid plastic properties to Built-in material
        /// </summary>
        private static void ApplySolidBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.65f); // Shiny plastic
            material.SetFloat("_Metallic", 0.05f);   // Not metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
        }

        /// <summary>
        /// Applies transparent properties to Built-in material
        /// </summary>
        private static void ApplyTransparentBuiltInProperties(Material material, LDrawColor ldrawColor)
        {
            material.SetFloat("_Mode", 3.0f); // Transparent
            material.SetFloat("_AlphaClipping", 0.0f);
            material.SetFloat("_Glossiness", 0.8f); // Very smooth for transparent
            material.SetFloat("_Metallic", 0.0f);
            material.SetFloat("_SpecularHighlights", 0.5f);
        }

        /// <summary>
        /// Applies chrome properties to Built-in material
        /// </summary>
        private static void ApplyChromeBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.95f); // Very shiny
            material.SetFloat("_Metallic", 0.9f);   // Highly metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
        }

        /// <summary>
        /// Applies pearlescent properties to Built-in material
        /// </summary>
        private static void ApplyPearlescentBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.85f); // Very shiny
            material.SetFloat("_Metallic", 0.3f);   // Slightly metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
        }

        /// <summary>
        /// Applies rubber properties to Built-in material
        /// </summary>
        private static void ApplyRubberBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.2f);  // Not shiny
            material.SetFloat("_Metallic", 0.0f);   // Not metallic
            material.SetFloat("_SpecularHighlights", 0.3f);
        }

        /// <summary>
        /// Applies metallic properties to Built-in material
        /// </summary>
        private static void ApplyMetallicBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.8f);  // Shiny
            material.SetFloat("_Metallic", 0.7f);   // Metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
        }

        /// <summary>
        /// Applies glitter properties to Built-in material
        /// </summary>
        private static void ApplyGlitterBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.9f);  // Very shiny
            material.SetFloat("_Metallic", 0.4f);   // Slightly metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
        }

        /// <summary>
        /// Applies speckle properties to Built-in material
        /// </summary>
        private static void ApplySpeckleBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.6f);  // Moderately shiny
            material.SetFloat("_Metallic", 0.1f);   // Slightly metallic
            material.SetFloat("_SpecularHighlights", 0.8f);
        }

        /// <summary>
        /// Applies matte properties to Built-in material
        /// </summary>
        private static void ApplyMatteBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.1f);  // Not shiny
            material.SetFloat("_Metallic", 0.0f);   // Not metallic
            material.SetFloat("_SpecularHighlights", 0.2f);
        }

        /// <summary>
        /// Applies metalized properties to Built-in material
        /// </summary>
        private static void ApplyMetalizedBuiltInProperties(Material material)
        {
            material.SetFloat("_Glossiness", 0.75f); // Shiny
            material.SetFloat("_Metallic", 0.6f);   // Metallic
            material.SetFloat("_SpecularHighlights", 1.0f);
        }

        /// <summary>
        /// Gets material for special color codes (16, 24, etc.) or placeholder for unsupported colors
        /// </summary>
        /// <param name="colorCode">LDraw color code</param>
        /// <returns>Material for special color</returns>
        private static Material GetSpecialColorMaterial(int colorCode)
        {
            // Return cached special materials if available
            if (s_specialMaterials.TryGetValue(colorCode, out Material cachedMaterial))
            {
                return cachedMaterial;
            }

            // Create placeholder LDrawColor for special codes
            LDrawColor specialColor = colorCode switch
            {
                16 => new LDrawColor
                {
                    Code = 16,
                    Name = "Main_Color",
                    Color = Color.gray,
                    Finish = MaterialFinishType.Solid,
                    IsTransparent = false,
                    Alpha = 1.0f
                },
                24 => new LDrawColor
                {
                    Code = 24,
                    Name = "Edge_Color",
                    Color = Color.black,
                    Finish = MaterialFinishType.Solid,
                    IsTransparent = false,
                    Alpha = 1.0f
                },
                _ => new LDrawColor
                {
                    Code = colorCode,
                    Name = $"Unsupported_{colorCode}",
                    Color = GetPlaceholderColor(colorCode),
                    Finish = MaterialFinishType.Solid, // Default to solid for unsupported
                    IsTransparent = false,
                    Alpha = 1.0f
                }
            };

            RenderPipeline renderPipeline = GetCurrentRenderPipeline();
            Material material = renderPipeline switch
            {
                RenderPipeline.URP => CreateURPMaterial(specialColor),
                RenderPipeline.HDRP => CreateHDRPMaterial(specialColor),
                RenderPipeline.BuiltIn => CreateBuiltInMaterial(specialColor),
                _ => CreateBuiltInMaterial(specialColor)
            };

            // Cache special material and save as asset
            s_specialMaterials[colorCode] = material;
            SaveMaterialAsAsset(material, specialColor);
            return material;
        }

        /// <summary>
        /// Creates a special URP material for color codes (deprecated - use CreateURPMaterial with LDrawColor)
        /// </summary>
        /// <param name="colorCode">LDraw color code</param>
        /// <returns>Special URP material</returns>
        private static Material CreateSpecialURPMaterial(int colorCode)
        {
            // This method is kept for compatibility but should not be used
            // Use GetSpecialColorMaterial instead which creates proper LDrawColor objects
            LDrawColor placeholderColor = new LDrawColor
            {
                Code = colorCode,
                Name = $"Special_{colorCode}",
                Color = colorCode == 16 ? Color.gray : (colorCode == 24 ? Color.black : GetPlaceholderColor(colorCode)),
                Finish = MaterialFinishType.Solid,
                IsTransparent = false,
                Alpha = 1.0f
            };

            return CreateURPMaterial(placeholderColor);
        }

        /// <summary>
        /// Creates a special HDRP material for color codes (deprecated - use CreateHDRPMaterial with LDrawColor)
        /// </summary>
        /// <param name="colorCode">LDraw color code</param>
        /// <returns>Special HDRP material</returns>
        private static Material CreateSpecialHDRPMaterial(int colorCode)
        {
            // This method is kept for compatibility but should not be used
            // Use GetSpecialColorMaterial instead which creates proper LDrawColor objects
            LDrawColor placeholderColor = new LDrawColor
            {
                Code = colorCode,
                Name = $"Special_{colorCode}",
                Color = colorCode == 16 ? Color.gray : (colorCode == 24 ? Color.black : GetPlaceholderColor(colorCode)),
                Finish = MaterialFinishType.Solid,
                IsTransparent = false,
                Alpha = 1.0f
            };

            return CreateHDRPMaterial(placeholderColor);
        }

        /// <summary>
        /// Creates a special Built-in material for color codes (deprecated - use CreateBuiltInMaterial with LDrawColor)
        /// </summary>
        /// <param name="colorCode">LDraw color code</param>
        /// <returns>Special Built-in material</returns>
        private static Material CreateSpecialBuiltInMaterial(int colorCode)
        {
            // This method is kept for compatibility but should not be used
            // Use GetSpecialColorMaterial instead which creates proper LDrawColor objects
            LDrawColor placeholderColor = new LDrawColor
            {
                Code = colorCode,
                Name = $"Special_{colorCode}",
                Color = colorCode == 16 ? Color.gray : (colorCode == 24 ? Color.black : GetPlaceholderColor(colorCode)),
                Finish = MaterialFinishType.Solid,
                IsTransparent = false,
                Alpha = 1.0f
            };

            return CreateBuiltInMaterial(placeholderColor);
        }

        /// <summary>
        /// Gets a placeholder color for unsupported color codes
        /// </summary>
        /// <param name="colorCode">Unsupported color code</param>
        /// <returns>Placeholder color</returns>
        private static Color GetPlaceholderColor(int colorCode)
        {
            // Generate a deterministic but varied color based on color code
            float hue = (colorCode * 137.5f) % 360f; // Golden angle for good distribution
            float saturation = 0.3f; // Low saturation for placeholder look
            float value = 0.6f;     // Medium brightness

            return Color.HSVToRGB(hue / 360f, saturation, value);
        }

        /// <summary>
        /// Detects the current Unity render pipeline
        /// </summary>
        /// <returns>Current render pipeline</returns>
        private static RenderPipeline GetCurrentRenderPipeline()
        {
            // Check for HDRP first
            if (IsRenderPipelineInstalled("HDRP"))
            {
                return RenderPipeline.HDRP;
            }

            // Check for URP
            if (IsRenderPipelineInstalled("URP"))
            {
                return RenderPipeline.URP;
            }

            // Default to Built-in
            return RenderPipeline.BuiltIn;
        }

        /// <summary>
        /// Checks if a specific render pipeline is installed
        /// </summary>
        /// <param name="pipelineName">Name of the pipeline (URP, HDRP)</param>
        /// <returns>True if pipeline is installed</returns>
        private static bool IsRenderPipelineInstalled(string pipelineName)
        {
            try
            {
                // Check if pipeline package is installed
                string packagePath = $"Packages/{pipelineName.ToLower()}";
                if (System.IO.Directory.Exists(packagePath))
                {
                    return true;
                }

                // Check for embedded pipeline packages
                packagePath = $"Packages/com.unity.{pipelineName.ToLower()}";
                if (System.IO.Directory.Exists(packagePath))
                {
                    return true;
                }

                // Check for legacy package locations
                packagePath = $"Packages/com.unity.render-pipelines.{pipelineName.ToLower()}";
                if (System.IO.Directory.Exists(packagePath))
                {
                    return true;
                }

                // Fallback: Check if we can find a shader from the pipeline
                string shaderName = pipelineName switch
                {
                    "URP" => "Universal Render Pipeline/Lit",
                    "HDRP" => "HDRP/Lit",
                    _ => null
                };

                return !string.IsNullOrEmpty(shaderName) && Shader.Find(shaderName) != null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error detecting render pipeline {pipelineName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves a material as an asset in the correct folder
        /// </summary>
        /// <param name="material">Material to save</param>
        /// <param name="ldrawColor">LDraw color for naming</param>
        private static void SaveMaterialAsAsset(Material material, LDrawColor ldrawColor)
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
                    // Use existing material instead of creating duplicate
                    return;
                }

                // Create the asset
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save material asset for {ldrawColor.Name}: {ex}");
            }
        }

        /// <summary>
        /// Clears cached materials and colors (useful for testing or library changes)
        /// </summary>
        public static void ClearCache()
        {
            s_loadedColors = null;
            s_specialMaterials.Clear();
        }

        /// <summary>
        /// Parses a hex color string to Unity Color
        /// </summary>
        /// <param name="hex">Hex color string (with or without # or 0x prefix)</param>
        /// <returns>Unity Color</returns>
        private static Color ParseHexColor(string hex)
        {
            // Remove # if present
            hex = hex.TrimStart('#');

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex.Substring(2);
            }

            if (hex.Length == 6)
            {
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }

            return Color.magenta; // Fallback for invalid colors
        }

        // Static cache for loaded colors and special materials
        private static List<LDrawColor> s_loadedColors;
        private static readonly Dictionary<int, Material> s_specialMaterials = new Dictionary<int, Material>();
    }
}
