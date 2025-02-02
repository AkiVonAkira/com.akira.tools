using System.IO;
#if UNITY_EDITOR
using UnityEditor;

namespace akira
{
    public static class ImportFile
    {
        public static void ImportTextAsScript(
            string txtPath,
            string outputPath,
            string nameSpace = "akira"
        )
        {
            string content = File.ReadAllText(txtPath);
            content = content.Replace("#ROOTNAMESPACEBEGIN#", $"namespace {nameSpace}");
            content = content.Replace("#ROOTNAMESPACEND#", "}");

            File.WriteAllText(outputPath, content);
            AssetDatabase.Refresh();
        }
    }
}
#endif
