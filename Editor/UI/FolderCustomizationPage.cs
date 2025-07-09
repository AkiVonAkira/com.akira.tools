#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using akira.Folders;
using UnityEditor;
using UnityEngine;

namespace akira.UI
{
    public static class FolderCustomizationPage
    {
        private static List<string> _customFolders = new();
        private static HashSet<string> _nonRemovableFolders = new();
        private static string _newFolderName = "";
        private static Vector2 _folderScrollPos;
        private static readonly Dictionary<string, bool> _folderFoldoutStates = new();
        private static string _currentFolderStructure = "Type"; // "Type" or "Function"
        private static ToolsHub _window;

        public static void Show(List<string> initialFolders, HashSet<string> nonRemovableFolders, string structureName,
            ToolsHub window)
        {
            _customFolders = new List<string>(initialFolders);
            _nonRemovableFolders = new HashSet<string>(nonRemovableFolders);
            _currentFolderStructure = structureName;
            _window = window;

            if (_window == null)
                _window = EditorWindow.GetWindow<ToolsHub>("Akira Tools Hub");

            var pageStackField =
                _window.GetType().GetField("_pageStack", BindingFlags.NonPublic | BindingFlags.Instance);
            var pageStack = pageStackField?.GetValue(_window) as IList;

            var currentPageIndexField = _window.GetType()
                .GetField("_currentPageIndex", BindingFlags.NonPublic | BindingFlags.Instance);

            // --- Use ToolsHub.PageState type via reflection ---
            var pageStateType = typeof(ToolsHub).GetNestedType("PageState", BindingFlags.NonPublic);
            var pageState = Activator.CreateInstance(pageStateType);
            pageStateType.GetField("Title").SetValue(pageState, "Customize Folders");
            pageStateType.GetField("DrawPage").SetValue(pageState, (Action)DrawFolderCustomizationPage);

            if (pageStack != null)
            {
                pageStack.Add(pageState);

                if (currentPageIndexField != null)
                    currentPageIndexField.SetValue(_window, pageStack.Count - 1);
            }

            _window.Repaint();
        }

