using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Akira.Tools.Core;

namespace Akira.Tools.Services
{
    /// <summary>
    /// Service layer for folder structure management
    /// Handles creation, validation, and preset management
    /// </summary>
    public class FolderService
    {
        private const string PRESETS_PATH = "Assets/Editor/FolderPresets";
        private List<FolderPreset> cachedPresets;

        #region Public API

        /// <summary>
        /// Create folder structure from a list of paths
        /// </summary>
        public void CreateFolderStructure(List<string> folders, Action<bool, string> callback)
        {
            if (!ErrorHandler.ValidateNotNull(folders, "Folder list"))
            {
                callback?.Invoke(false, "Folder list cannot be null");
                return;
            }

            if (folders.Count == 0)
            {
                ErrorHandler.LogWarning("Folder list is empty");
                callback?.Invoke(false, "No folders to create");
                return;
            }

            ErrorHandler.Try(
                action: () =>
                {
                    int createdCount = 0;
                    int skippedCount = 0;

                    foreach (var folderPath in folders)
                    {
                        if (string.IsNullOrEmpty(folderPath)) continue;

                        if (CreateSingleFolder(folderPath))
                        {
                            createdCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }

                    AssetDatabase.Refresh();
                    
                    string message = $"Created {createdCount} folders";
                    if (skippedCount > 0)
                    {
                        message += $", skipped {skippedCount} existing folders";
                    }

                    ErrorHandler.Log(message);
                    callback?.Invoke(true, message);
                },
                onError: (ex) =>
                {
                    ErrorHandler.LogErrorWithCode("FLD001", ex.Message);
                    callback?.Invoke(false, "Failed to create folder structure");
                },
                context: "Create folder structure"
            );
        }

        /// <summary>
        /// Create folder structure from a preset
        /// </summary>
        public void CreateFromPreset(FolderPreset preset, string rootPath, Action<bool, string> callback)
        {
            if (!ErrorHandler.ValidateNotNull(preset, "Folder preset"))
            {
                callback?.Invoke(false, "Preset cannot be null");
                return;
            }

            ErrorHandler.Log($"Creating folder structure from preset: {preset.Name}");

            // Build full paths
            var fullPaths = preset.Folders.Select(f => Path.Combine(rootPath, f)).ToList();
            
            CreateFolderStructure(fullPaths, callback);
        }

        /// <summary>
        /// Save a custom folder preset
        /// </summary>
        public void SavePreset(FolderPreset preset, Action<bool, string> callback)
        {
            if (!ErrorHandler.ValidateNotNull(preset, "Folder preset"))
            {
                callback?.Invoke(false, "Preset cannot be null");
                return;
            }

            ErrorHandler.Try(
                action: () =>
                {
                    // Ensure presets directory exists
                    if (!Directory.Exists(PRESETS_PATH))
                    {
                        Directory.CreateDirectory(PRESETS_PATH);
                    }

                    string filePath = Path.Combine(PRESETS_PATH, $"{preset.Name}.json");
                    string json = JsonUtility.ToJson(preset, true);
                    File.WriteAllText(filePath, json);

                    AssetDatabase.Refresh();
                    
                    ErrorHandler.Log($"Preset saved: {preset.Name}");
                    callback?.Invoke(true, $"Preset '{preset.Name}' saved successfully");
                    
                    // Clear cache to reload presets
                    cachedPresets = null;
                },
                onError: (ex) =>
                {
                    ErrorHandler.LogError($"Failed to save preset: {ex.Message}");
                    callback?.Invoke(false, "Failed to save preset");
                },
                context: $"Save preset: {preset.Name}"
            );
        }

        /// <summary>
        /// Load a preset by name
        /// </summary>
        public FolderPreset LoadPreset(string presetName)
        {
            if (!ErrorHandler.ValidateNotEmpty(presetName, "Preset name"))
            {
                return null;
            }

            return ErrorHandler.Try(
                func: () =>
                {
                    string filePath = Path.Combine(PRESETS_PATH, $"{presetName}.json");
                    
                    if (!File.Exists(filePath))
                    {
                        ErrorHandler.LogWarning($"Preset not found: {presetName}");
                        return null;
                    }

                    string json = File.ReadAllText(filePath);
                    return JsonUtility.FromJson<FolderPreset>(json);
                },
                defaultValue: null,
                onError: (ex) => ErrorHandler.LogError($"Failed to load preset: {ex.Message}"),
                context: $"Load preset: {presetName}"
            );
        }

        /// <summary>
        /// Get all available presets
        /// </summary>
        public List<FolderPreset> GetAllPresets()
        {
            if (cachedPresets != null)
            {
                return cachedPresets;
            }

            cachedPresets = ErrorHandler.Try(
                func: () =>
                {
                    var presets = new List<FolderPreset>();

                    // Add built-in presets
                    presets.AddRange(GetBuiltInPresets());

                    // Load custom presets
                    if (Directory.Exists(PRESETS_PATH))
                    {
                        var jsonFiles = Directory.GetFiles(PRESETS_PATH, "*.json");
                        foreach (var file in jsonFiles)
                        {
                            string json = File.ReadAllText(file);
                            var preset = JsonUtility.FromJson<FolderPreset>(json);
                            if (preset != null)
                            {
                                presets.Add(preset);
                            }
                        }
                    }

                    return presets;
                },
                defaultValue: new List<FolderPreset>(),
                onError: (ex) => ErrorHandler.LogError($"Failed to get presets: {ex.Message}"),
                context: "Get all presets"
            );

            return cachedPresets;
        }

        /// <summary>
        /// Delete a custom preset
        /// </summary>
        public void DeletePreset(string presetName, Action<bool, string> callback)
        {
            if (!ErrorHandler.ValidateNotEmpty(presetName, "Preset name"))
            {
                callback?.Invoke(false, "Preset name cannot be empty");
                return;
            }

            ErrorHandler.Try(
                action: () =>
                {
                    string filePath = Path.Combine(PRESETS_PATH, $"{presetName}.json");
                    
                    if (!File.Exists(filePath))
                    {
                        callback?.Invoke(false, "Preset not found");
                        return;
                    }

                    File.Delete(filePath);
                    AssetDatabase.Refresh();
                    
                    cachedPresets = null; // Clear cache
                    
                    ErrorHandler.Log($"Preset deleted: {presetName}");
                    callback?.Invoke(true, $"Preset '{presetName}' deleted successfully");
                },
                onError: (ex) =>
                {
                    ErrorHandler.LogError($"Failed to delete preset: {ex.Message}");
                    callback?.Invoke(false, "Failed to delete preset");
                },
                context: $"Delete preset: {presetName}"
            );
        }

        /// <summary>
        /// Validate folder structure (check if folders exist)
        /// </summary>
        public bool ValidateFolderStructure(List<string> folders, out List<string> missingFolders)
        {
            missingFolders = new List<string>();

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder))
                {
                    missingFolders.Add(folder);
                }
            }

