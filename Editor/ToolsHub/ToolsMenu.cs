#if UNITY_EDITOR
using System;
using akira.Folders;
using akira.Scene;
using UnityEditor;
using UnityEngine;

namespace akira.ToolsHub
{
    public static class ToolsMenu
    {
        internal const string RootFolder = "_Project";
        public static string SelectedFolderStructure = "Type";

        [MenuButtonItem("Setup/Folders", "Type-Based", "Create a type-based folder structure")]
        public static void CreateTypeBasedDefaultFolders()
        {
            SelectedFolderStructure = "Type";

            try
            {
                FolderHelpers.CreateFolders(RootFolder, FolderStructures.DefaultStructures["Type"]);
                FolderHelpers.CleanupDefaultFolders();
                ToolsHubManger.ShowNotification("Type-based folder structure created successfully", "success");
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
                ToolsHubManger.ShowNotification("Function-based folder structure created successfully", "success");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating folders: {e.Message}");
            }
        }


        [MenuButtonItem("Setup/Scene", "Basic Hierarchy", "Create a basic scene hierarchy")]
        public static void CreateBasicSceneHierarchy()
        {
            try
            {
                SceneHierarchySetup.CreateBasicHierarchy();
                ToolsHubManger.ShowNotification("Scene hierarchy setup completed", "success");
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
            ToolsHubManger.ShowNotification("Domain reload disabled.", "success");
        }
    }
}
#endif