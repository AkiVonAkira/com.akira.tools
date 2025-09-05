#if UNITY_EDITOR
using System;
using System.IO;
using akira.ToolsHub;
using Editor.Files;
using UnityEditor;
using UnityEngine;

namespace akira.UI
{
    public class ScriptImportPageImpl : IToolsHubPage
    {
        // UI constants
        private static readonly Color PreviewBackgroundColor = new(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color ButtonColor = new(0.3f, 0.6f, 0.9f, 1f);

        // Assembly definition specific fields
        private readonly string[] _asmdefLocations =
        {
            "_Scripts",
            "_Scripts/Editor",
            "_Scripts/Runtime",
            "_Scripts/Tests",
            "_Scripts/Tests/EditMode",
            "_Scripts/Tests/PlayMode"
        };

        private readonly string _displayName;
        private readonly string _menuPath;
        private readonly string _templateName;

        private string _className;

        // Add this field to track file comparison results
        private bool? _fileHasDifferences;

        // User inputs
        private string _namespace;
        private string _outputName;
        private string _outputPath;
        private string _previewContent;
        private int _selectedAsmdefLocationIndex;

        // Content
        private string _templateContent;
        private string _templatePath;

        public ScriptImportPageImpl(string templateName, string outputName, string displayName, string menuPath)
        {
            _templateName = templateName;
            _outputName = outputName;
            _displayName = displayName;
            _menuPath = menuPath;

            // Initialize class name from output name
            _className = Path.GetFileNameWithoutExtension(_outputName);

            // Initialize namespace
#if UNITY_2020_2_OR_NEWER
            _namespace = !string.IsNullOrEmpty(EditorSettings.projectGenerationRootNamespace)
                ? EditorSettings.projectGenerationRootNamespace
                : "Scripts";
#else
            _namespace = "Scripts";
#endif

            // Set default asmdef location index based on output name
            if (_outputName.EndsWith(".asmdef"))
            {
                if (_outputName.Contains("Editor"))
                {
                    // If it's an editor assembly, default to the Editor folder
                    _selectedAsmdefLocationIndex = Array.IndexOf(_asmdefLocations, "_Scripts/Editor");
                    if (_selectedAsmdefLocationIndex < 0) _selectedAsmdefLocationIndex = 0;
                }
                else if (_outputName.Contains("Test"))
                {
                    // If it's a test assembly, default to the Tests folder
                    _selectedAsmdefLocationIndex = Array.IndexOf(_asmdefLocations, "_Scripts/Tests");
                    if (_selectedAsmdefLocationIndex < 0) _selectedAsmdefLocationIndex = 0;
                }
            }

            // Initialize path first
            _outputPath = GetDefaultScriptPath(_outputName, _menuPath);

            // Load template content
            LoadTemplateContent();
            UpdatePreview();
        }

        public string Title => $"Import {_displayName} Script";
        public string Description => "Import script template to your project";

        public void DrawContentHeader()
        {
            GUILayout.Space(5);

            // Class Name Field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Class Name:", GUILayout.Width(80));
            var newClassName = EditorGUILayout.TextField(_className ?? "");

            if (newClassName != _className)
            {
                _className = newClassName;
                _outputName = _className + Path.GetExtension(_outputName);

                // Update output path with new file name
                var directory = Path.GetDirectoryName(_outputPath);

                if (directory != null)
                    _outputPath = Path.Combine(directory, _outputName).Replace('\\', '/');

                UpdatePreview();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Namespace field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Namespace:", GUILayout.Width(80));
            var newNamespace = EditorGUILayout.TextField(_namespace ?? "");

            if (newNamespace != _namespace)
            {
                _namespace = newNamespace;
                UpdatePreview();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // For asmdef files, add location dropdown
            if (_outputName.EndsWith(".asmdef"))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Location:", GUILayout.Width(80));
                var newLocationIndex = EditorGUILayout.Popup(_selectedAsmdefLocationIndex, _asmdefLocations);

                if (newLocationIndex != _selectedAsmdefLocationIndex)
                {
                    _selectedAsmdefLocationIndex = newLocationIndex;
                    // Update output path with the new location
                    _outputPath = GetDefaultScriptPath(_outputName, _menuPath);
                    UpdatePreview();
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);
            }

            // Output path with browse button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Path:", GUILayout.Width(80));

            var displayPath = _outputPath ?? "";
            var assetsIdx = displayPath.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx < 0) assetsIdx = displayPath.IndexOf("Assets\\", StringComparison.OrdinalIgnoreCase);

            if (assetsIdx >= 0)
                displayPath = displayPath.Substring(assetsIdx).Replace('\\', '/');
            else
                displayPath = displayPath.Replace('\\', '/');

            EditorGUI.BeginChangeCheck();
            var newDisplayPath = EditorGUILayout.TextField(displayPath);

            if (EditorGUI.EndChangeCheck() && newDisplayPath != displayPath)
            {
                _outputPath = newDisplayPath.Replace('\\', '/');
                UpdatePreview();
            }

            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = ButtonColor;

            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var dir = Path.GetDirectoryName(_outputPath ?? "");
                var extension = Path.GetExtension(_outputName);

                if (string.IsNullOrEmpty(extension)) extension = "cs";
                else extension = extension.TrimStart('.');

                var selected = EditorUtility.SaveFilePanel("Select Output Path", dir, _outputName, extension);

                if (!string.IsNullOrEmpty(selected))
                {
                    _outputPath = selected.Replace('\\', '/');
                    // Update class name from selected file
                    _className = Path.GetFileNameWithoutExtension(_outputPath);
                    _outputName = Path.GetFileName(_outputPath);
                    UpdatePreview();
                }
            }

            GUI.backgroundColor = originalBg;

            EditorGUILayout.EndHorizontal();
        }