            return missingFolders.Count == 0;
        }

        #endregion

        #region Private Methods

        private bool CreateSingleFolder(string path)
        {
            return ErrorHandler.Try(
                func: () =>
                {
                    // Normalize path
                    path = path.Replace("\\", "/");

                    // Check if folder already exists
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        ErrorHandler.Log($"Folder already exists: {path}");
                        return false;
                    }

                    // Create parent folders if needed
                    string[] pathParts = path.Split('/');
                    string currentPath = pathParts[0];

                    for (int i = 1; i < pathParts.Length; i++)
                    {
                        string newPath = currentPath + "/" + pathParts[i];
                        
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            string guid = AssetDatabase.CreateFolder(currentPath, pathParts[i]);
                            if (string.IsNullOrEmpty(guid))
                            {
                                ErrorHandler.LogWarning($"Failed to create folder: {newPath}");
                                return false;
                            }
                        }
                        
                        currentPath = newPath;
                    }

                    return true;
                },
                defaultValue: false,
                onError: (ex) => ErrorHandler.LogError($"Error creating folder '{path}': {ex.Message}"),
                context: $"Create folder: {path}"
            );
        }

        private List<FolderPreset> GetBuiltInPresets()
        {
            return new List<FolderPreset>
            {
                new FolderPreset
                {
                    Name = "Standard Project",
                    Description = "Standard Unity project structure",
                    Folders = new List<string>
                    {
                        "_Project/Scripts/Core",
                        "_Project/Scripts/Utilities",
                        "_Project/Scripts/Gameplay",
                        "_Project/Scripts/UI",
                        "_Project/Scripts/Managers",
                        "_Project/Prefabs",
                        "_Project/Materials",
                        "_Project/Textures",
                        "_Project/Models",
                        "_Project/Audio/Music",
                        "_Project/Audio/SFX",
                        "_Project/Scenes",
                        "_Project/Resources",
                        "_Project/Animation",
                        "Plugins"
                    }
                },
                new FolderPreset
                {
                    Name = "Mobile Game",
                    Description = "Optimized for mobile game development",
                    Folders = new List<string>
                    {
                        "_Project/Scripts/Managers",
                        "_Project/Scripts/UI",
                        "_Project/Scripts/Gameplay",
                        "_Project/Scripts/Data",
                        "_Project/Scripts/Services",
                        "_Project/UI/Sprites",
                        "_Project/UI/Prefabs",
                        "_Project/UI/Fonts",
                        "_Project/Levels",
                        "_Project/Audio",
                        "_Project/Resources",
                        "_Project/Scenes",
                        "_Project/Settings"
                    }
                },
                new FolderPreset
                {
                    Name = "VR Project",
                    Description = "Virtual Reality project structure",
                    Folders = new List<string>
                    {
                        "_Project/Scripts/VR",
                        "_Project/Scripts/Interaction",
                        "_Project/Scripts/UI",
                        "_Project/Scripts/Core",
                        "_Project/Prefabs/VR",
                        "_Project/Prefabs/Interactable",
                        "_Project/Materials",
                        "_Project/Models",
                        "_Project/Audio/Spatial",
                        "_Project/Scenes",
                        "_Project/Resources"
                    }
                },
                new FolderPreset
                {
                    Name = "2D Platformer",
                    Description = "2D platformer game structure",
                    Folders = new List<string>
                    {
                        "_Project/Scripts/Player",
                        "_Project/Scripts/Enemies",
                        "_Project/Scripts/Managers",
                        "_Project/Scripts/UI",
                        "_Project/Sprites/Characters",
                        "_Project/Sprites/Environment",
                        "_Project/Sprites/UI",
                        "_Project/Animation",
                        "_Project/Prefabs",
                        "_Project/Audio",
                        "_Project/Levels",
                        "_Project/Scenes"
                    }
                },
                new FolderPreset
                {
                    Name = "Minimal",
                    Description = "Minimal project structure",
                    Folders = new List<string>
                    {
                        "_Project/Scripts",
                        "_Project/Prefabs",
                        "_Project/Scenes",
                        "_Project/Resources"
                    }
                }
            };
        }

        #endregion
    }

    /// <summary>
    /// Data class for folder presets
    /// </summary>
    [Serializable]
    public class FolderPreset
    {
        public string Name;
        public string Description;
        public List<string> Folders = new List<string>();
        public bool IsBuiltIn;
    }
}

