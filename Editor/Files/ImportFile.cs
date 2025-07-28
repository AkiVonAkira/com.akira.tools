#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace Editor.Files
{
    public static class ImportFile
    {
        public static void ImportTextAsScript(
            string txtPath,
            string outputPath,
            string nameSpace = "akira"
        )
        {
            var content = File.ReadAllText(txtPath);

            if (outputPath.EndsWith(".asmdef"))
            {
                content = content.Replace("#ROOTNAMESPACE#", nameSpace);
                content = content.Replace("#SCRIPTNAME#", Path.GetFileNameWithoutExtension(outputPath));
            }
            else
            {
                content = content.Replace("#ROOTNAMESPACEBEGIN#", $"namespace {nameSpace}");
                content = content.Replace("#ROOTNAMESPACEND#", "}");
                content = content.Replace("#SCRIPTNAME#", Path.GetFileNameWithoutExtension(outputPath));
            }

            File.WriteAllText(outputPath, content);
            AssetDatabase.Refresh();
        }
    }
}
#endif