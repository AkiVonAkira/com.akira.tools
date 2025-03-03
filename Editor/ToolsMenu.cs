#if UNITY_EDITOR
using System;
using akira.Folders;
using akira.Packages;
using akira.Scene;
using UnityEditor;
using UnityEngine;

//using akira.Assets;

namespace akira
{
    public static class ToolsMenu
    {
        private const string RootFolder = "_Project";

        [MenuItem("Tools/Setup/Folders/Type-Based")]
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

        [MenuItem("Tools/Setup/Folders/Function-Based")]
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

        [MenuItem("Tools/Setup/Packages/Essential")]
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

        [MenuItem("Tools/Setup/Scene/Create Basic Hierarchy")]
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

        [MenuItem("Tools/Setup/Disable Domain Reload")]
        public static void DisableDomainReload()
        {
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            Debug.Log("Domain reload disabled.");
        }
    }
}
#endif