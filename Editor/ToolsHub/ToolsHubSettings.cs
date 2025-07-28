#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace akira.ToolsHub
{
    [Serializable]
    public class FoldoutStateEntry
    {
        public string Key;
        public bool Value;
    }

    [Serializable]
    public class PackageEntry
    {
        public string Id;
        public string DisplayName;
        public bool IsEssential;
        public bool IsEnabled = true;
        public string Description;
    }

    [Serializable]
    public class ToolsHubSettingsData
    {
        public bool AssetPrefixEnabled = true;
        public int RecentRenameDisplayCount = 5;

        // Use a serializable list for foldout states
        public List<FoldoutStateEntry> FoldoutStatesList = new();

        // Package management
        public List<PackageEntry> PackagesList = new();

        // Not serialized, runtime only
        [NonSerialized] private Dictionary<string, bool> _foldoutStatesDict;
        [NonSerialized] private Dictionary<string, PackageEntry> _packagesDict;

        public Dictionary<string, bool> GetFoldoutStatesDict()
        {
            if (_foldoutStatesDict == null)
            {
                _foldoutStatesDict = new Dictionary<string, bool>();

                if (FoldoutStatesList != null)
                    foreach (var entry in FoldoutStatesList)
                        if (!string.IsNullOrEmpty(entry.Key))
                            _foldoutStatesDict[entry.Key] = entry.Value;
            }

            return _foldoutStatesDict;
        }

        public void SetFoldoutState(string key, bool value)
        {
            var dict = GetFoldoutStatesDict();
            dict[key] = value;

            // Update or add in the list
            var entry = FoldoutStatesList.Find(e => e.Key == key);

            if (entry != null)
                entry.Value = value;
            else
                FoldoutStatesList.Add(new FoldoutStateEntry { Key = key, Value = value });
        }

        // Package management methods
        public Dictionary<string, PackageEntry> GetPackagesDict()
        {
            if (_packagesDict == null)
            {
                _packagesDict = new Dictionary<string, PackageEntry>();

                if (PackagesList != null)
                    foreach (var package in PackagesList)
                        if (!string.IsNullOrEmpty(package.Id))
                            _packagesDict[package.Id] = package;
            }

            return _packagesDict;
        }

        public PackageEntry GetPackage(string packageId)
        {
            var dict = GetPackagesDict();

            if (dict.TryGetValue(packageId, out var entry))
                return entry;

            return null;
        }

        public void AddOrUpdatePackage(PackageEntry package)
        {
            var dict = GetPackagesDict();
            dict[package.Id] = package;

            // Update in the list
            var index = PackagesList.FindIndex(p => p.Id == package.Id);

            if (index >= 0)
                PackagesList[index] = package;
            else
                PackagesList.Add(package);
        }

        public void RemovePackage(string packageId)
        {
            var dict = GetPackagesDict();

            if (dict.ContainsKey(packageId))
                dict.Remove(packageId);

            PackagesList.RemoveAll(p => p.Id == packageId);
        }
    }

    public static class ToolsHubSettings
    {
        // Settings are saved in:
        // Saves/ToolsHubSettings.json (relative to project root)

        private static readonly string SettingsPath = Path.Combine(
            Application.dataPath, "../Saves/ToolsHubSettings.json");

        private static ToolsHubSettingsData _data;

        public static ToolsHubSettingsData Data
        {
            get
            {
                if (_data == null)
                    Load();

                return _data;
            }
        }

        public static bool GetFoldoutState(string key, bool defaultValue = true)
        {
            var dict = Data.GetFoldoutStatesDict();

            if (dict.TryGetValue(key, out var value))
                return value;

            return defaultValue;
        }

        public static void SetFoldoutState(string key, bool value)
        {
            Data.SetFoldoutState(key, value);
            Save();
        }

        // Package management
        public static PackageEntry GetPackage(string packageId)
        {
            return Data.GetPackage(packageId);
        }

        public static void AddOrUpdatePackage(PackageEntry package)
        {
            Data.AddOrUpdatePackage(package);
            Save();
        }

        public static void RemovePackage(string packageId)
        {
            Data.RemovePackage(packageId);
            Save();
        }

        public static List<PackageEntry> GetAllPackages()
        {
            return Data.PackagesList;
        }

        public static void SetPackageEnabled(string packageId, bool enabled)
        {
            var package = Data.GetPackage(packageId);

            if (package != null)
            {
                package.IsEnabled = enabled;
                Save();
            }
        }

        public static void Load()
        {
            EnsureSaveFolder();

            if (File.Exists(SettingsPath))
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    _data = JsonUtility.FromJson<ToolsHubSettingsData>(json) ?? new ToolsHubSettingsData();

                    // Validate package entries to avoid problematic ones
                    ValidateSettings();
                }
                catch (Exception ex)
                {
                    ToolsHubManger.ShowNotification("Error loading settings - creating new settings file", "warning");
                    Debug.LogError($"Error loading ToolsHub settings: {ex.Message}. Creating new settings file.");
                    _data = new ToolsHubSettingsData();
                }
            else
                _data = new ToolsHubSettingsData();
        }

        // Add a validation method to ensure settings are always clean
        private static void ValidateSettings()
        {
            // Don't use Package Manager APIs here - just basic cleanup
            if (_data?.PackagesList == null) return;

            var needsSave = false;

            // Find and remove any problematic TextMeshPro entries
            for (var i = _data.PackagesList.Count - 1; i >= 0; i--)
            {
                var package = _data.PackagesList[i];

                // Check for problematic entries
                if (package.Id == "com.unity.textmeshpro" ||
                    (package.Id != "TextMeshPro" && package.Id.Contains("textmeshpro")))
                {
                    _data.PackagesList.RemoveAt(i);
                    needsSave = true;
                }
            }

            // Ensure proper TextMeshPro entry exists
            var hasTmpEntry = _data.PackagesList.Any(p => p.Id == "TextMeshPro" && p.DisplayName == "TextMeshPro");

            if (!hasTmpEntry)
            {
                _data.PackagesList.Add(new PackageEntry
                {
                    Id = "TextMeshPro",
                    DisplayName = "TextMeshPro",
                    Description =
                        "Advanced text rendering system with improved visual quality and performance. Resources need to be imported.",
                    IsEssential = true,
                    IsEnabled = true
                });
                needsSave = true;
            }

            // Save changes if needed
            if (needsSave) Save();
        }

        public static void Save()
        {
            EnsureSaveFolder();
            var json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(SettingsPath, json);
        }

        private static void EnsureSaveFolder()
        {
            var dir = Path.GetDirectoryName(SettingsPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif