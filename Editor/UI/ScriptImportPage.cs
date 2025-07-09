#if UNITY_EDITOR
using System;
using System.IO;
using Editor.Files;
using UnityEditor;
using UnityEngine;

namespace akira.UI
{
    public static class ScriptImportPage
    {
        private static string _namespaceInput = "_Project";
        private static string _scriptTemplateName;
        private static string _scriptOutputName;
        private static string _scriptDisplayName;
        private static Action _onClose;

        /// <summary>
        ///     Call this to prepare the page before showing it in ToolsHub.
        /// </summary>
        public static void Show(string templateName, string outputName, string displayName, Action onClose)
        {
            _scriptTemplateName = templateName;
            _scriptOutputName = outputName;
            _scriptDisplayName = displayName;
            _onClose = onClose;
            _namespaceInput = "_Project";
        }

        /// <summary>
        ///     Draws the import UI as a ToolsHub page.
        /// </summary>
        public static void Draw()
        {
            GUILayout.Space(8);
            GUILayout.Label($"Enter the namespace for the {_scriptDisplayName} script:", EditorStyles.label);

            _namespaceInput = EditorGUILayout.TextField(_namespaceInput);

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Import", GUILayout.Height(28)))
            {
                ImportScript(_namespaceInput);
                _onClose?.Invoke();
            }

            if (GUILayout.Button("Cancel", GUILayout.Height(28))) _onClose?.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        private static void ImportScript(string nameSpace)
        {
            var packageName = "com.akira.tools";

            var txtPath = Path.Combine(
                Application.dataPath,
                "../Packages",
                packageName,
                "Scripts",
                _scriptTemplateName
            );

            var outputPath = Path.Combine(
                Application.dataPath,
                "_Project",
                "_Scripts",
                "Utilities",
                _scriptOutputName
            );

            // Read template, replace namespace, and write to output
            if (File.Exists(txtPath))
            {
                ImportFile.ImportTextAsScript(txtPath, outputPath, nameSpace);
                Debug.Log($"{_scriptDisplayName} imported successfully!");
            }
            else
            {
                Debug.LogError($"Script template not found: {txtPath}");
            }
        }
    }
}
#endif