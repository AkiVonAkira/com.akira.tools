#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using akira.ToolsHub;
using UnityEditor;
using UnityEngine;

namespace akira.Folders
{
    [Serializable]
    public class FolderStructurePreset
    {
        public string Name;
        public string Description;
        public List<string> Folders = new();
        public Dictionary<string, bool> EnabledState = new();
        [NonSerialized] public bool IsBuiltIn;

        public string ToJson()
        {
            var serializablePreset = new SerializablePreset
            {
                Name = Name, Description = Description, Folders = new List<string>(Folders)
            };

            foreach (var kvp in EnabledState)
                serializablePreset.EnabledStates.Add(new SerializableEnabledState
                {
                    Folder = kvp.Key, Enabled = kvp.Value
                });

            return JsonUtility.ToJson(serializablePreset, true);
        }

        public static FolderStructurePreset FromJson(string json)
        {
            try
            {
                var serializablePreset = JsonUtility.FromJson<SerializablePreset>(json);

                var preset = new FolderStructurePreset
                {
                    Name = serializablePreset.Name,
                    Description = serializablePreset.Description,
                    Folders = serializablePreset.Folders
                };
                foreach (var item in serializablePreset.EnabledStates) preset.EnabledState[item.Folder] = item.Enabled;

                return preset;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing preset: {e.Message}");

                return null;
            }
        }

        [Serializable]
        public class SerializableEnabledState
        {
            public string Folder;
            public bool Enabled;
        }

        [Serializable]
        public class SerializablePreset
        {
            public string Name;
            public string Description;
            public List<string> Folders = new();
            public List<SerializableEnabledState> EnabledStates = new();
        }
    }

    public static class FolderStructureManager
    {
        private const string PRESETS_KEY = "akira.FolderStructurePresets";
        private static List<FolderStructurePreset> _cachedPresets;

        public static string GetPresetsDirectory()
        {
            var directory = Path.Combine(Application.dataPath, "../FolderPresets");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return directory;
        }

        public static List<FolderStructurePreset> GetAllPresets()
        {
            if (_cachedPresets != null)
                return _cachedPresets;

            _cachedPresets = new List<FolderStructurePreset>();

            // Add built-in presets
            AddBuiltInPresets();

            // Load saved presets
            LoadInternalPresets();

            return _cachedPresets;
        }

        private static void AddBuiltInPresets()
        {
            // Add Type-based
            var typeBasedPreset = new FolderStructurePreset
            {
                Name = "Type-Based (Built-in)",
                Description = "Standard type-based folder structure",
                Folders = new List<string>(FolderStructures.DefaultStructures["Type"]),
                IsBuiltIn = true
            };

            // Add Function-based
            var functionBasedPreset = new FolderStructurePreset
            {
                Name = "Function-Based (Built-in)",
                Description = "Standard function-based folder structure",
                Folders = new List<string>(FolderStructures.DefaultStructures["Function"]),
                IsBuiltIn = true
            };

            // Set all folders as enabled
            foreach (var folder in typeBasedPreset.Folders)
                typeBasedPreset.EnabledState[folder] = true;

            foreach (var folder in functionBasedPreset.Folders)
                functionBasedPreset.EnabledState[folder] = true;

            _cachedPresets.Add(typeBasedPreset);
            _cachedPresets.Add(functionBasedPreset);
        }

        private static void LoadInternalPresets()
        {
            var json = EditorPrefs.GetString(PRESETS_KEY, "{\"presets\":[]}");

            try
            {
                var wrapper = JsonUtility.FromJson<PresetsWrapper>(json);

                if (wrapper != null && wrapper.presets != null)
                    foreach (var presetJson in wrapper.presets)
                    {
                        var preset = FolderStructurePreset.FromJson(presetJson);
                        if (preset != null) _cachedPresets.Add(preset);
                    }
            }
            catch (Exception e)
            {
                ToolsHubManager.ShowNotification("Error loading folder presets", "error");
                Debug.LogError($"Error loading presets: {e.Message}\nJSON: {json}");
                // Reset the corrupted data
                EditorPrefs.SetString(PRESETS_KEY, "{\"presets\":[]}");
            }
        }

        public static void SavePreset(FolderStructurePreset preset)
        {
            if (preset == null || string.IsNullOrEmpty(preset.Name))
                return;

            // Get existing presets
            var presets = GetAllPresets();

            // Remove built-in presets for saving
            presets = presets.Where(p => !p.IsBuiltIn).ToList();

            // Remove existing preset with the same name if it exists
            presets.RemoveAll(p => p.Name == preset.Name);

            // Add the new preset
            presets.Add(preset);

            // Save back
            SaveInternalPresets(presets.Where(p => !p.IsBuiltIn).ToList());

            // Refresh cache
            _cachedPresets = null;
        }

        public static void DeletePreset(string presetName)
        {
            // Get existing presets
            var presets = GetAllPresets();

            // Remove built-in presets for saving
            presets = presets.Where(p => !p.IsBuiltIn).ToList();

            // Remove the preset
            presets.RemoveAll(p => p.Name == presetName);

            // Save back
            SaveInternalPresets(presets);

            // Refresh cache
            _cachedPresets = null;
        }

        private static void SaveInternalPresets(List<FolderStructurePreset> presets)
        {
            // Convert to serializable format
            var wrapper = new PresetsWrapper();
            wrapper.presets = new List<string>();

            foreach (var preset in presets) wrapper.presets.Add(preset.ToJson());

            // Save to EditorPrefs
            var json = JsonUtility.ToJson(wrapper);
            EditorPrefs.SetString(PRESETS_KEY, json);
        }

        public static void ExportPresetToFile(FolderStructurePreset preset, string filePath)
        {
            try
            {
                var json = preset.ToJson();
                File.WriteAllText(filePath, json);
                ToolsHubManager.ShowNotification($"Preset exported to {filePath}", "success");
            }
            catch (Exception e)
            {
                ToolsHubManager.ShowNotification("Error exporting preset", "error");
                Debug.LogError($"Error exporting preset: {e.Message}");
            }
        }

        public static FolderStructurePreset ImportPresetFromFile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);

                return FolderStructurePreset.FromJson(json);
            }
            catch (Exception e)
            {
                ToolsHubManager.ShowNotification("Error importing preset", "error");
                Debug.LogError($"Error importing preset: {e.Message}");

                return null;
            }
        }

        [Serializable]
        private class PresetsWrapper
        {
            public List<string> presets = new();
        }
    }
}
#endif