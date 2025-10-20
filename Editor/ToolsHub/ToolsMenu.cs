#if UNITY_EDITOR
using System;
using Akira.Folders;
using Akira.Scene;
using Akira.Tools.Core;
using UnityEditor;
using UnityEngine;

namespace Akira.ToolsHub
{
    public static class ToolsMenu
    {
        internal const string RootFolder = "_Project";
        public static string SelectedFolderStructure = "Type";

        [MenuButtonItem("Setup/Folders", "Type-Based", "Create a type-based folder structure")]
        public static void CreateTypeBasedDefaultFolders()
        {
            SelectedFolderStructure = "Type";

            ErrorHandler.Try(() =>
            {
                FolderHelpers.CreateFolders(RootFolder, FolderStructures.DefaultStructures["Type"]);
                FolderHelpers.CleanupDefaultFolders();
                ToolsHubManager.ShowNotification("Type-based folder structure created successfully", "success");
            },
            onError: (ex) =>
            {
                ErrorHandler.LogErrorWithCode("FLD001", "Type-based folder structure");
                ToolsHubManager.ShowNotification("Failed to create folder structure", "error");
            },
            context: "CreateTypeBasedDefaultFolders");
        }

        [MenuButtonItem("Setup/Folders", "Function-Based", "Create a function-based folder structure")]
        public static void CreateFunctionBasedDefaultFolders()
        {
            SelectedFolderStructure = "Function";

            ErrorHandler.Try(() =>
            {
                FolderHelpers.CreateFolders(RootFolder, FolderStructures.DefaultStructures["Function"]);
                FolderHelpers.CleanupDefaultFolders();
                ToolsHubManager.ShowNotification("Function-based folder structure created successfully", "success");
            },
            onError: (ex) =>
            {
                ErrorHandler.LogErrorWithCode("FLD001", "Function-based folder structure");
                ToolsHubManager.ShowNotification("Failed to create folder structure", "error");
            },
            context: "CreateFunctionBasedDefaultFolders");
        }


        [MenuButtonItem("Setup/Scene", "Basic Hierarchy", "Create a basic scene hierarchy")]
        public static void CreateBasicSceneHierarchy()
        {
            ErrorHandler.Try(() =>
            {
                SceneHierarchySetup.CreateBasicHierarchy();
                ToolsHubManager.ShowNotification("Scene hierarchy setup completed", "success");
            },
            onError: (ex) =>
            {
                ErrorHandler.LogError($"Error setting up scene hierarchy: {ex.Message}");
                ToolsHubManager.ShowNotification("Failed to setup scene hierarchy", "error");
            },
            context: "CreateBasicSceneHierarchy");
        }

        [MenuButtonItem("Settings", "Disable Domain Reload", "Disable domain reload for faster play mode")]
        public static void DisableDomainReload()
        {
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            ToolsHubManager.ShowNotification("Domain reload disabled.", "success");
        }
    }
}
#endif