        public void DrawScrollContent()
        {
            // Use SelectableLabel for read-only preview with scrolling
            var previewStyle = new GUIStyle(EditorStyles.label);
            previewStyle.richText = true;
            previewStyle.wordWrap = true;
            previewStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

            var previewContent = _previewContent ?? "// No preview available";

            EditorGUILayout.LabelField(previewContent, previewStyle,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }

        public void DrawContentFooter()
        {
            // Show warnings if needed
            if (string.IsNullOrEmpty(_className) || _className.Contains(" "))
                EditorGUILayout.HelpBox("Please enter a valid class name (no spaces).", MessageType.Warning);

            if (string.IsNullOrEmpty(_outputPath))
            {
                EditorGUILayout.HelpBox("Please specify an output path.", MessageType.Warning);
            }
            else if (File.Exists(_outputPath) && !_outputPath.EndsWith(".meta"))
            {
                // Check for differences in file content
                if (_fileHasDifferences == null) _fileHasDifferences = HasFileDifferences(_outputPath);

                if (_fileHasDifferences.Value)
                    EditorGUILayout.HelpBox("File already exists. Importing will overwrite with different content.",
                        MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("File already exists with identical content. No changes will be made.",
                        MessageType.Info);
            }

            // Check for valid extensions
            var isValidExtension = _outputPath.ToLower().EndsWith(".cs") || _outputPath.ToLower().EndsWith(".asmdef");

            if (!isValidExtension)
                EditorGUILayout.HelpBox("Output path should end with .cs or .asmdef extension.", MessageType.Warning);
        }

        public void DrawFooter()
        {
            // Disable the button if validation fails
            var isValidExtension = _outputPath.ToLower().EndsWith(".cs") || _outputPath.ToLower().EndsWith(".asmdef");
            var isValid = !string.IsNullOrEmpty(_className) &&
                          !string.IsNullOrEmpty(_outputPath) &&
                          isValidExtension &&
                          !_className.Contains(" ");

            var left = new System.Collections.Generic.List<PageLayout.FooterButton>
            {
                new PageLayout.FooterButton
                {
                    Label = "Cancel",
                    Style = PageLayout.FooterButtonStyle.Secondary,
                    Enabled = true,
                    OnClick = () => ToolsHubManager.ClosePage(PageOperationResult.Cancelled),
                    MinWidth = 100
                }
            };

            var right = new System.Collections.Generic.List<PageLayout.FooterButton>
            {
                new PageLayout.FooterButton
                {
                    Label = "Import Script",
                    Style = PageLayout.FooterButtonStyle.Primary,
                    Enabled = isValid,
                    OnClick = () =>
                    {
                        if (isValid)
                        {
                            var success = ImportScript(_outputPath, _namespace);
                            ToolsHubManager.ClosePage(success ? PageOperationResult.Success : PageOperationResult.Failure);
                        }
                        else
                        {
                            ToolsHubManager.ClosePage(PageOperationResult.Failure);
                        }
                    },
                    MinWidth = 120
                }
            };

            PageLayout.DrawFooterSplit(left, right);
        }

        // Update the OnPageResult to give more detailed information
        public void OnPageResult(PageOperationResult result)
        {
            if (result == PageOperationResult.Success)
            {
                if (File.Exists(_outputPath) && _fileHasDifferences.HasValue && !_fileHasDifferences.Value)
                    ToolsHubManager.ShowNotification(
                        $"File '{_className}' already exists with identical content. No changes made.");
                else
                    ToolsHubManager.ShowNotification($"Script '{_className}' imported successfully at {_outputPath}",
                        "success");
            }
            else if (result == PageOperationResult.Failure)
            {
                ToolsHubManager.ShowNotification(
                    $"Failed to import script '{_className}'. Please check the console for errors.", "error");
            }
        }

        // Helper to construct the correct script path using _Project/_Scripts structure
        private string GetDefaultScriptPath(string fileName, string categoryPath)
        {
            // First ensure the fileName isn't empty
            if (string.IsNullOrEmpty(fileName))
                fileName = "NewScript.cs";

            var assetsPath = Application.dataPath; // Path to the Assets folder

            // For .asmdef files, use the selected location
            if (fileName.EndsWith(".asmdef"))
            {
                var targetLocation = _asmdefLocations[_selectedAsmdefLocationIndex];
                var scriptPath = Path.Combine(assetsPath, "_Project", targetLocation, fileName);

                return scriptPath.Replace('\\', '/');
            }
            else
            {
                // Parse the category path to get the appropriate subfolder for regular scripts
                var subfolder = "Utilities"; // Default

                if (!string.IsNullOrEmpty(categoryPath))
                {
                    var parts = categoryPath.Split('/');

                    if (parts.Length > 0)
                        subfolder = parts[parts.Length - 1];
                }

                // For normal scripts, use the category subfolder
                var scriptPath = Path.Combine(assetsPath, "_Project", "_Scripts", subfolder, fileName);

                return scriptPath.Replace('\\', '/');
            }
        }

        private void LoadTemplateContent()
        {
            // Find the template in the package directory
            var templatesDir = Path.Combine(Application.dataPath, "../Packages/com.akira.tools/Scripts");
            _templatePath = Path.Combine(templatesDir, _templateName);

            if (File.Exists(_templatePath))
            {
                _templateContent = File.ReadAllText(_templatePath);
            }
            else
            {
                ToolsHubManager.ShowNotification($"Template file not found: {_templateName}", "error");
                Debug.LogWarning($"Template file not found: {_templatePath}");
                _templateContent = $"// Template file not found at: {_templatePath}";
            }
        }

        // Add a helper method to check if there are differences between the file to be created and an existing file
        private bool HasFileDifferences(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return true;

                // Read the existing file content
                var existingContent = File.ReadAllText(filePath);

                // Generate the content that would be created
                var newContent = _templateContent;

                if (filePath.EndsWith(".asmdef"))
                    newContent = newContent
                        .Replace("#ROOTNAMESPACE#", _namespace)
                        .Replace("#SCRIPTNAME#", _className);
                else
                    newContent = newContent
                        .Replace("#ROOTNAMESPACEBEGIN#", $"namespace {_namespace}")
                        .Replace("#ROOTNAMESPACEND#", "}")
                        .Replace("#SCRIPTNAME#", _className);

                // Normalize line endings for comparison
                existingContent = NormalizeLineEndings(existingContent);
                newContent = NormalizeLineEndings(newContent);

                // Compare content
                return existingContent != newContent;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error comparing files: {ex.Message}");

                return true; // Assume different if error occurs
            }
        }

        // Helper to normalize line endings for consistent comparison
        private string NormalizeLineEndings(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            // Replace all line endings with \n
            return content.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // Reset file difference check when the output path changes
        private void UpdatePreview()
        {
            // Reset file difference check
            _fileHasDifferences = null;

            if (string.IsNullOrEmpty(_templateContent) || _templateContent.StartsWith("// Template"))
            {
                _previewContent = _templateContent;

                return;
            }

            // Apply different template transformations based on file type
            if (_outputPath.EndsWith(".asmdef"))
                _previewContent = _templateContent
                    .Replace("#ROOTNAMESPACE#", _namespace)
                    .Replace("#SCRIPTNAME#", _className);
            else if (_outputPath.EndsWith(".cs"))
                _previewContent = _templateContent
                    .Replace("#ROOTNAMESPACEBEGIN#", $"namespace {_namespace}")
                    .Replace("#ROOTNAMESPACEND#", "}")
                    .Replace("#SCRIPTNAME#", _className);
        }

        private bool ImportScript(string outputPath, string nameSpace)
        {
            try
            {
                if (!File.Exists(_templatePath))
                {
                    ToolsHubManager.ShowNotification($"Script template not found: {_templateName}", "error");
                    Debug.LogError($"Script template not found: {_templatePath}");

                    return false;
                }

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(outputPath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                ImportFile.ImportTextAsScript(_templatePath, outputPath, nameSpace);
                AssetDatabase.Refresh();

                return true;
            }
            catch (Exception ex)
            {
                ToolsHubManager.ShowNotification($"Error importing script: {_className}", "error");
                Debug.LogError($"Error importing script: {ex.Message}");

                return false;
            }
        }
    }

    public static class ScriptImportPage
    {
        // Keep a reference to the current page implementation for state persistence
        private static ScriptImportPageImpl _currentPageImpl;

        [MenuButtonItem("Scripts/Utilities", "Singleton", "Import Singleton script", true)]
        public static void ImportSingleton()
        {
            ShowScriptImportPage("Singleton.txt", "Singleton.cs", "Singleton", "Scripts/Utilities");
        }

        [MenuButtonItem("Scripts", "Assembly Definition", "Import Project Assembly Definition", true)]
        public static void ImportProjectAssemblyDefinition()
        {
            ShowScriptImportPage("ProjectAsmdef.txt", "_Project.asmdef", "Project Assembly Definition", "Scripts");
        }

        [MenuButtonItem("Scripts", "Editor Assembly Definition", "Import Editor Assembly Definition", true)]
        public static void ImportEditorAssemblyDefinition()
        {
            ShowScriptImportPage("EditorAsmdef.txt", "_Project.Editor.asmdef", "Editor Assembly Definition", "Scripts");
        }

        public static void ShowScriptImportPage(string templateName, string outputName, string displayName,
            string menuPath = null)
        {
            _currentPageImpl = new ScriptImportPageImpl(templateName, outputName, displayName, menuPath);
            _currentPageImpl.ShowInToolsHub();
        }

        // Each _Page method now creates a new instance specific to that template
        // rather than trying to reuse a potentially mismatched cached instance

        public static void ImportSingleton_Page()
        {
            // Always create a fresh instance for this specific template
            _currentPageImpl =
                new ScriptImportPageImpl("Singleton.txt", "Singleton.cs", "Singleton", "Scripts/Utilities");
            _currentPageImpl.DrawPage();
        }

        public static void ImportProjectAssemblyDefinition_Page()
        {
            // Always create a fresh instance for this specific template
            _currentPageImpl = new ScriptImportPageImpl("ProjectAsmdef.txt", "_Project.asmdef",
                "Project Assembly Definition", "Scripts");
            _currentPageImpl.DrawPage();
        }

        public static void ImportEditorAssemblyDefinition_Page()
        {
            // Always create a fresh instance for this specific template
            _currentPageImpl = new ScriptImportPageImpl("EditorAsmdef.txt", "_Project.Editor.asmdef",
                "Editor Assembly Definition", "Scripts");
            _currentPageImpl.DrawPage();
        }
    }
}
#endif