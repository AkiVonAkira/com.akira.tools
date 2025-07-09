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
        private static string _templateName;
        private static string _outputName;
        private static string _displayName;
        private static string _outputPath;
        private static string _namespace;
        private static string _templateContent;
        private static string _previewContent;
        private static Action _onImport;
        private static Action _onClose;
        private static string _menuPath;
        private static Vector2 _previewScroll;

        public static void Show(string templateName, string outputName, string displayName, Action onImport,
            Action onClose, string menuPath = null)
        {
            _templateName = templateName;
            _outputName = outputName;
            _displayName = displayName;
            _onImport = onImport;
            _onClose = onClose;
            _menuPath = menuPath;

            var defaultPath = ToolsMenu.GetDefaultScriptOutputPath(_outputName, _menuPath);
            _outputPath = defaultPath.Replace('\\', '/');

#if UNITY_2020_2_OR_NEWER
            _namespace = !string.IsNullOrEmpty(EditorSettings.projectGenerationRootNamespace)
                ? EditorSettings.projectGenerationRootNamespace
                : "_Script";
#else
            _namespace = "_Script";
#endif

            var templatePath = GetTemplatePath();

            if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                _templateContent = File.ReadAllText(templatePath);
            else
                _templateContent = "// Template not found";

            UpdatePreview();
        }

        private static void UpdatePreview()
        {
            if (string.IsNullOrEmpty(_templateContent) || _templateContent == "// Template not found")
            {
                _previewContent = "// No preview available";

                return;
            }

            _previewContent = _templateContent
                .Replace("#ROOTNAMESPACEBEGIN#", $"namespace {_namespace}")
                .Replace("#ROOTNAMESPACEND#", "}")
                .Replace("#SCRIPTNAME#", Path.GetFileNameWithoutExtension(_outputName));
        }

        public static void Draw()
        {
            GUILayout.Label("Script Import", EditorStyles.boldLabel);
            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Namespace:", GUILayout.Width(80));
            var newNamespace = EditorGUILayout.TextField(_namespace);

            if (newNamespace != _namespace)
            {
                _namespace = newNamespace;
                UpdatePreview();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Path:", GUILayout.Width(80));
            var displayPath = _outputPath;
            var assetsIdx = displayPath.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx < 0) assetsIdx = displayPath.IndexOf("Assets\\", StringComparison.OrdinalIgnoreCase);

            if (assetsIdx >= 0)
                displayPath = displayPath.Substring(assetsIdx).Replace('\\', '/');
            else
                displayPath = _outputPath.Replace('\\', '/');
            var newDisplayPath = EditorGUILayout.TextField(displayPath);

            if (newDisplayPath != displayPath)
            {
                _outputPath = newDisplayPath.Replace('\\', '/');
                UpdatePreview();
            }

            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                var dir = Path.GetDirectoryName(_outputPath);
                var selected = EditorUtility.SaveFilePanel("Select Script Output Path", dir, _outputName, "cs");

                if (!string.IsNullOrEmpty(selected))
                {
                    _outputPath = selected.Replace('\\', '/');
                    UpdatePreview();
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.ExpandHeight(true));

            EditorGUILayout.SelectableLabel(_previewContent ?? "// Something went wrong. No preview available",
                EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(40))) _onClose?.Invoke();

            if (GUILayout.Button("Import Script", GUILayout.Height(40)))
                if (!string.IsNullOrEmpty(_templateName) && !string.IsNullOrEmpty(_outputPath) &&
                    !string.IsNullOrEmpty(_namespace))
                {
                    ImportScript(_outputPath, _namespace);
                    _onImport?.Invoke();
                }

            EditorGUILayout.EndHorizontal();
        }

        private static void ImportScript(string outputPath, string nameSpace)
        {
            var templatePath = GetTemplatePath();

            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                Debug.LogError("Script template not found for import.");

                return;
            }

            ImportFile.ImportTextAsScript(templatePath, outputPath, nameSpace);
            AssetDatabase.Refresh();
        }

        private static string GetTemplatePath()
        {
            return Path.Combine(Application.dataPath, "../Packages/com.akira.tools/Scripts", _templateName);
        }
    }
}
#endif