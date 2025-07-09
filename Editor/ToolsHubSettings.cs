#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace akira
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

        // Use a serializable list for foldout states
        public List<FoldoutStateEntry> FoldoutStatesList = new();

        // Not serialized, runtime only
        [NonSerialized] private Dictionary<string, bool> _foldoutStatesDict;

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

        public static void Load()
        {
            EnsureSaveFolder();

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _data = JsonUtility.FromJson<ToolsHubSettingsData>(json) ?? new ToolsHubSettingsData();
            }
            else
            {
                _data = new ToolsHubSettingsData();
            }
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