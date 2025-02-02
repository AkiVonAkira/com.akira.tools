using System.IO;
using akira;

namespace ringo
{
    public static class WorldObjectFolders
    {
        public static void Create(string root, params string[] dirs)
        {
            string[] subFolders = { "Materials", "Models", "Prefabs", "Textures", "Animations" };

            foreach (var dir in dirs)
            {
                string dirSeparated = dir.Replace('>', Path.DirectorySeparatorChar);

                string fullPath = Path.Combine(root, dirSeparated);
                Folders.CreateDirectories(fullPath, subFolders);
            }
        }
    }
}