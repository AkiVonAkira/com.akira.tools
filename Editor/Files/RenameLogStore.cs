#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor.Files
{
    [Serializable]
    public class RenameLogData
    {
        public List<AssetRenameLogEntry> RecentRenames = new();
    }

    [Serializable]
    public class AssetRenameLogEntry
    {
        public string IconPath;
        public string OldName;
        public string NewName;
        public string AssetPath;
        public string DateTime;
        public string OriginalAssetPath;
        public string PreviousAssetPath;
        public string Guid;
    }

    public static class RenameLogStore
    {
        private static readonly string LogPath = Path.Combine(
            Application.dataPath, "../Saves/ToolsHubRenameLog.json");

        private static RenameLogData _data;

        public static RenameLogData Data
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

            if (File.Exists(LogPath))
            {
                var json = File.ReadAllText(LogPath);
                _data = JsonUtility.FromJson<RenameLogData>(json) ?? new RenameLogData();
            }
            else
            {
                _data = new RenameLogData();
            }
        }

        public static void Save()
        {
            EnsureSaveFolder();
            var json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(LogPath, json);
        }

        private static void EnsureSaveFolder()
        {
            var dir = Path.GetDirectoryName(LogPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static void AddRename(string oldName, string newName, string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            // Try to find the previous rename entry for this asset by GUID
            AssetRenameLogEntry previousEntry = null;

            foreach (var entry in Data.RecentRenames)
                if (!string.IsNullOrEmpty(entry.Guid) && entry.Guid == guid)
                {
                    previousEntry = entry;

                    break;
                }

            Data.RecentRenames.Insert(0,
                new AssetRenameLogEntry
                {
                    IconPath = guid,
                    OldName = oldName,
                    NewName = newName,
                    AssetPath = assetPath,
                    DateTime = DateTime.Now.ToString("u"),
                    Guid = guid,
                    PreviousAssetPath = previousEntry?.AssetPath,
                    OriginalAssetPath = previousEntry?.OriginalAssetPath ?? oldName
                });
            Save();
        }

        public static string GetCurrentAssetPathForRename(AssetRenameLogEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Guid))
                return entry.AssetPath;

            var assetPath = AssetDatabase.GUIDToAssetPath(entry.Guid);

            if (!string.IsNullOrEmpty(assetPath))
                return assetPath;

            return entry.AssetPath;
        }
    }
}
#endif