using System.Collections.Generic;
using System.Linq;

namespace akira.Folders
{
    public static class FolderStructures
    {
        private static readonly string[] SubFolders = { "Materials", "Models", "Prefabs", "Textures", "Animations" };

        private static readonly string[] ObjectTypes =
        {
            "Objects/Architecture/[ArchitectureName]",
            "Objects/Props/[PropName]",
            "Characters/[CharacterName]",
            "Enemies/[EnemyName]",
            "VFX/[VFXName]",
            "Player"
        };

        private static readonly string[] ScriptFolders =
        {
            // Core Systems
            "_Scripts/Core",
            "_Scripts/GameFlow",
            "_Scripts/Managers",
            "_Scripts/Services",
            "_Scripts/Systems",

            // Gameplay
            "_Scripts/AI",
            "_Scripts/Controllers",
            "_Scripts/Input",
            "_Scripts/Physics",
            "_Scripts/Spawners",
            "_Scripts/States",
            "_Scripts/Units",

            // Data & Events
            "_Scripts/Data",
            "_Scripts/Events",
            "_Scripts/SaveLoad",
            "_Scripts/Scriptables",

            // UI & Audio
            "_Scripts/Audio",
            "_Scripts/UI",
            "_Scripts/VFX",

            // Objects & Pooling
            "_Scripts/Objects",
            "_Scripts/Pooling",

            // Networking
            "_Scripts/Networking",

            // Development & Testing
            "_Scripts/Editor",
            "_Scripts/Tests/Editor",
            "_Scripts/Tests/Runtime",

            // Utilities & Extensions
            "_Scripts/Extensions",
            "_Scripts/Interfaces",
            "_Scripts/Utilities/Constants",
            "_Scripts/Utilities/Helpers",
        };

        private static readonly string[] TypeBasedFolders =
        {
            // Audio
            "Audio/Ambient",
            "Audio/Music",
            "Audio/SFX",
            "Audio/Voice",

            // Visual Assets
            "Animations/Characters",
            "Animations/UI",
            "Materials/Particles",
            "Materials/Shaders",
            "Materials/Terrain",
            "Materials/UI",
            "Sprites/Characters",
            "Sprites/Environment",
            "Sprites/Items",
            "Sprites/UI",
            "Sprites/VFX",

            // 3D Assets
            "Models/Characters",
            "Models/Environment",
            "Models/FBX",
            "Models/Props",

            // Prefabs
            "Prefabs/Enemies",
            "Prefabs/Managers",
            "Prefabs/Player",
            "Prefabs/Props",
            "Prefabs/Systems",
            "Prefabs/UI Prefabs",
            "Prefabs/VFX",

            // Development
            "Editor/Icons",
            "Editor/Presets",
            "Editor/ScriptTemplates",

            // Resources
            "Resources/DefaultData",
            "Resources/Fonts",
            "Resources/Scriptable Objects",
            "Resources/Shaders",
            "Resources/Settings",

            // Scenes
            "Scenes/Levels",
            "Scenes/Templates",
            "Scenes/Temporary Scenes",
            "Scenes/UI"
        };

        private static readonly string[] FunctionBasedFolders =
        {
            // Development
            "_Dev/FirstnameLastname",
            "_Dev/_Lost&Found",
            "_Dev/Documentation",
            "_Dev/Prototypes",

            // Audio
            "Audio/Ambient",
            "Audio/Music",
            "Audio/SFX",
            "Audio/Voice",

            // Gameplay
            "Gameplay/Abilities",
            "Gameplay/Interactibles",
            "Gameplay/Mechanics",
            "Gameplay/Obstacles",
            "Gameplay/Pickups",
            "Gameplay/Triggers",
            "Gameplay/Weapons",

            // Levels
            "Levels",
            "Levels/Scenes",
            "Levels/Streaming",
            "Levels/Tutorial",

            // Resources
            "Resources",
            "Resources/Configs",
            "Resources/Data",
            "Resources/DefaultStates",

            // UI
            "UI/Animations",
            "UI/Fonts",
            "UI/Images",
            "UI/Prefabs"
        };

        public static readonly Dictionary<string, string[]> DefaultStructures = new()
        {
            ["Type"] = ScriptFolders.Concat(TypeBasedFolders).ToArray(),
            ["Function"] = ObjectTypes
                .SelectMany(objType => SubFolders.Select(sub => $"{objType}/{sub}"))
                .Concat(ScriptFolders)
                .Concat(FunctionBasedFolders)
                .ToArray()
        };
    }
}