        private static void DrawFolderCustomizationPage()
        {
            GUILayout.Label("Customize Project Folders", EditorStyles.boldLabel);
            GUILayout.Space(6);

            // --- Folder structure dropdown ---
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Folder Structure:", GUILayout.Width(120));
            var structureOptions = new[] { "Type-Based", "Function-Based" };
            var selectedIdx = _currentFolderStructure == "Function" ? 1 : 0;
            var newIdx = EditorGUILayout.Popup(selectedIdx, structureOptions, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            var newStructure = newIdx == 1 ? "Function" : "Type";

            if (newStructure != _currentFolderStructure)
            {
                // Switch structure and reload folders
                _currentFolderStructure = newStructure;
                ToolsMenu.SelectedFolderStructure = newStructure;
                var initialFolders = new List<string>(FolderStructures.DefaultStructures[newStructure]);

                if (!initialFolders.Contains("_Scripts"))
                    initialFolders.Insert(0, "_Scripts");
                _customFolders = initialFolders;
                _folderFoldoutStates.Clear();
                _newFolderName = "";
                GUI.FocusControl(null);

                return;
            }

            // Build folder tree structure
            var root = new FolderTreeNode { Name = "", Children = new Dictionary<string, FolderTreeNode>() };

            foreach (var path in _customFolders)
            {
                var parts = path.Split('/');
                var current = root;

                foreach (var part in parts)
                {
                    if (!current.Children.ContainsKey(part))
                        current.Children[part] = new FolderTreeNode
                        {
                            Name = part, Children = new Dictionary<string, FolderTreeNode>(), Parent = current
                        };
                    current = current.Children[part];
                }

                current.FullPath = path;
                current.IsLeaf = true;
            }

            // Only draw help box at the root/scroll area, not for every subfolder
            EditorGUILayout.HelpBox("Add, remove, and organize your project's folder structure." +
                                    // "\nRemoving a folder will also remove all its subfolders." +
                                    "\nYou can add root folders and subfolders at the same time." +
                                    "\nExample: 'Materials/Physics'", MessageType.Info);

            _folderScrollPos = EditorGUILayout.BeginScrollView(_folderScrollPos, EditorStyles.helpBox,
                GUILayout.Height(300), GUILayout.ExpandWidth(true));
            DrawFolderTreeNode(root, 0);
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            _newFolderName = EditorGUILayout.TextField(_newFolderName);
            GUI.enabled = !string.IsNullOrWhiteSpace(_newFolderName) && !_customFolders.Contains(_newFolderName);

            if (GUILayout.Button("Add Folder", GUILayout.Width(100)))
            {
                _customFolders.Add(_newFolderName.Trim());
                _newFolderName = "";
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Apply Changes", GUILayout.Height(30)))
            {
                ToolsMenu.ApplyCustomFolders(_customFolders);
                ToolsHub.ShowNotification("Folder structure updated!");
            }
        }

        // Recursive folder tree rendering
        private static void DrawFolderTreeNode(FolderTreeNode node, int indent)
        {
            foreach (var child in node.Children.Values.OrderBy(n => n.Name))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(indent * 16);

                var hasChildren = child.Children.Count > 0;
                var foldoutKey = child.FullPath ?? string.Join("/", GetPathUpTo(child));

                if (hasChildren)
                {
                    // Custom foldout header rect
                    var headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(20), GUILayout.ExpandWidth(true));
                    var arrowSize = 12f;
                    var arrowRect = new Rect(headerRect.x, headerRect.y + 4, arrowSize, arrowSize);

                    // Draw foldout arrow
                    if (!_folderFoldoutStates.ContainsKey(foldoutKey))
                        _folderFoldoutStates[foldoutKey] = true;
                    var isOpen = _folderFoldoutStates[foldoutKey];
                    EditorGUI.BeginChangeCheck();
                    isOpen = EditorGUI.Foldout(arrowRect, isOpen, GUIContent.none, false);
                    var arrowChanged = EditorGUI.EndChangeCheck();

                    var labelRect = new Rect(arrowRect.xMax + 2, headerRect.y,
                        headerRect.width - (arrowRect.xMax - headerRect.x), headerRect.height);

                    // Draw label
                    EditorGUI.LabelField(labelRect, child.Name, EditorStyles.label);

                    float buttonWidth = 60;

                    var buttonRect = new Rect(headerRect.xMax - buttonWidth, headerRect.y + 2, buttonWidth,
                        headerRect.height - 4);

                    if (child.FullPath != null && !_nonRemovableFolders.Contains(child.FullPath))
                    {
                        if (GUILayout.Button("Remove", GUILayout.Width(60)))
                        {
                            RemoveFolderAndSubfolders(child.FullPath);
                            EditorGUILayout.EndHorizontal();

                            return;
                        }
                    }
                    else if (child.FullPath != null && _nonRemovableFolders.Contains(child.FullPath))
                    {
                        var reqRect = new Rect(headerRect.xMax - buttonWidth, headerRect.y + 2, buttonWidth,
                            headerRect.height - 4);
                        GUI.Label(reqRect, "Required", EditorStyles.miniLabel);
                    }

                    // Handle click on label to toggle foldout
                    var e = Event.current;

                    if (e.type == EventType.MouseDown && labelRect.Contains(e.mousePosition))
                    {
                        isOpen = !isOpen;
                        e.Use();
                    }
                    else if (arrowChanged)
                    {
                        // already handled
                    }

                    _folderFoldoutStates[foldoutKey] = isOpen;

                    EditorGUILayout.EndHorizontal();

                    if (isOpen) DrawFolderTreeNode(child, indent + 1);
                }
                else
                {
                    // Draw label for leaf node
                    GUILayout.Label(child.Name, GUILayout.ExpandWidth(true));

                    // Remove/Required button next to label
                    if (child.FullPath != null && !_nonRemovableFolders.Contains(child.FullPath))
                    {
                        if (GUILayout.Button("Remove", GUILayout.Width(60)))
                        {
                            RemoveFolderAndSubfolders(child.FullPath);
                            EditorGUILayout.EndHorizontal();

                            return;
                        }
                    }
                    else if (child.FullPath != null && _nonRemovableFolders.Contains(child.FullPath))
                    {
                        GUILayout.Label("Required", EditorStyles.miniLabel, GUILayout.Width(60));
                    }
                    else
                    {
                        GUILayout.Space(66);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // Helper to reconstruct path for non-leaf nodes
        private static List<string> GetPathUpTo(FolderTreeNode node)
        {
            var path = new List<string>();
            var current = node;

            while (current != null && !string.IsNullOrEmpty(current.Name))
            {
                path.Insert(0, current.Name);
                current = current.Parent;
            }

            return path;
        }

        // Remove a folder and all its subfolders from _customFolders
        private static void RemoveFolderAndSubfolders(string folderPath)
        {
            _customFolders.RemoveAll(f => f == folderPath || f.StartsWith(folderPath + "/"));
        }

        // Helper class for folder tree
        private class FolderTreeNode
        {
            public Dictionary<string, FolderTreeNode> Children = new();
            public string FullPath;
            public bool IsLeaf;
            public string Name;
            public FolderTreeNode Parent;
        }
    }
}
#endif