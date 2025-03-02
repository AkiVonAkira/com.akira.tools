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
            content = content.Replace("#ROOTNAMESPACEBEGIN#", $"namespace {nameSpace}");
            content = content.Replace("#ROOTNAMESPACEND#", "}");

            File.WriteAllText(outputPath, content);
            AssetDatabase.Refresh();
        }
    }
}
#endif