#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Editor.Files; // <-- Add this for AutoAssetPrefix
using akira.UI; // Add this for ScriptImportPage

namespace akira
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuButtonItemAttribute : Attribute
    {
        public string ButtonText;
        public string Path;
        public string Tooltip;
        public bool IsPage;

        public MenuButtonItemAttribute(string path, string buttonText, string tooltip = "", bool isPage = false)
        {
            Path = path;
            ButtonText = buttonText;
            Tooltip = tooltip;
            IsPage = isPage;
        }
    }

    public class ToolsHub : EditorWindow
    {
        // Cache for method hash to avoid unnecessary rebuilds
        private static List<MethodInfo> _cachedMethods = new();

        // Foldout state for each node path
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private readonly float _buttonHeight = 28;
        private readonly int _buttonsPerRow = 4;
        private GUIStyle _buttonStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _compactFoldoutStyle;
        private Color _foldoutBgColor = new(0.18f, 0.18f, 0.18f, 1f);
        private Color _foldoutBorderColor = new(0.35f, 0.35f, 0.35f, 1f);

        private MenuNode _rootNode;

        private Vector2 _scrollPosition;

        // Cache for foldout styles per indent level
        private readonly Dictionary<int, GUIStyle> _foldoutStyleCache = new();

        // Static textures for button backgrounds to avoid delayed hover
        private static Texture2D _normalBg;
        private static Texture2D _hoverBg;
        private static Texture2D _activeBg;

        // Page system state
        private readonly List<PageState> _pageStack = new();
        private int _currentPageIndex = -1;

        private static ToolsHub _instance;

        private Texture2D _popupIcon;

        private class PageState
        {
            public string Title;
            public Action DrawPage;
        }

        private void OnEnable()
        {
            _instance = this;
            InitializeStyles();
            RefreshMenuTree();
            _pageStack.Clear();
            _currentPageIndex = -1;
        }

        private void OnDisable()
        {
            _cachedMethods = null;
            _pageStack.Clear();
            _currentPageIndex = -1;
        }

        private void RefreshMenuTree()
        {
            _cachedMethods = null;
            BuildMenuTree(GetMenuButtonMethods());
            _pageStack.Clear();
            _currentPageIndex = -1;
        }

        private void OnGUI()
        {
            // Ensure styles are initialized before any GUI code
            InitializeStyles();

            // Toolbar with Back, Forward, Refresh
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Back button
            GUI.enabled = _currentPageIndex > -1;
            if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                if (_currentPageIndex > -1)
                {
                    _currentPageIndex--;
                    GUI.FocusControl(null);
                }
            }
            GUI.enabled = true;

            // Forward button
            GUI.enabled = _currentPageIndex < _pageStack.Count - 1;
            if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                if (_currentPageIndex < _pageStack.Count - 1)
                {
                    _currentPageIndex++;
                    GUI.FocusControl(null);
                }
            }
            GUI.enabled = true;

            GUILayout.Space(8);

            GUILayout.FlexibleSpace();

            // Refresh button
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshMenuTree();
            }
            EditorGUILayout.EndHorizontal();

            // If on a page, draw it
            if (_currentPageIndex > -1 && _currentPageIndex < _pageStack.Count)
            {
                var page = _pageStack[_currentPageIndex];
                GUILayout.Space(8);
                GUILayout.Label(page.Title, _headerStyle);
                GUILayout.Space(8);
                page.DrawPage?.Invoke();
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label("Akira Tools Hub", _headerStyle);
            GUILayout.Space(8);

            if (_rootNode == null)
            {
                EditorGUILayout.HelpBox("No menu items found, try Refreshing.", MessageType.Warning);
                return;
            }
            EditorGUILayout.HelpBox("Select tools to configure your project.", MessageType.Info);
            GUILayout.Space(4);

            // --- Asset Prefix Toggle and Recent Rename Display Count ---
            EditorGUILayout.BeginHorizontal();
            AutoAssetPrefix.Enabled = EditorGUILayout.ToggleLeft("Enable Asset Prefixing", AutoAssetPrefix.Enabled, GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Recent Renames to Show:", GUILayout.Width(150));
            int newCount = EditorGUILayout.IntField(AutoAssetPrefix.RecentRenameDisplayCount, GUILayout.Width(50));
            newCount = Mathf.Clamp(newCount, 1, 100);
            if (newCount != AutoAssetPrefix.RecentRenameDisplayCount)
                AutoAssetPrefix.RecentRenameDisplayCount = newCount;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            bool first = true;
            foreach (var node in _rootNode.Children)
            {
                if (!first)
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                DrawNode(node, 0, true);
                first = false;
            }

            EditorGUILayout.EndScrollView();

            // --- Recent Renames ---
            int displayCount = Mathf.Min(AutoAssetPrefix.RecentRenameDisplayCount, AutoAssetPrefix.RecentRenames.Count);
            if (displayCount > 0)
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField("Recent Asset Renames", EditorStyles.boldLabel);
                for (int i = 0; i < displayCount; i++)
                {
                    var entry = AutoAssetPrefix.RecentRenames[i];
                    EditorGUILayout.BeginHorizontal();
                    Texture icon = null;
                    if (!string.IsNullOrEmpty(entry.IconPath))
                    {
                        string iconAssetPath = AssetDatabase.GUIDToAssetPath(entry.IconPath);
                        if (!string.IsNullOrEmpty(iconAssetPath))
                            icon = AssetDatabase.GetCachedIcon(iconAssetPath);
                    }
                    GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));
                    GUILayout.Label($"{entry.OldName}  →  {entry.NewName}", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Ping", GUILayout.Width(36)))
                    {
                        string currentPath = RenameLogStore.GetCurrentAssetPathForRename(entry);
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(currentPath);
                        if (obj != null)
                            EditorGUIUtility.PingObject(obj);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private static List<MethodInfo> GetMenuButtonMethods()
        {
            if (_cachedMethods == null)
            {
                _cachedMethods = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                    })
                    .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    .Where(m => m.GetCustomAttribute<MenuButtonItemAttribute>() != null)
                    .ToList();
            }
            return _cachedMethods;
        }

        private void BuildMenuTree(List<MethodInfo> methods)
        {
            _rootNode = new MenuNode { Name = "Root", Path = "" };

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<MenuButtonItemAttribute>();
                var pathParts = attr.Path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                var current = _rootNode;
                var currentPath = "";

                foreach (var part in pathParts)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                    var child = current.Children.FirstOrDefault(n => n.Name == part);

                    if (child == null)
                    {
                        child = new MenuNode { Name = part, Path = currentPath };
                        current.Children.Add(child);
                    }

                    current = child;
                }

                // Add button to the leaf node
                current.Buttons.Add(new ButtonInfo
                {
                    Label = attr.ButtonText,
                    Tooltip = attr.Tooltip,
                    Action = () => method.Invoke(null, null),
                    IsPage = attr.IsPage,
                    PageDrawAction = TryGetCustomPageDraw(method)
                });
            }
        }

        // Try to find a static method named <MethodName>_Page that returns void and takes no parameters
        private Action TryGetCustomPageDraw(MethodInfo method)
        {
            var pageMethod = method.DeclaringType?.GetMethod(method.Name + "_Page", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (pageMethod != null && pageMethod.ReturnType == typeof(void) && pageMethod.GetParameters().Length == 0)
            {
                return () => pageMethod.Invoke(null, null);
            }
            return null;
        }

        private void InitializeStyles()
        {
            if (_normalBg == null)
                _normalBg = CreateColorTexture(new Color(0.32f, 0.32f, 0.32f));
            if (_hoverBg == null)
                _hoverBg = CreateColorTexture(new Color(0.42f, 0.42f, 0.42f));
            if (_activeBg == null)
                _activeBg = CreateColorTexture(new Color(0.18f, 0.18f, 0.18f));

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter
                };
                _headerStyle.normal.textColor = Color.white;
            }

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fixedHeight = _buttonHeight,
                    padding = new RectOffset(8, 8, 8, 8),
                    border = new RectOffset(3, 3, 3, 3)
                };
                _buttonStyle.normal.textColor = Color.white;
                _buttonStyle.normal.background = _normalBg;
                _buttonStyle.hover.textColor = Color.white;
                _buttonStyle.hover.background = _hoverBg;
                _buttonStyle.active.textColor = Color.white;
                _buttonStyle.active.background = _activeBg;
                _buttonStyle.border = new RectOffset(3, 3, 3, 3);
            }

            if (_compactFoldoutStyle == null)
            {
                var baseStyle = EditorStyles.label ?? new GUIStyle();
                _compactFoldoutStyle = new GUIStyle(baseStyle)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(6, 2, 2, 2),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                _compactFoldoutStyle.normal.textColor = Color.white;
                _compactFoldoutStyle.focused.textColor = Color.white;
            }

            if (_popupIcon == null)
            {
                string assetPath = "Packages/com.akira.tools/Editor/popup.png";
                _popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
        }

        private void DrawNode(MenuNode node, int indent, bool isTopLevel = false)
        {
            var hasContent = node.Children.Count > 0 || node.Buttons.Count > 0;
            if (!hasContent) return;

            if (!_foldoutStates.ContainsKey(node.Path))
                _foldoutStates[node.Path] = true;

            // Flat, compact foldout header with border
            Rect headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(20), GUILayout.ExpandWidth(true));
            float arrowSize = 12f;
            float arrowPadding = 2f;

            EditorGUI.DrawRect(headerRect, _foldoutBgColor);
            DrawRectBorder(headerRect, _foldoutBorderColor, 1);

            // Draw foldout arrow
            Rect arrowRect = new Rect(headerRect.x + indent * 12 + arrowPadding, headerRect.y + 4, arrowSize, arrowSize);
            bool isOpen = _foldoutStates[node.Path];
            EditorGUI.BeginChangeCheck();
            isOpen = EditorGUI.Foldout(arrowRect, isOpen, GUIContent.none, false);
            if (EditorGUI.EndChangeCheck())
                _foldoutStates[node.Path] = isOpen;

            // Draw label next to arrow, ensure style is not null
            Rect labelRect = new Rect(arrowRect.xMax + 2, headerRect.y, headerRect.width - (arrowRect.xMax - headerRect.x), headerRect.height);
            var style = _compactFoldoutStyle ?? EditorStyles.label;
            EditorGUI.LabelField(labelRect, node.Name, style);

            if (_foldoutStates[node.Path])
            {
                EditorGUILayout.BeginVertical();

                // Draw buttons
                if (node.Buttons.Count > 0)
                    DrawButtonGrid(node.Buttons);

                // Draw children recursively, separated by a thin line
                bool firstChild = true;
                foreach (var child in node.Children)
                {
                    if (!firstChild)
                        DrawDividerLine();
                    DrawNode(child, indent + 1, false);
                    firstChild = false;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawDividerLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.25f, 0.25f, 1f));
        }

        // Draws a border around a rect with the given color and thickness
        private void DrawRectBorder(Rect rect, Color color, int thickness)
        {
            // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            // Left
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            // Right
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
            // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        }

        private void DrawButtonGrid(List<ButtonInfo> buttons)
        {
            var totalButtons = buttons.Count;
            var rows = Mathf.CeilToInt((float)totalButtons / _buttonsPerRow);

            float spacing = Mathf.Clamp(2f + (8f - totalButtons), 2f, 8f);

            for (var row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(spacing);

                int buttonsInThisRow = Math.Min(_buttonsPerRow, totalButtons - row * _buttonsPerRow);

                for (var col = 0; col < buttonsInThisRow; col++)
                {
                    var index = row * _buttonsPerRow + col;
                    var button = buttons[index];

                    Rect buttonRect = GUILayoutUtility.GetRect(
                        new GUIContent(button.Label, button.Tooltip),
                        _buttonStyle,
                        GUILayout.Height(_buttonHeight),
                        GUILayout.ExpandWidth(true)
                    );

                    bool pressed = false;
                    if (button.IsPage)
                    {
                        // Draw button background and label
                        if (GUI.Button(buttonRect, GUIContent.none, _buttonStyle))
                            pressed = true;

                        // Draw label (full width, not offset)
                        GUI.Label(buttonRect, new GUIContent(button.Label, button.Tooltip), _buttonStyle);

                        // Draw popup icon absolutely at the top right inside the button
                        if (_popupIcon != null)
                        {
                            var iconSize = 16f;
                            var iconRect = new Rect(
                                buttonRect.xMax - iconSize - 4,
                                buttonRect.y + 2,
                                iconSize, iconSize
                            );
                            Color prevColor = GUI.color;
                            GUI.color = Color.white;
                            GUI.DrawTexture(iconRect, _popupIcon, ScaleMode.ScaleToFit, true);
                            GUI.color = prevColor;
                        }
                    }
                    else
                    {
                        if (GUI.Button(buttonRect, new GUIContent(button.Label, button.Tooltip), _buttonStyle))
                            pressed = true;
                    }

                    if (col < buttonsInThisRow - 1)
                        GUILayout.Space(spacing);

                    if (pressed)
                    {
                        if (button.IsPage)
                        {
                            // If navigating from the middle, remove forward pages
                            if (_currentPageIndex < _pageStack.Count - 1)
                                _pageStack.RemoveRange(_currentPageIndex + 1, _pageStack.Count - _currentPageIndex - 1);

                            // Only add if not already the current page
                            if (_currentPageIndex == -1 || _pageStack.Count == 0 || _pageStack[_currentPageIndex].Title != button.Label)
                            {
                                _pageStack.Add(new PageState
                                {
                                    Title = button.Label,
                                    DrawPage = button.PageDrawAction ?? (() =>
                                    {
                                        GUILayout.Label("No custom page content.", EditorStyles.centeredGreyMiniLabel);
                                        GUILayout.Space(10);
                                        if (GUILayout.Button("Back", GUILayout.Width(80)))
                                            _currentPageIndex = Math.Max(-1, _currentPageIndex - 1);
                                    })
                                });
                                _currentPageIndex = _pageStack.Count - 1;
                            }
                        }
                        else
                        {
                            button.Action?.Invoke();
                        }
                    }
                }

                GUILayout.Space(spacing);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        public static void ShowScriptImportPage(string templateName, string outputName, string displayName)
        {
            if (_instance == null)
                _instance = GetWindow<ToolsHub>("Akira Tools Hub");

            _instance.RemoveScriptImportPageFromStack();

            ScriptImportPage.Show(templateName, outputName, displayName, () =>
            {
                _instance.RemoveScriptImportPageFromStack();
                _instance.Repaint();
            });

            _instance._pageStack.Add(new PageState
            {
                Title = $"Import {displayName} Script",
                DrawPage = ScriptImportPage.Draw
            });
            _instance._currentPageIndex = _instance._pageStack.Count - 1;
            _instance.Repaint();
        }

        private void RemoveScriptImportPageFromStack()
        {
            for (int i = _pageStack.Count - 1; i >= 0; i--)
            {
                if (_pageStack[i].DrawPage == ScriptImportPage.Draw)
                {
                    _pageStack.RemoveAt(i);
                    if (_currentPageIndex >= _pageStack.Count)
                        _currentPageIndex = _pageStack.Count - 1;
                }
            }
        }

        [MenuItem("Tools/Akira Tools Hub")]
        public static void ShowWindow()
        {
            var window = GetWindow<ToolsHub>("Akira Tools Hub");
            window.minSize = new Vector2(450, 350);
            _instance = window;
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, color);
            tex.Apply(false, true);
            return tex;
        }

        // Node structure for menu
        private class MenuNode
        {
            public readonly List<ButtonInfo> Buttons = new();
            public readonly List<MenuNode> Children = new();
            public string Name;
            public string Path;
        }

        private class ButtonInfo
        {
            public Action Action;
            public string Label;
            public string Tooltip;
            public bool IsPage;
            public Action PageDrawAction;
        }
    }
}
#endif
