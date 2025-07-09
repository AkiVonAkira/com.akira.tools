#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using akira.Folders;
using akira.Packages;
using akira.Scene;
using UnityEditor;
using UnityEngine;

namespace akira
{
    public static class ToolsMenu
    {
        private const string RootFolder = "_Project";
        public static string SelectedFolderStructure = "Type"; // "Type" or "Function"

        public static string GetSelectedFolderStructureName() => SelectedFolderStructure;

        public static string GetDefaultScriptOutputPath(string scriptOutputName, string menuPath = null)
        {
            if (!string.IsNullOrEmpty(menuPath))
            {
                var parts = menuPath.Trim('/').Split('/');
                var scriptsIdx = Array.IndexOf(parts, "Scripts");
                var subPath = scriptsIdx >= 0 && scriptsIdx < parts.Length - 1
                    ? string.Join(Path.DirectorySeparatorChar.ToString(), parts, scriptsIdx + 1, parts.Length - scriptsIdx - 1)
                    : string.Join(Path.DirectorySeparatorChar.ToString(), parts);

                return Path.Combine(Application.dataPath, "_Project", "_Scripts", subPath, scriptOutputName);
            }
            return Path.Combine(Application.dataPath, "_Project", "_Scripts", scriptOutputName);
        }

        [MenuButtonItem("Setup/Folders", "Type-Based", "Create a type-based folder structure")]
        public static void CreateTypeBasedDefaultFolders()
        {
            SelectedFolderStructure = "Type";
            try
            {
                FolderHelpers.CreateFolders(RootFolder, FolderStructures.DefaultStructures["Type"]);
                FolderHelpers.CleanupDefaultFolders();
                ToolsHub.ShowNotification("Type-based folder structure created successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating folders: {e.Message}");
            }
        }

        [MenuButtonItem("Setup/Folders", "Function-Based", "Create a function-based folder structure")]
        public static void CreateFunctionBasedDefaultFolders()
        {
            SelectedFolderStructure = "Function";
            try
            {
                FolderHelpers.CreateFolders(RootFolder, FolderStructures.DefaultStructures["Function"]);
                FolderHelpers.CleanupDefaultFolders();
                ToolsHub.ShowNotification("Function-based folder structure created successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating folders: {e.Message}");
            }
        }

        [MenuButtonItem("Setup/Packages", "Install Essentials", "Install all essential packages")]
        public static async void InstallEssentialPackages()
        {
            string[] packages =
            {
                "com.unity.cinemachine",
                "com.unity.2d.animation",
                "git+https://github.com/adammyhre/Unity-Utils.git",
                "git+https://github.com/adammyhre/Unity-Improved-Timers.git",
                "com.unity.inputsystem"
            };

            await PackageManager.InstallPackages(packages);
            ToolsHub.ShowNotification("Essential packages installed (check Console for details)");
        }

        [MenuButtonItem("Setup/Scene", "Basic Hierarchy", "Create a basic scene hierarchy")]
        public static void CreateBasicSceneHierarchy()
        {
            try
            {
                SceneHierarchySetup.CreateBasicHierarchy();
                ToolsHub.ShowNotification("Scene hierarchy setup completed");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting up scene hierarchy: {e.Message}");
            }
        }

        [MenuButtonItem("Settings", "Disable Domain Reload", "Disable domain reload for faster play mode")]
        public static void DisableDomainReload()
        {
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            ToolsHub.ShowNotification("Domain reload disabled.");
        }

        [MenuButtonItem("Scripts/Utilities", "Singleton", "Import Singleton script")]
        private static void ImportSingleton()
        {
            ToolsHub.ShowScriptImportPage("Singleton.txt", "Singleton.cs", "Singleton", "Scripts/Utilities");
        }

        [MenuButtonItem("Setup/Folders", "Customize...", "Customize which folders are created")]
        public static void CustomizeFolders()
        {
            var structure = GetSelectedFolderStructureName();
            var initialFolders = new List<string>(FolderStructures.DefaultStructures[structure]);
            if (!initialFolders.Contains("_Scripts"))
                initialFolders.Insert(0, "_Scripts");
            var nonRemovable = new HashSet<string> { "_Project", "_Scripts", "_Scripts/Utilities" };
            ToolsHub.ShowFolderCustomizationPage(initialFolders, nonRemovable, structure);
        }

        public static void ApplyCustomFolders(List<string> folders)
        {
            FolderHelpers.CreateFolders(RootFolder, folders.ToArray());
        }
    }
}
#endif