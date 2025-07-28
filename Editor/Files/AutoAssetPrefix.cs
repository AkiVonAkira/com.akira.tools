#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using akira.ToolsHub;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Editor.Files
{
    public class AutoAssetPrefix : AssetPostprocessor
    {
        private static readonly HashSet<string> LoggedErrors = new();
        private static readonly HashSet<string> SubstanceFolders = new();

        private static readonly string[] AdobeSubstancePaths = { "Assets/Adobe/Substance3DForUnity" };

        public static bool Enabled
        {
            get => ToolsHubSettings.Data.AssetPrefixEnabled;
            set
            {
                ToolsHubSettings.Data.AssetPrefixEnabled = value;
                ToolsHubSettings.Save();
            }
        }

        public static int RecentRenameDisplayCount
        {
            get => ToolsHubSettings.Data.RecentRenameDisplayCount;
            set
            {
                ToolsHubSettings.Data.RecentRenameDisplayCount = value;
                ToolsHubSettings.Save();
            }
        }

        public static List<AssetRenameLogEntry> RecentRenames => RenameLogStore.Data.RecentRenames;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (!Enabled)
                return;

            foreach (var assetPath in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                if (IsSubstanceGraphSOType(asset))
                {
                    var folder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
                    SubstanceFolders.Add(folder);
                }
            }

            if (SubstanceFolders.Count > 0) Debug.Log($"Substance folders: {string.Join(", ", SubstanceFolders)}");

            foreach (var assetPath in importedAssets)
            {
                var normalizedAssetPath = assetPath.Replace("\\", "/");

                if (SubstanceFolders.Any(folder => normalizedAssetPath.StartsWith(folder)))
                {
                    Debug.Log($"Skipping asset: {assetPath} as it is in a SubstanceGraphSO folder.");

                    continue;
                }

                ProcessAsset(assetPath);
            }
        }

        private static void ProcessAsset(string assetPath)
        {
            if (IsAdobeSubstanceAsset(assetPath) || IsAdobeSubstanceImporter(assetPath))
                return;

            if (!assetPath.StartsWith("Assets/_Project"))
                return;

            if (assetPath.EndsWith(".cs"))
                return;

            Object oldAsset = null;

            try
            {
                oldAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            }
            catch
            {
                return;
            }

            if (oldAsset == null || IsSubstanceGraphSOType(oldAsset) || IsAdobeModifiedAsset(oldAsset))
                return;

            var assetType = GetAssetType(assetPath);

            if (assetType == null)
                return;

            var splitFilePath = assetPath.Split('/');
            var splitFileName = splitFilePath.Last().Split('.');
            var fileNameWithoutExtension = splitFileName.First();
            var fileExtension = splitFileName.Last();

            var newName = GetNewName(fileNameWithoutExtension, fileExtension, assetType);

            if (!string.IsNullOrEmpty(newName))
            {
                AssetDatabase.RenameAsset(assetPath, newName);
                AssetDatabase.Refresh();

                var newAssetPath =
                    $"{string.Join("/", splitFilePath.Take(splitFilePath.Length - 1))}/{newName}";
                Object asset = null;

                try
                {
                    asset = AssetDatabase.LoadAssetAtPath<Object>(newAssetPath);
                }
                catch
                {
                    return;
                }

                if (asset == null) return;
                asset.name = newName.Split('/').Last().Split('.').First();
                EditorUtility.SetDirty(asset);

                RenameLogStore.AddRename(fileNameWithoutExtension + "." + fileExtension, newName, newAssetPath);
            }
        }

        private static Type GetAssetType(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (asset != null)
                switch (asset)
                {
                    case ScriptableObject:
                        return typeof(ScriptableObject);
                    case DefaultAsset:
                    case MonoScript:
                    case RenderTexture:
                    case TextAsset:
                        return null;
                    default:
                        return asset.GetType();
                }

            LogErrorOnce($"Failed to load asset at path: {assetPath}");

            return null;
        }

        private static string GetNewName(string fileNameWithoutExtension, string fileExtension, Type assetType)
        {
            foreach (var pair in FileTypePairs)
            {
                if (pair.FileType != fileExtension) continue;

                return fileNameWithoutExtension.Split('_').First() == pair.Prefix
                    ? null
                    : $"{pair.Prefix}_{fileNameWithoutExtension}.{fileExtension}";
            }

            foreach (var pair in AssetTypePairs)
            {
                if (pair.AssetType != assetType) continue;

                return fileNameWithoutExtension.Split('_').First() == pair.Prefix
                    ? null
                    : $"{pair.Prefix}_{fileNameWithoutExtension}.{fileExtension}";
            }

            LogErrorOnce($"Unknown file type: {fileExtension}");
            LogErrorOnce($"Unknown asset type for file: {fileNameWithoutExtension}.{fileExtension}");
            LogErrorOnce($"Unknown .asset file type: {assetType}");

            return null;
        }

        private static void LogErrorOnce(string message)
        {
            if (LoggedErrors.Contains(message)) return;
            Debug.LogError(message);
            LoggedErrors.Add(message);
        }

        #region Prefix Definitions

        private static readonly PrefixFileTypePair[] FileTypePairs =
        {
            new("anim", "AC"),
            new("controller", "CTRL"),
            new("fbx", "FBX"),
            new("mat", "M"),
            new("mp3", "SFX"),
            new("ogg", "SFX"),
            new("png", "SPR"),
            new("prefab", "P"),
            new("scenetemplate", "SCENE"),
            new("shader", "SHADER"),
            new("terrainlayer", "TL"),
            new("unity", "SCENE"),
            new("wav", "SFX")
        };

        private static readonly PrefixAssetTypePair[] AssetTypePairs =
        {
            new(typeof(AnimationClip), "AC"),
            new(typeof(AudioClip), "SFX"),
            new(typeof(GameObject), "FBX"),
            new(typeof(Material), "M"),
            new(typeof(RuntimeAnimatorController), "CTRL"),
            new(typeof(SceneAsset), "SCENE"),
            new(typeof(ScriptableObject), "SO"),
            new(typeof(Shader), "SHADER"),
            new(typeof(TerrainData), "TD"),
            new(typeof(TerrainLayer), "TL"),
            new(typeof(Texture2D), "SPR")
        };

        #endregion

        #region Adobe Substance Importer Specific Checks

        private static bool IsAdobeSubstanceAsset(string assetPath)
        {
            return AdobeSubstancePaths.Any(adobePath => assetPath.StartsWith(adobePath));
        }

        private static bool IsAdobeSubstanceImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);

            return importer != null && importer.GetType().ToString().Contains("Adobe.SubstanceEditor.Importer");
        }

        private static bool IsSubstanceGraphSOType(Object asset)
        {
            return asset is ScriptableObject scriptableObject &&
                   scriptableObject.GetType().FullName == "Adobe.Substance.SubstanceGraphSO";
        }

        private static bool IsAdobeModifiedAsset(Object asset)
        {
            if (asset is Material material) return material.shader.name.Contains("graph_0");
            if (asset is Texture2D texture) return texture.name.Contains("graph_0");

            return false;
        }

        #endregion
    }

    #region Editor

    internal struct PrefixAssetTypePair
    {
        public readonly Type AssetType;
        public readonly string Prefix;

        public PrefixAssetTypePair(Type assetType, string prefix)
        {
            AssetType = assetType;
            Prefix = prefix;
        }
    }

    internal struct PrefixFileTypePair
    {
        public readonly string FileType;
        public readonly string Prefix;

        public PrefixFileTypePair(string pFileType, string pPrefix)
        {
            FileType = pFileType;
            Prefix = pPrefix;
        }
    }

    #endregion
}
#endif