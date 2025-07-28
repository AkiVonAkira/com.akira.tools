#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using akira.Folders;
using akira.ToolsHub;
using UnityEditor;
using UnityEngine;

namespace akira.UI
{
    public class FolderCustomizationPageImpl : IToolsHubPage
    {
        // UI constants
        private const float BUTTON_HEIGHT = 28f;

        private const int MAX_RECURSION_DEPTH = 10;

        // More contrasting colors for better visibility
        private static readonly Color EnabledButtonColor = new(0.2f, 0.7f, 0.2f, 1f);
        private static readonly Color DisabledButtonColor = new(0.7f, 0.2f, 0.2f, 1f);
        private static readonly Color FolderHeaderColor = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color AddButtonColor = new(0.3f, 0.5f, 0.9f, 1f);
        private static readonly Color DeleteButtonColor = new(0.8f, 0.3f, 0.3f, 1f);
        private readonly Dictionary<string, bool> _folderEnabledStates = new(); // Track enabled/disabled state
        private readonly Dictionary<string, bool> _folderFoldoutStates = new(); // Track expanded/collapsed state
        private readonly HashSet<string> _nonRemovableFolders; // Required folders

        // Add a protection against infinite recursion
        private readonly HashSet<string> _processedFolders = new();
        private List<string> _availableFolders; // All possible folders
        private List<FolderStructurePreset> _availablePresets = new();
        private string _currentFolderStructure = "Type"; // "Type" or "Function"

        // Track which folder is currently being edited with the "+" button
        private string _currentlyEditingFolder;

        // Track delete mode
        private bool _deleteMode;
        private string _newFolderName = "";
        private string _newSubfolderName = "";
        private string _presetDescription = "";

        // New fields for preset management
        private string _presetName = "";
        private int _selectedPresetIndex = -1;

        // Track preset management UI state
        private bool _showPresetManagement;

        public FolderCustomizationPageImpl(List<string> initialFolders, HashSet<string> nonRemovableFolders,
            string structureName)
        {
            _availableFolders = new List<string>(initialFolders);
            _nonRemovableFolders = new HashSet<string>(nonRemovableFolders);
            _currentFolderStructure = structureName;

            // Initialize all folders as enabled
            foreach (var folder in _availableFolders) _folderEnabledStates[folder] = true;

            // Ensure all parent folders are initially expanded
            foreach (var folder in _availableFolders)
                if (folder.Contains("/"))
                {
                    var parentPath = folder.Substring(0, folder.LastIndexOf('/'));
                    _folderFoldoutStates[parentPath] = true;
                }

            // Load available presets
            _availablePresets = FolderStructureManager.GetAllPresets();
        }

        public string Title => "Customize Project Folders";
        public string Description => "Enable or disable folders to include in your project structure";

        public void DrawContentHeader()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Toggle folder buttons to enable/disable them", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Add delete mode toggle
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = _deleteMode ? DeleteButtonColor : Color.grey;

            if (GUILayout.Button(_deleteMode ? "Exit Delete Mode" : "Delete Mode", GUILayout.Width(120)))
                _deleteMode = !_deleteMode;
            GUI.backgroundColor = originalColor;

            EditorGUILayout.EndHorizontal();
        }

        public void DrawScrollContent()
        {
            // Reset the processed folders set at the start of drawing
            _processedFolders.Clear();

            // Create a dictionary to organize folders into a proper hierarchy
            var folderHierarchy = new Dictionary<string, List<string>>();
            var allPaths = new HashSet<string>();

            // First, ensure all parent paths are in our hierarchy
            foreach (var folder in _availableFolders)
            {
                var currentPath = "";
                var parts = folder.Split('/');

                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var newPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                    if (!allPaths.Contains(newPath))
                    {
                        allPaths.Add(newPath);

                        // Get parent path
                        var parentPath = currentPath;

                        // Add to parent's children
                        if (!folderHierarchy.ContainsKey(parentPath))
                            folderHierarchy[parentPath] = new List<string>();

                        if (!folderHierarchy[parentPath].Contains(newPath))
                            folderHierarchy[parentPath].Add(newPath);
                    }

                    currentPath = newPath;
                }
            }

