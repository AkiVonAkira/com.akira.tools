#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using akira.Packages;
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
    public class ToolsHubSettingsData
    {
        public bool AssetPrefixEnabled = true;
        public int RecentRenameDisplayCount = 5;
        public List<FoldoutStateEntry> FoldoutStatesList = new();
        public List<PackageEntry> PackagesList = new();
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
            var entry = FoldoutStatesList.Find(e => e.Key == key);

            if (entry != null)
                entry.Value = value;
            else
                FoldoutStatesList.Add(new FoldoutStateEntry { Key = key, Value = value });
        }

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
                    ValidateSettings();
                }
                catch (Exception ex)
                {
                    ToolsHubManager.ShowNotification("Error loading settings - creating new settings file", "warning");
                    Debug.LogError($"Error loading ToolsHub settings: {ex.Message}. Creating new settings file.");
                    _data = new ToolsHubSettingsData();
                }
            else
                _data = new ToolsHubSettingsData();
        }

        private static void ValidateSettings()
        {
            if (_data?.PackagesList == null) return;
            var needsSave = false;

            // Remove conflicting TMP entries
            for (var i = _data.PackagesList.Count - 1; i >= 0; i--)
            {
                var package = _data.PackagesList[i];
                if (package.Id == "com.unity.textmeshpro" ||
                    (package.Id != "TextMeshPro" && package.Id.Contains("textmeshpro")))
                {
                    _data.PackagesList.RemoveAt(i);
                    needsSave = true;
                }
            }

            // Ensure TMP entry exists
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

            // Seed default Asset Store entries
            if (EnsureDefaultAssetStoreEntries())
                needsSave = true;

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

        // ---- Default Asset Store entries ----
        private static readonly string[] DefaultAssets = {
            "https://assetstore.unity.com/packages/tools/utilities/better-hierarchy-272963",
            "https://assetstore.unity.com/packages/tools/utilities/ui-preview-for-prefabs-and-canvases-226906",
            "https://assetstore.unity.com/packages/tools/utilities/mouse-button-shortcuts-and-selection-history-228013",
            "https://assetstore.unity.com/packages/tools/utilities/better-transform-size-notes-global-local-workspace-parent-child--321300",
            "https://assetstore.unity.com/packages/tools/utilities/better-mesh-mesh-preview-full-insight-at-a-glance-321364",
            "https://assetstore.unity.com/packages/tools/utilities/editor-auto-save-234445",
            "https://assetstore.unity.com/packages/tools/utilities/component-names-212478",
            "https://assetstore.unity.com/packages/tools/utilities/scene-view-bookmark-tool-244521",
            "https://assetstore.unity.com/packages/tools/utilities/scene-selection-tool-244501",
            "https://assetstore.unity.com/packages/tools/audio/audio-preview-tool-244446",
            "https://assetstore.unity.com/packages/tools/utilities/favorites-window-123487",
            "https://assetstore.unity.com/packages/tools/utilities/selection-history-184204",
            "https://assetstore.unity.com/packages/tools/utilities/fullscreen-editor-69534",
            "https://assetstore.unity.com/packages/tools/painting/color-studio-151892",
            "https://assetstore.unity.com/packages/tools/utilities/editor-console-pro-11889",
            "https://assetstore.unity.com/packages/tools/animation/primetween-high-performance-animations-and-sequences-252960",
            "https://assetstore.unity.com/packages/tools/utilities/odin-validator-227861",
            "https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041",
            "https://assetstore.unity.com/packages/tools/utilities/hot-reload-edit-code-without-compiling-254358",
            "https://assetstore.unity.com/packages/tools/utilities/colourful-hierarchy-category-gameobject-205934",
            "https://assetstore.unity.com/packages/tools/utilities/asset-store-publishing-tools-115",
            "https://assetstore.unity.com/packages/tools/utilities/asset-validator-271338",
            "https://assetstore.unity.com/packages/tools/utilities/screenshot-utility-177723",
            "https://assetstore.unity.com/packages/tools/utilities/export-project-to-zip-243983",
            "https://assetstore.unity.com/packages/tools/utilities/tableforge-scriptableobject-management-tool-326608"
        };

        private static bool EnsureDefaultAssetStoreEntries()
        {
            var added = false;
            foreach (var url in DefaultAssets)
            {
                var id = BuildAssetIdFromUrl(url);

                // Already present by id?
                var existing = _data.GetPackage(id);
                if (existing != null) continue;

                // Already present by URL?
                if (_data.PackagesList.Any(p => !string.IsNullOrEmpty(p.AssetStoreUrl) &&
                                                string.Equals(p.AssetStoreUrl, url, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var entry = new PackageEntry
                {
                    Id = id,
                    DisplayName = null, // will be populated by fetcher header later
                    Description = null,
                    IsEssential = false,
                    IsEnabled = true,
                    IsAssetStore = true,
                    IsFree = null,
                    AssetStoreUrl = url,
                    Price = null,
                    LastAssetFetchUnixSeconds = 0,
                    ExtraTags = new List<string>()
                };

                _data.AddOrUpdatePackage(entry);
                added = true;
            }
            return added;
        }

        private static string BuildAssetIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var u = url.Trim();
                if (u.EndsWith("/")) u = u.Substring(0, u.Length - 1);
                var last = u.Split('/').LastOrDefault();
                if (!string.IsNullOrEmpty(last)) return $"assetstore:{last}";
                return $"assetstore:{u.GetHashCode()}";
            }
            catch { return $"assetstore:{url.GetHashCode()}"; }
        }
    }
}
#endif