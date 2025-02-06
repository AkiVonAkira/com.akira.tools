#if UNITY_EDITOR
using System.IO;
using ringo;
using UnityEngine;
using static UnityEditor.AssetDatabase;
using UnityEditor;

namespace akira
{
    public static class ToolsMenu
    {
        private const string RootFolder = "_Project";
        
        [MenuItem("Tools/Setup/Folders/Type-Based")]
        public static void CreateTypeBasedDefaultFolders()
        {
            Folders.CreateDirectories(
                RootFolder,
                "_Scripts>Controllers",
                "_Scripts>Editor",
                "_Scripts>Interfaces",
                "_Scripts>Managers",
                "_Scripts>Objects",
                "_Scripts>Scriptables",
                "_Scripts>Spawners",
                "_Scripts>States",
                "_Scripts>Systems",
                "_Scripts>UI",
                "_Scripts>Units",
                "_Scripts>Utilities",
                "Animations",
                "Audio>Music",
                "Editor>Icons",
                "Materials>Shaders",
                "Materials>Terrain",
                "Models>FBX",
                "Prefabs>Enemies",
                "Prefabs>Player",
                "Prefabs>Props",
                "Prefabs>UI Prefabs",
                "Prefabs>VFX",
                "Resources>Fonts",
                "Resources>Scriptable Objects",
                "Resources>Shaders",
                "Scenes>Templates",
                "Scenes>Temporary Scenes",
                "Sprites>UI"
            );
            Refresh();
        }
        
        [MenuItem("Tools/Setup/Folders/Function-Based")]
        public static void CreateFunctionBasedDefaultFolders()
        {
            Folders.CreateDirectories(
                RootFolder,

                "_Dev>FirstnameLastname",
                "_Dev>_Lost&Found",

                "_Scripts>Controllers",
                "_Scripts>Editor",
                "_Scripts>Interfaces",
                "_Scripts>Managers",
                "_Scripts>Objects",
                "_Scripts>Scriptables",
                "_Scripts>Spawners",
                "_Scripts>States",
                "_Scripts>Systems",
                "_Scripts>UI",
                "_Scripts>Units",
                "_Scripts>Utilities",

                "Gameplay>Triggers", 
                "Gameplay>Interactibles", 
                "Gameplay>Pickups", 
                "Gameplay>Obstacles", 

                "Audio>Music",
                "Audio>SFX",

                "Levels", 

                "UI>Fonts",

                "Resources"
            );
            
            WorldObjectFolders.Create(
                RootFolder, 
                "Objects>Architecture>[ArchitectureName]", 
                "Objects>Props>[PropName]", 
                "Characters>[CharacterName]", 
                "Enemies>[EnemyName]", 
                "VFX>[VFXName]",
                "Player"
                );
            
            Refresh();
        }

        // [MenuItem("Tools/Setup/Load New Manifest (2D)")]
        // static async void LoadNewManifest()
        // {
        //     await Packages.ReplacePackageFromGist("7b3e5fd1f6bd7d18fd23b382765b938b");
        //     Debug.Log("Loaded new manifest successfully!");
        // }

        [MenuItem("Tools/Setup/Packages/New Input System")]
        static async void AddNewInputSystem()
        {
            bool success = await Packages.InstallUnityPackage("com.unity.inputsystem");
            if (success)
                Debug.Log("New Input System package installed successfully.");
        }

        [MenuItem("Tools/Setup/Packages/Post Processing")]
        static async void AddPostProcessing()
        {
            bool success = await Packages.InstallUnityPackage("com.unity.postprocessing");
            if (success)
                Debug.Log("Post Processing package installed successfully.");
        }

        [MenuItem("Tools/Setup/Packages/Cinemachine")]
        static async void AddCinemachine()
        {
            bool success = await Packages.InstallUnityPackage("com.unity.cinemachine");
            if (success)
                Debug.Log("Cinemachine package installed successfully.");
        }
    }
}
#endif
