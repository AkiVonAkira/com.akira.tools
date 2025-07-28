using System.IO;
using UnityEngine;
using static UnityEditor.AssetDatabase;

namespace akira.Folders
{
    public static class FolderHelpers
    {
        public static void CreateFolders(string rootPath, params string[] folders)
        {
            var fullPath = Path.Combine(Application.dataPath, rootPath);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);

            foreach (var folder in folders)
            {
                var path = Path.Combine(fullPath, folder.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(path);
            }

            CreateAssemblyDefinition(rootPath);
            Refresh();
        }

        private static void CreateAssemblyDefinition(string rootPath)
        {
            var packageName = "com.akira.tools";

            var txtPath = Path.Combine(
                Application.dataPath,
                "../Packages",
                packageName,
                "Scripts/ProjectAsmdef.txt"
            );

            var outputPath = Path.Combine(
                Application.dataPath,
                rootPath,
                "_Scripts",
                "_Project.asmdef"
            );

            var scriptsFolder = Path.Combine(Application.dataPath, rootPath, "_Scripts");

            if (!Directory.Exists(scriptsFolder)) Directory.CreateDirectory(scriptsFolder);

            if (!File.Exists(outputPath))
            {
                File.Copy(txtPath, outputPath);
                Debug.Log($"Created assembly definition at: {outputPath}");
            }
        }

        private static void Move(string newParent, string folderName)
        {
            var sourcePath = $"Assets/{folderName}";
            var destinationPath = $"Assets/{newParent}/{folderName}";

            if (!IsValidFolder(sourcePath))
                return;

            if (IsValidFolder(destinationPath))
            {
                var assets = FindAssets("", new[] { sourcePath });

                foreach (var assetGuid in assets)
                {
                    var assetPath = GUIDToAssetPath(assetGuid);

                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    var relativePath = assetPath.Substring(sourcePath.Length);
                    var newPath = destinationPath + relativePath;

                    var dirName = Path.GetDirectoryName(newPath);

                    if (!string.IsNullOrEmpty(dirName) && !IsValidFolder(dirName))
                        CreateFolder(Path.GetDirectoryName(dirName), Path.GetFileName(dirName));

                    CopyAsset(assetPath, newPath);
                }

                DeleteAsset(sourcePath);
                Debug.Log($"Merged {folderName} into {destinationPath}");

                return;
            }

            var error = MoveAsset(sourcePath, destinationPath);

            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"Failed to move {folderName}: {error}");
        }

        private static void Delete(string folderName)
        {
            var pathToDelete = $"Assets/{folderName}";

            if (IsValidFolder(pathToDelete)) DeleteAsset(pathToDelete);
        }

        public static void CleanupDefaultFolders()
        {
            Move("_Project", "Scenes");
            Move("_Project", "Settings");
            Delete("TutorialInfo");
            Refresh();

            MoveAsset("Assets/InputSystem_Actions.inputactions",
                "Assets/_Project/Settings/InputSystem_Actions.inputactions");
            DeleteAsset("Assets/Readme.asset");
            Refresh();
        }
    }
}