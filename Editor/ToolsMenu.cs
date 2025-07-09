#if UNITY_EDITOR
using System;
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

        [MenuButtonItem("Setup/Folders", "Type-Based", "Create a type-based folder structure")]
        public static void CreateTypeBasedDefaultFolders()
        {
            try
            {
                // Create base function folders
                FolderHelpers.CreateFolders(RootFolder, FolderStructures.DefaultStructures["Type"]);

                // Cleanup default folders
                FolderHelpers.CleanupDefaultFolders();
                Debug.Log("Type-based folder structure created successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating folders: {e.Message}");
            }
        }

        [MenuButtonItem("Setup/Folders", "Function-Based", "Create a function-based folder structure")]
        public static void CreateFunctionBasedDefaultFolders()
        {
            try
            {
                // Create base function folders
                FolderHelpers.CreateFolders(RootFolder, FolderStructures.DefaultStructures["Function"]);

                // Cleanup default folders
                FolderHelpers.CleanupDefaultFolders();
                Debug.Log("Function-based folder structure created successfully");
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
                // keep InputSystem last because it requires a restart
                "com.unity.inputsystem"
            };

            await PackageManager.InstallPackages(packages);
        }

        [MenuButtonItem("Setup/Scene", "Basic Hierarchy", "Create a basic scene hierarchy")]
        public static void CreateBasicSceneHierarchy()
        {
            try
            {
                SceneHierarchySetup.CreateBasicHierarchy();
                Debug.Log("Scene hierarchy setup completed");
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
            Debug.Log("Domain reload disabled.");
        }

        [MenuButtonItem("Scripts/Utilities", "Singleton", "Import Singleton script")]
        private static void ImportSingleton()
        {
            ToolsHub.ShowScriptImportPage("Singleton.txt", "Singleton.cs", "Singleton");
        }
    }
}
#endif