using System.IO;

namespace akira.Folders
{
    public static class WorldObjectFolders
    {
        private static readonly string[] SubFolders = { "Materials", "Models", "Prefabs", "Textures", "Animations" };

        public static void Create(string root, params string[] dirs)
        {
            foreach (var dir in dirs)
            {
                var dirSeparated = dir.Replace('/', Path.DirectorySeparatorChar);

                var fullPath = Path.Combine(root, dirSeparated);
                FolderHelpers.CreateFolders(fullPath, SubFolders);
            }
        }
    }
}