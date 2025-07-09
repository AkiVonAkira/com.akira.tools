#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace akira
{
    [Serializable]
    public class ToolsHubSettingsData
    {
        public bool AssetPrefixEnabled = true;
        public int RecentRenameDisplayCount = 5;
        // Only settings here, no rename log
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