            // Now draw the root folders (those with empty parent)
            if (folderHierarchy.ContainsKey(""))
                foreach (var rootFolder in folderHierarchy[""].OrderBy(f => f))
                {
                    DrawFolderGroupHeader(rootFolder);

                    if (_folderFoldoutStates.TryGetValue(rootFolder, out var isExpanded) && isExpanded)
                        DrawFolderHierarchyRecursive(rootFolder, folderHierarchy, 1);

                    EditorGUILayout.Space(10);
                }
        }

        public void DrawContentFooter()
        {
            GUILayout.Space(10);

            // Folder addition input
            EditorGUILayout.BeginHorizontal();

            // Store previous value to detect changes
            var previousFolderName = _newFolderName;
            _newFolderName = EditorGUILayout.TextField("Add New Folder:", _newFolderName);

            // Validate folder name when it changes
            if (_newFolderName != previousFolderName) _newFolderName = ValidateFolderName(_newFolderName);

            GUI.enabled = !string.IsNullOrWhiteSpace(_newFolderName) && !_availableFolders.Contains(_newFolderName);

            if (GUILayout.Button("Add", GUILayout.Width(60)))
                try
                {
                    var trimmedName = _newFolderName.Trim();

                    // Ensure all parent folders exist in the hierarchy
                    EnsureParentFoldersExist(trimmedName);

                    // Now add the actual folder
                    _availableFolders.Add(trimmedName);
                    _folderEnabledStates[trimmedName] = true;

                    // For nested folders, make sure all parent folders are expanded
                    if (trimmedName.Contains("/"))
                    {
                        var parts = trimmedName.Split('/');
                        var currentPath = "";

                        // Expand each segment of the path
                        for (var i = 0; i < parts.Length - 1; i++)
                        {
                            currentPath = string.IsNullOrEmpty(currentPath)
                                ? parts[i]
                                : $"{currentPath}/{parts[i]}";

                            _folderFoldoutStates[currentPath] = true;
                        }
                    }

                    _newFolderName = "";
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error adding folder: {ex.Message}");
                    ToolsHubManger.ShowNotification($"Error adding folder: {ex.Message}");
                }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Add preset management UI as a foldout
            GUILayout.Space(15);

            // Create a foldout header similar to the ToolsHub style
            var foldoutRect = EditorGUILayout.GetControlRect(GUILayout.Height(24));
            var bgColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            var borderColor = new Color(0.35f, 0.35f, 0.35f, 1f);

            // Draw background and border
            EditorGUI.DrawRect(foldoutRect, bgColor);
            DrawRectBorder(foldoutRect, borderColor, 1);

            // Draw foldout control
            var labelRect = new Rect(foldoutRect.x + 20, foldoutRect.y, foldoutRect.width - 20, foldoutRect.height);
            var arrowRect = new Rect(foldoutRect.x + 4, foldoutRect.y + 4, 16, 16);

            _showPresetManagement = EditorGUI.Foldout(arrowRect, _showPresetManagement, GUIContent.none, false);
            EditorGUI.LabelField(labelRect, "Preset Management", EditorStyles.boldLabel);

            // Handle click on the whole header
            var currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && foldoutRect.Contains(currentEvent.mousePosition))
            {
                _showPresetManagement = !_showPresetManagement;
                currentEvent.Use();
            }

            if (_showPresetManagement)
            {
                // Save section
                GUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Save Current Structure", EditorStyles.boldLabel);

                _presetName = EditorGUILayout.TextField("Preset Name:", _presetName);
                _presetDescription = EditorGUILayout.TextField("Description:", _presetDescription);

                GUILayout.BeginHorizontal();
                GUI.enabled = !string.IsNullOrWhiteSpace(_presetName);

                if (GUILayout.Button("Save As Preset")) SaveCurrentStructureAsPreset();

                if (GUILayout.Button("Export to File...")) ExportCurrentStructureToFile();

                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                // Load section
                GUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Load Structure", EditorStyles.boldLabel);

                // Preset dropdown
                var presetNames = _availablePresets.Select(p => p.Name).ToArray();
                EditorGUI.BeginChangeCheck();
                _selectedPresetIndex = EditorGUILayout.Popup("Select Preset:", _selectedPresetIndex, presetNames);

                // Show selected preset description
                if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _availablePresets.Count)
                    EditorGUILayout.HelpBox(_availablePresets[_selectedPresetIndex].Description, MessageType.Info);

                GUILayout.BeginHorizontal();
                GUI.enabled = _selectedPresetIndex >= 0 && _selectedPresetIndex < _availablePresets.Count;

                if (GUILayout.Button("Load Preset")) LoadSelectedPreset();

                if (GUILayout.Button("Export"))
                    if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _availablePresets.Count)
                        ExportSelectedPreset();

                // Only allow deleting non-built-in presets
                GUI.enabled = _selectedPresetIndex >= 0 && _selectedPresetIndex < _availablePresets.Count &&
                              !_availablePresets[_selectedPresetIndex].IsBuiltIn;

                if (GUILayout.Button("Delete")) DeleteSelectedPreset();

                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Import from JSON...")) ImportPresetFromFile();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }
        }

        public void DrawFooter()
        {
            GUILayout.FlexibleSpace();
            PageLayout.DrawCancelButton(100);
            GUILayout.Space(10);

            if (PageLayout.DrawActionButton("Apply Changes", 120))
            {
                ApplyCustomFolders();
                ToolsHubManger.ClosePage(PageOperationResult.Success);
            }
        }

        public void OnPageResult(PageOperationResult result)
        {
            if (result == PageOperationResult.Success)
                ToolsHubManger.ShowNotification("Folder structure updated!", "success");
        }

        // Add a helper method to check if a folder is a parent folder (has children)
        private bool IsFolderParent(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return false;

            // Check if any other folder has this folder as a parent
            return _availableFolders.Any(f =>
                f != folderPath && // Not the same folder
                (f.StartsWith(folderPath + "/") || // Direct child
                 // Handle a special case where folder isn't in the list yet
                 (f.Contains("/") && f.Substring(0, f.LastIndexOf('/')) == folderPath))
            );
        }

        private void DrawFolderHierarchyRecursive(string folderPath, Dictionary<string, List<string>> hierarchy,
            int indentLevel)
        {
            // Protection against infinite recursion
            if (indentLevel > MAX_RECURSION_DEPTH || _processedFolders.Contains(folderPath)) return;

            // Mark this folder as processed to avoid cycles
            _processedFolders.Add(folderPath);

            // If this folder has children in the hierarchy
            if (hierarchy.ContainsKey(folderPath))
                try
                {
                    // Get direct child subfolders (those without additional slashes)
                    var directChildren = hierarchy[folderPath]
                        .Where(child => child.Length > folderPath.Length + 1 &&
                                        !child.Substring(folderPath.Length + 1).Contains('/') &&
                                        // Only include leaf nodes as buttons - don't show folders that have children
                                        !IsFolderParent(child))
                        .OrderBy(child => child) // Sort alphabetically
                        .ToList();

                    // Display these folders as buttons in a grid
                    if (directChildren.Any()) DrawFolderButtons(directChildren, indentLevel);

                    // Now draw subfolder groups (those with additional hierarchy)
                    var subfolderGroups = hierarchy[folderPath]
                        .Where(child => child.Length > folderPath.Length + 1 &&
                                        (child.Substring(folderPath.Length + 1).Contains('/') || IsFolderParent(child)))
                        .Select(child =>
                        {
                            // If the folder has a slash, get the next segment
                            if (child.Substring(folderPath.Length + 1).Contains('/'))
                            {
                                var nextSlashIndex = child.IndexOf('/', folderPath.Length + 1);

                                if (nextSlashIndex > 0)
                                    return child.Substring(0, nextSlashIndex);
                            }

                            // Otherwise, this folder itself is a parent
                            return child;
                        })
                        .Distinct()
                        .ToList();

                    foreach (var subfolder in subfolderGroups.OrderBy(f => f))
                    {
                        // Extra validation to prevent issues
                        if (string.IsNullOrEmpty(subfolder) || subfolder == folderPath)
                            continue;

                        DrawFolderGroupHeader(subfolder, indentLevel);

                        if (_folderFoldoutStates.TryGetValue(subfolder, out var isExpanded) && isExpanded)
                            DrawFolderHierarchyRecursive(subfolder, hierarchy, indentLevel + 1);
                    }
                }
                catch (Exception ex)
                {
                    ToolsHubManger.ShowNotification("Error processing folder hierarchy", "error");
                    Debug.LogError($"Error processing folder hierarchy for '{folderPath}': {ex.Message}");
                }

            // Remove from processed set when done with this branch
            _processedFolders.Remove(folderPath);
        }

        private void DrawFolderGroupHeader(string folderPath, int indentLevel = 0)
        {
            // Get just the folder name for display
            var displayName = folderPath.Contains("/")
                ? folderPath.Substring(folderPath.LastIndexOf('/') + 1)
                : folderPath;

            // Create a rect for the header
            var headerRect = EditorGUILayout.GetControlRect(false, BUTTON_HEIGHT);

            // Adjust rect for indent
            headerRect.x += indentLevel * 20;
            headerRect.width -= indentLevel * 20;

            // Draw background
            EditorGUI.DrawRect(headerRect, FolderHeaderColor);

            // Draw foldout
            var isExpanded = _folderFoldoutStates.ContainsKey(folderPath) && _folderFoldoutStates[folderPath];

            var foldoutRect = new Rect(headerRect.x + 4, headerRect.y, 20, headerRect.height);
            var newExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none, false);

            if (newExpanded != isExpanded) _folderFoldoutStates[folderPath] = newExpanded;

            // Draw folder name
            var labelRect = new Rect(foldoutRect.xMax, headerRect.y, headerRect.width - 90, headerRect.height);
            EditorGUI.LabelField(labelRect, displayName, EditorStyles.boldLabel);

            // Add "+" button at the end of the header
            var addButtonRect = new Rect(headerRect.xMax - 58, headerRect.y + 4, 24, headerRect.height - 8);
            var deleteButtonRect = new Rect(headerRect.xMax - 30, headerRect.y + 4, 24, headerRect.height - 8);

            // If we're currently editing this folder, show input field instead of + button
            if (_currentlyEditingFolder == folderPath)
            {
                var availableWidth = headerRect.width - 150; // Account for the label and buttons

                // Calculate rect for the input field - make sure it's not too wide
                var inputFieldRect = new Rect(
                    headerRect.x + 150,
                    headerRect.y + 2,
                    Mathf.Min(200, availableWidth - 120), // Ensure reasonable width
                    headerRect.height - 4
                );

                // Draw input field
                GUI.SetNextControlName("NewSubfolderField");
                _newSubfolderName = EditorGUI.TextField(inputFieldRect, _newSubfolderName);

                // Focus the field
                EditorGUI.FocusTextInControl("NewSubfolderField");

                // Add confirm and cancel buttons
                var confirmRect = new Rect(inputFieldRect.xMax + 5, headerRect.y + 2, 50, headerRect.height - 4);
                var cancelRect = new Rect(confirmRect.xMax + 5, headerRect.y + 2, 50, headerRect.height - 4);

                // Ensure the cancel button is visible
                if (cancelRect.xMax > headerRect.xMax)
                {
                    var overflow = cancelRect.xMax - headerRect.xMax + 5;
                    inputFieldRect.width -= overflow;
                    confirmRect.x -= overflow;
                    cancelRect.x -= overflow;
                }

                if (GUI.Button(confirmRect, "Add"))
                {
                    // Create the new subfolder
                    if (!string.IsNullOrWhiteSpace(_newSubfolderName))
                    {
                        var newFolderPath = folderPath + "/" + _newSubfolderName.Trim();

                        if (!_availableFolders.Contains(newFolderPath))
                        {
                            _availableFolders.Add(newFolderPath);
                            _folderEnabledStates[newFolderPath] = true;
                        }
                    }

                    _currentlyEditingFolder = null;
                    _newSubfolderName = "";
                }

                if (GUI.Button(cancelRect, "Cancel"))
                {
                    _currentlyEditingFolder = null;
                    _newSubfolderName = "";
                }

                // Also check for Enter key
                if (Event.current.type == EventType.KeyDown &&
                    Event.current.keyCode == KeyCode.Return)
                {
                    // Create the new subfolder
                    if (!string.IsNullOrWhiteSpace(_newSubfolderName))
                    {
                        var newFolderPath = folderPath + "/" + _newSubfolderName.Trim();

                        if (!_availableFolders.Contains(newFolderPath))
                        {
                            _availableFolders.Add(newFolderPath);
                            _folderEnabledStates[newFolderPath] = true;
                        }
                    }

                    _currentlyEditingFolder = null;
                    _newSubfolderName = "";
                    Event.current.Use();
                }

                // Also check for Escape key
                if (Event.current.type == EventType.KeyDown &&
                    Event.current.keyCode == KeyCode.Escape)
                {
                    _currentlyEditingFolder = null;
                    _newSubfolderName = "";
                    Event.current.Use();
                }
            }
            else
            {
                // Draw the + button
                var originalBg = GUI.backgroundColor;
                GUI.backgroundColor = AddButtonColor;

                if (GUI.Button(addButtonRect, "+"))
                {
                    _currentlyEditingFolder = folderPath;
                    _newSubfolderName = "";
                    GUI.FocusControl(null);
                }

                // Draw the delete button if in delete mode
                GUI.backgroundColor = DeleteButtonColor;

                if (GUI.Button(deleteButtonRect, "×"))
                {
                    if (_deleteMode)
                        // Delete this folder and its subfolders
                        DeleteFolderAndSubfolders(folderPath);
                    else
                        // If not in delete mode, give a warning message
                        EditorUtility.DisplayDialog("Delete Mode Disabled",
                            "Enable 'Delete Mode' from the top button to delete folders.", "OK");
                }

                GUI.backgroundColor = originalBg;
            }

            // Check for clicks on the header (except foldout arrow and buttons)
            if (Event.current.type == EventType.MouseDown &&
                labelRect.Contains(Event.current.mousePosition) &&
                !addButtonRect.Contains(Event.current.mousePosition) &&
                !deleteButtonRect.Contains(Event.current.mousePosition))
            {
                _folderFoldoutStates[folderPath] = !isExpanded;
                Event.current.Use();
            }
        }

        private void DrawFolderButtons(List<string> folders, int indentLevel = 0)
        {
            if (folders == null || folders.Count == 0)
                return;

            // Sort the folders alphabetically before displaying
            folders = folders.OrderBy(f => f).ToList();

            // Start with indentation
            GUILayout.BeginHorizontal();
            GUILayout.Space(20 + indentLevel * 20);

            // Use a vertical layout to contain all wrapped buttons
            GUILayout.BeginVertical();

            // Begin a horizontal group that will wrap automatically
            GUILayout.BeginHorizontal();
            float currentLineWidth = 0;

            var availableWidth =
                EditorGUIUtility.currentViewWidth - (40 + indentLevel * 20); // Account for margins and indentation

            foreach (var folderPath in folders)
            {
                var displayName = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
                var isEnabled = _folderEnabledStates.ContainsKey(folderPath) && _folderEnabledStates[folderPath];
                var isRequired = _nonRemovableFolders.Contains(folderPath);

                // Calculate button width based on text
                var buttonWidth = Mathf.Max(100, GUI.skin.button.CalcSize(new GUIContent(displayName)).x + 20);

                // Check if we need to wrap to a new line
                if (currentLineWidth > 0 && currentLineWidth + buttonWidth > availableWidth)
                {
                    // End current line and start a new one
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2); // Space between rows
                    GUILayout.BeginHorizontal();
                    currentLineWidth = 0;
                }

                // Different behavior based on mode
                if (_deleteMode)
                {
                    // In delete mode, show a delete button instead of toggle
                    var canDelete = !isRequired;
                    GUI.backgroundColor = canDelete ? DeleteButtonColor : Color.gray;

                    EditorGUI.BeginDisabledGroup(!canDelete);

                    if (GUILayout.Button($"Delete {displayName}", GUILayout.Height(BUTTON_HEIGHT),
                            GUILayout.MinWidth(buttonWidth)))
                        if (canDelete)
                        {
                            // Remove the folder
                            _availableFolders.Remove(folderPath);
                            _folderEnabledStates.Remove(folderPath);
                        }

                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    // Normal mode - toggle enabled/disabled
                    GUI.backgroundColor = isEnabled ? EnabledButtonColor : DisabledButtonColor;

                    // Create the button
                    EditorGUI.BeginDisabledGroup(isRequired); // Can't disable required folders

                    if (GUILayout.Button(displayName, GUILayout.Height(BUTTON_HEIGHT), GUILayout.MinWidth(buttonWidth)))
                        if (!isRequired)
                            _folderEnabledStates[folderPath] = !isEnabled;

                    EditorGUI.EndDisabledGroup();
                }

                // Update the line width and add spacing between buttons
                currentLineWidth += buttonWidth + 5; // 5 is the spacing between buttons
                GUILayout.Space(5);
            }

            // Close the layout groups
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // Reset color
            GUI.backgroundColor = Color.white;
        }

        // Helper method to draw borders around rectangles
        private void DrawRectBorder(Rect rect, Color color, int thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        }

        private void ExportCurrentStructureToFile()
        {
            if (string.IsNullOrWhiteSpace(_presetName))
            {
                EditorUtility.DisplayDialog("Export Error", "Please enter a name for the preset before exporting.",
                    "OK");

                return;
            }

            // Create preset
            var preset = new FolderStructurePreset
            {
                Name = _presetName, Description = _presetDescription, Folders = new List<string>(_availableFolders)
            };

            // Save enabled states
            foreach (var folder in _availableFolders)
                preset.EnabledState[folder] = _folderEnabledStates.ContainsKey(folder) && _folderEnabledStates[folder];

            // Show save dialog
            var presetsDir = FolderStructureManager.GetPresetsDirectory();

            var filePath = EditorUtility.SaveFilePanel(
                "Export Folder Structure",
                presetsDir,
                _presetName + ".json",
                "json"
            );

            if (!string.IsNullOrEmpty(filePath))
            {
                FolderStructureManager.ExportPresetToFile(preset, filePath);
                ToolsHubManger.ShowNotification($"Preset exported to {filePath}", "success");
            }
        }

        private void ExportSelectedPreset()
        {
            if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _availablePresets.Count)
                return;

            var preset = _availablePresets[_selectedPresetIndex];

            // Show save dialog
            var presetsDir = FolderStructureManager.GetPresetsDirectory();

            var filePath = EditorUtility.SaveFilePanel(
                "Export Folder Structure",
                presetsDir,
                preset.Name + ".json",
                "json"
            );

            if (!string.IsNullOrEmpty(filePath))
            {
                FolderStructureManager.ExportPresetToFile(preset, filePath);
                ToolsHubManger.ShowNotification($"Preset '{preset.Name}' exported successfully.", "success");
            }
        }

        private void ImportPresetFromFile()
        {
            var presetsDir = FolderStructureManager.GetPresetsDirectory();

            var filePath = EditorUtility.OpenFilePanel(
                "Import Folder Structure",
                presetsDir,
                "json"
            );

            if (string.IsNullOrEmpty(filePath))
                return;

            var preset = FolderStructureManager.ImportPresetFromFile(filePath);

            if (preset != null)
            {
                // Ask if user wants to save it to internal presets
                if (EditorUtility.DisplayDialog("Import Preset",
                        $"Do you want to save '{preset.Name}' to your presets?",
                        "Yes", "No"))
                {
                    FolderStructureManager.SavePreset(preset);

                    // Refresh presets list
                    _availablePresets = FolderStructureManager.GetAllPresets();

                    // Update selected index to point to the imported preset
                    _selectedPresetIndex = _availablePresets.FindIndex(p => p.Name == preset.Name);
                }

                // Load the preset into the UI
                _availableFolders = new List<string>(preset.Folders);
                _folderEnabledStates.Clear();

                // Load enabled states
                foreach (var folder in preset.Folders)
                {
                    var isEnabled = preset.EnabledState.ContainsKey(folder) ? preset.EnabledState[folder] : true;
                    _folderEnabledStates[folder] = isEnabled;
                }

                // Update preset name and description fields
                _presetName = preset.Name;
                _presetDescription = preset.Description;

                ToolsHubManger.ShowNotification($"Preset '{preset.Name}' imported successfully.", "success");
            }
        }

        private void DeleteSelectedPreset()
        {
            if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _availablePresets.Count)
                return;

            var preset = _availablePresets[_selectedPresetIndex];

            // Don't allow deleting built-in presets
            if (preset.IsBuiltIn)
            {
                EditorUtility.DisplayDialog("Cannot Delete", "Built-in presets cannot be deleted.", "OK");

                return;
            }

            // Confirm deletion
            if (EditorUtility.DisplayDialog("Delete Preset",
                    $"Are you sure you want to delete preset '{preset.Name}'?",
                    "Yes", "No"))
            {
                FolderStructureManager.DeletePreset(preset.Name);

                // Refresh presets list
                _availablePresets = FolderStructureManager.GetAllPresets();

                // Reset selection
                _selectedPresetIndex = -1;

                ToolsHubManger.ShowNotification($"Preset '{preset.Name}' deleted.", "success");
            }
        }

        private void LoadSelectedPreset()
        {
            if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _availablePresets.Count)
                return;

            var preset = _availablePresets[_selectedPresetIndex];

            // Update the UI to reflect the loaded preset
            _availableFolders = new List<string>(preset.Folders);
            _folderEnabledStates.Clear();

            // Load enabled states
            foreach (var folder in preset.Folders)
            {
                var isEnabled = preset.EnabledState.ContainsKey(folder) ? preset.EnabledState[folder] : true;
                _folderEnabledStates[folder] = isEnabled;
            }

            // Update preset name and description fields
            _presetName = preset.Name;
            _presetDescription = preset.Description;

            // Update structure type if applicable
            if (preset.Name.Contains("Type"))
                _currentFolderStructure = "Type";
            else if (preset.Name.Contains("Function"))
                _currentFolderStructure = "Function";

            // Ensure parent folders are expanded
            foreach (var folder in _availableFolders)
                if (folder.Contains("/"))
                {
                    var parentPath = folder.Substring(0, folder.LastIndexOf('/'));
                    _folderFoldoutStates[parentPath] = true;
                }

            ToolsHubManger.ShowNotification($"Preset '{preset.Name}' loaded.", "success");
        }

        private void SaveCurrentStructureAsPreset()
        {
            if (string.IsNullOrWhiteSpace(_presetName))
                return;

            var preset = new FolderStructurePreset
            {
                Name = _presetName, Description = _presetDescription, Folders = new List<string>(_availableFolders)
            };

            // Save enabled states
            foreach (var folder in _availableFolders)
                preset.EnabledState[folder] = _folderEnabledStates.ContainsKey(folder) && _folderEnabledStates[folder];

            // Save preset
            FolderStructureManager.SavePreset(preset);

            // Refresh presets list
            _availablePresets = FolderStructureManager.GetAllPresets();

            // Update selected index to point to the new preset
            _selectedPresetIndex = _availablePresets.FindIndex(p => p.Name == _presetName);

            ToolsHubManger.ShowNotification($"Preset '{_presetName}' saved successfully.", "success");
        }

        private void ApplyCustomFolders()
        {
            // Get only enabled folders
            var enabledFolders = new List<string>();

            foreach (var folder in _availableFolders)
                if (_folderEnabledStates.TryGetValue(folder, out var isEnabled) && isEnabled)
                    enabledFolders.Add(folder);
                else if (_nonRemovableFolders.Contains(folder))
                    // Always include required folders
                    enabledFolders.Add(folder);

            FolderHelpers.CreateFolders(ToolsMenu.RootFolder, enabledFolders.ToArray());
        }

        // Helper method to remove a folder and all its subfolders
        private void DeleteFolderAndSubfolders(string folderPath)
        {
            // Can't delete required folders
            if (_nonRemovableFolders.Contains(folderPath))
            {
                EditorUtility.DisplayDialog("Cannot Delete",
                    $"The folder '{folderPath}' is required and cannot be deleted.", "OK");

                return;
            }

            // Ask for confirmation
            var confirm = EditorUtility.DisplayDialog("Delete Folder",
                $"Are you sure you want to delete '{folderPath}' and all its subfolders?",
                "Yes, Delete", "Cancel");

            if (!confirm) return;

            // Find all folders to delete
            var foldersToDelete = _availableFolders
                .Where(f => f == folderPath || f.StartsWith(folderPath + "/"))
                .ToList();

            // Remove them from available folders
            foreach (var folder in foldersToDelete)
            {
                _availableFolders.Remove(folder);
                _folderEnabledStates.Remove(folder);
            }
        }

        // Ensure parent folders exist when adding a new nested folder
        private void EnsureParentFoldersExist(string folderPath)
        {
            if (!folderPath.Contains("/"))
                return;

            var parentPath = folderPath.Substring(0, folderPath.LastIndexOf('/'));

            if (!string.IsNullOrEmpty(parentPath) && !_availableFolders.Contains(parentPath))
            {
                // Recursively ensure all ancestor folders exist
                EnsureParentFoldersExist(parentPath);

                // Add the parent folder
                _availableFolders.Add(parentPath);
                _folderEnabledStates[parentPath] = true;

                // Make sure the parent folder is expanded
                _folderFoldoutStates[parentPath] = true;
            }
        }

        // Validate folder name to prevent problematic characters or formats
        private string ValidateFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
                return folderName;

            // Replace invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            foreach (var c in invalidChars) folderName = folderName.Replace(c.ToString(), "");

            // Normalize slashes
            folderName = folderName.Replace('\\', '/');

            // Remove multiple consecutive slashes
            while (folderName.Contains("//")) folderName = folderName.Replace("//", "/");

            // Remove leading and trailing slashes
            return folderName.Trim('/');
        }
    }

    // FolderTreeNode helper class
    public class FolderTreeNode
    {
        public Dictionary<string, FolderTreeNode> Children = new();
        public string FullPath;
        public bool IsLeaf;
        public string Name;
        public FolderTreeNode Parent;
    }

    public static class FolderCustomizationPage
    {
        private static FolderCustomizationPageImpl _currentPageImpl;

        [MenuButtonItem("Setup/Folders", "Customize...", "Customize which folders are created", true)]
        public static void CustomizeFolders()
        {
            var structure = ToolsMenu.SelectedFolderStructure;
            var initialFolders = new List<string>(FolderStructures.DefaultStructures[structure]);

            if (!initialFolders.Contains("_Scripts"))
                initialFolders.Insert(0, "_Scripts");
            var nonRemovable = new HashSet<string> { "_Project", "_Scripts", "_Scripts/Utilities" };

            ShowFolderCustomizationPage(initialFolders, nonRemovable, structure);
        }

        public static void ShowFolderCustomizationPage(List<string> initialFolders, HashSet<string> nonRemovableFolders,
            string structureName = "Type")
        {
            _currentPageImpl = new FolderCustomizationPageImpl(initialFolders, nonRemovableFolders, structureName);
            _currentPageImpl.ShowInToolsHub();
        }

        // This method is needed for backward compatibility with the ToolsHub menu system
        public static void DrawFolderCustomizationPage()
        {
            if (_currentPageImpl != null)
            {
                _currentPageImpl.DrawPage();
            }
            else
            {
                _currentPageImpl = new FolderCustomizationPageImpl(
                    new List<string>(FolderStructures.DefaultStructures[ToolsMenu.SelectedFolderStructure]),
                    new HashSet<string> { "_Project", "_Scripts", "_Scripts/Utilities" },
                    ToolsMenu.SelectedFolderStructure
                );
                _currentPageImpl.DrawPage();
            }
        }

        // This method is needed for the ToolsHub menu system to find the Draw method
        public static void CustomizeFolders_Page()
        {
            DrawFolderCustomizationPage();
        }
    }
}
#endif