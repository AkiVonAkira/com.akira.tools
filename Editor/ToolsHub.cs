#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using akira.UI;
using Editor.Files;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace akira
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuButtonItemAttribute : Attribute
    {
        public string ButtonText;
        public bool IsPage;
        public string Path;
        public string Tooltip;

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
        // --- Singleton & Instance ---
        private static ToolsHub _instance;

        // --- Menu/Reflection ---
        private static List<MethodInfo> _cachedMethods = new();

        // --- Textures & Styles ---
        private static Texture2D _normalBg;
        private static Texture2D _hoverBg;
        private static Texture2D _activeBg;

        // --- Notification fields ---
        private static string _toolbarNotification;
        private static double _toolbarNotificationTime;

        // --- Layout & UI ---
        private readonly float _buttonHeight = 28;
        private readonly int _buttonsPerRow = 4;
        private readonly Color _foldoutBgColor = new(0.18f, 0.18f, 0.18f, 1f);
        private readonly Color _foldoutBorderColor = new(0.35f, 0.35f, 0.35f, 1f);

        // --- Navigation/Page Stack ---
        private readonly List<PageState> _pageStack = new();
        private GUIStyle _buttonStyle;
        private GUIStyle _compactFoldoutStyle;
        private int _currentPageIndex = -1;
        private GUIStyle _headerStyle;
        private Texture2D _popupIcon;

        // --- Menu Tree & Scroll ---
        private MenuNode _rootNode;
        private Vector2 _scrollPosition;

        private void OnEnable()
        {
            _instance = this;
            InitializeStyles();
            RefreshMenuTree();
            ClearPageStack();
        }

        private void OnDisable()
        {
            _cachedMethods = null;
            ClearPageStack();
        }

        private void OnGUI()
        {
            InitializeStyles();
            DrawToolbar();

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
            DrawSettingsRow();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            var first = true;

            foreach (var node in _rootNode.Children)
            {
                if (!first)
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                DrawNode(node, 0, true);
                first = false;
            }

            EditorGUILayout.EndScrollView();
            DrawRecentRenames();
        }

        // --- Notification API ---
        public static void ShowNotification(string message)
        {
            _toolbarNotification = message;
            _toolbarNotificationTime = EditorApplication.timeSinceStartup;

            if (_instance != null)
                _instance.Repaint();
        }

        private void RefreshMenuTree()
        {
            _cachedMethods = null;
            BuildMenuTree(GetMenuButtonMethods());
            ClearPageStack();
        }

        private void ClearPageStack()
        {
            _pageStack.Clear();
            _currentPageIndex = -1;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawBackForwardButtons();
            GUILayout.Space(8);

            // --- Notification area ---
            if (!string.IsNullOrEmpty(_toolbarNotification))
            {
                var notifStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.yellow },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                GUILayout.FlexibleSpace();
                GUILayout.Label(_toolbarNotification, notifStyle, GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            // --- Clear notification after 10s ---
            if (!string.IsNullOrEmpty(_toolbarNotification) &&
                EditorApplication.timeSinceStartup - _toolbarNotificationTime > 10)
            {
                _toolbarNotification = null;
                Repaint();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                RefreshMenuTree();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBackForwardButtons()
        {
            GUI.enabled = _currentPageIndex > -1;

            if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24)))
                if (_currentPageIndex > -1)
                {
                    _currentPageIndex--;
                    GUI.FocusControl(null);
                }

            GUI.enabled = true;
            GUI.enabled = _currentPageIndex < _pageStack.Count - 1;

            if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24)))
                if (_currentPageIndex < _pageStack.Count - 1)
                {
                    _currentPageIndex++;
                    GUI.FocusControl(null);
                }

            GUI.enabled = true;
        }

        private void DrawSettingsRow()
        {
            EditorGUILayout.BeginHorizontal();

            AutoAssetPrefix.Enabled = EditorGUILayout.ToggleLeft("Enable Asset Prefixing", AutoAssetPrefix.Enabled,
                GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Recent Renames to Show:", GUILayout.Width(150));
            var newCount = EditorGUILayout.IntField(AutoAssetPrefix.RecentRenameDisplayCount, GUILayout.Width(50));
            newCount = Mathf.Clamp(newCount, 1, 100);

            if (newCount != AutoAssetPrefix.RecentRenameDisplayCount)
                AutoAssetPrefix.RecentRenameDisplayCount = newCount;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawRecentRenames()
        {
            var displayCount = Mathf.Min(AutoAssetPrefix.RecentRenameDisplayCount, AutoAssetPrefix.RecentRenames.Count);

            if (displayCount > 0)
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField("Recent Asset Renames", EditorStyles.boldLabel);

                for (var i = 0; i < displayCount; i++)
                {
                    var entry = AutoAssetPrefix.RecentRenames[i];
                    EditorGUILayout.BeginHorizontal();
                    Texture icon = null;

                    if (!string.IsNullOrEmpty(entry.IconPath))
                    {
                        var iconAssetPath = AssetDatabase.GUIDToAssetPath(entry.IconPath);

                        if (!string.IsNullOrEmpty(iconAssetPath))
                            icon = AssetDatabase.GetCachedIcon(iconAssetPath);
                    }

                    GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));
                    GUILayout.Label($"{entry.OldName}  →  {entry.NewName}", GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("Ping", GUILayout.Width(36)))
                    {
                        var currentPath = RenameLogStore.GetCurrentAssetPathForRename(entry);
                        var obj = AssetDatabase.LoadAssetAtPath<Object>(currentPath);

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
                _cachedMethods = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try
                        {
                            return a.GetTypes();
                        }
                        catch
                        {
                            return Array.Empty<Type>();
                        }
                    })
                    .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    .Where(m => m.GetCustomAttribute<MenuButtonItemAttribute>() != null)
                    .ToList();

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

        private Action TryGetCustomPageDraw(MethodInfo method)
        {
            var pageMethod = method.DeclaringType?.GetMethod(method.Name + "_Page",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (pageMethod != null && pageMethod.ReturnType == typeof(void) && pageMethod.GetParameters().Length == 0)
                return () => pageMethod.Invoke(null, null);

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
                    fontSize = 16, alignment = TextAnchor.MiddleCenter
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
                    margin = new RectOffset(0, 0, 0, 0)
                };
                _compactFoldoutStyle.normal.textColor = Color.white;
                _compactFoldoutStyle.focused.textColor = Color.white;
            }

            if (_popupIcon == null)
            {
                var assetPath = "Packages/com.akira.tools/Editor/popup.png";
                _popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
        }

        private void DrawNode(MenuNode node, int indent, bool isTopLevel = false)
        {
            var hasContent = node.Children.Count > 0 || node.Buttons.Count > 0;

            if (!hasContent) return;

            var isOpen = ToolsHubSettings.GetFoldoutState(node.Path);

            var headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(20), GUILayout.ExpandWidth(true));
            var arrowSize = 12f;
            var arrowPadding = 2f;
            EditorGUI.DrawRect(headerRect, _foldoutBgColor);
            DrawRectBorder(headerRect, _foldoutBorderColor, 1);
            var arrowRect = new Rect(headerRect.x + indent * 12 + arrowPadding, headerRect.y + 4, arrowSize, arrowSize);

            // Calculate label rect to cover the rest of the header
            var labelRect = new Rect(arrowRect.xMax + 2, headerRect.y,
                headerRect.width - (arrowRect.xMax - headerRect.x), headerRect.height);

            var style = _compactFoldoutStyle ?? EditorStyles.label;

            // Draw foldout arrow (no label)
            EditorGUI.BeginChangeCheck();
            isOpen = EditorGUI.Foldout(arrowRect, isOpen, GUIContent.none, false);
            var arrowChanged = EditorGUI.EndChangeCheck();

            // Draw label
            EditorGUI.LabelField(labelRect, node.Name, style);

            // Handle click on label to toggle foldout
            var e = Event.current;

            if (e.type == EventType.MouseDown && labelRect.Contains(e.mousePosition))
            {
                isOpen = !isOpen;
                ToolsHubSettings.SetFoldoutState(node.Path, isOpen);
                e.Use();
            }
            else if (arrowChanged)
            {
                ToolsHubSettings.SetFoldoutState(node.Path, isOpen);
            }

            if (ToolsHubSettings.GetFoldoutState(node.Path))
            {
                EditorGUILayout.BeginVertical();

                if (node.Buttons.Count > 0)
                    DrawButtonGrid(node.Buttons);
                var firstChild = true;

                foreach (var child in node.Children)
                {
                    if (!firstChild)
                        DrawDividerLine();
                    DrawNode(child, indent + 1);
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

        private void DrawRectBorder(Rect rect, Color color, int thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        }

        private void DrawButtonGrid(List<ButtonInfo> buttons)
        {
            var totalButtons = buttons.Count;
            var rows = Mathf.CeilToInt((float)totalButtons / _buttonsPerRow);
            var spacing = Mathf.Clamp(2f + (8f - totalButtons), 2f, 8f);

            for (var row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(spacing);
                var buttonsInThisRow = Math.Min(_buttonsPerRow, totalButtons - row * _buttonsPerRow);

                for (var col = 0; col < buttonsInThisRow; col++)
                {
                    var index = row * _buttonsPerRow + col;
                    var button = buttons[index];

                    var buttonRect = GUILayoutUtility.GetRect(
                        new GUIContent(button.Label, button.Tooltip),
                        _buttonStyle,
                        GUILayout.Height(_buttonHeight),
                        GUILayout.ExpandWidth(true)
                    );
                    var pressed = false;

                    if (button.IsPage)
                    {
                        if (GUI.Button(buttonRect, GUIContent.none, _buttonStyle))
                            pressed = true;
                        GUI.Label(buttonRect, new GUIContent(button.Label, button.Tooltip), _buttonStyle);

                        if (_popupIcon != null)
                        {
                            var iconSize = 16f;

                            var iconRect = new Rect(
                                buttonRect.xMax - iconSize - 4,
                                buttonRect.y + 2,
                                iconSize, iconSize
                            );
                            var prevColor = GUI.color;
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
                            if (_currentPageIndex < _pageStack.Count - 1)
                                _pageStack.RemoveRange(_currentPageIndex + 1, _pageStack.Count - _currentPageIndex - 1);

                            if (_currentPageIndex == -1 || _pageStack.Count == 0 ||
                                _pageStack[_currentPageIndex].Title != button.Label)
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

        public static void ShowScriptImportPage(string templateName, string outputName, string displayName,
            string menuPath = null)
        {
            if (_instance == null)
                _instance = GetWindow<ToolsHub>("Akira Tools Hub");
            _instance.RemoveScriptImportPageFromStack();

            ScriptImportPage.Show(templateName, outputName, displayName, () =>
            {
                _instance.RemoveScriptImportPageFromStack();
                ShowNotification($"Script '{displayName}' imported successfully.");
                _instance.Repaint();
            }, () =>
            {
                _instance.RemoveScriptImportPageFromStack();
                _instance.Repaint();
            }, menuPath);

            _instance._pageStack.Add(new PageState
            {
                Title = $"Import {displayName} Script", DrawPage = ScriptImportPage.Draw
            });
            _instance._currentPageIndex = _instance._pageStack.Count - 1;
            _instance.Repaint();
        }

        private void RemoveScriptImportPageFromStack()
        {
            for (var i = _pageStack.Count - 1; i >= 0; i--)
                if (_pageStack[i].DrawPage == ScriptImportPage.Draw)
                {
                    _pageStack.RemoveAt(i);

                    if (_currentPageIndex >= _pageStack.Count)
                        _currentPageIndex = _pageStack.Count - 1;
                }
        }

        public static void ShowFolderCustomizationPage(List<string> initialFolders, HashSet<string> nonRemovableFolders,
            string structureName = "Type")
        {
            FolderCustomizationPage.Show(initialFolders, nonRemovableFolders, structureName, _instance);
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
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply(false, true);

            return tex;
        }

        private class PageState
        {
            public Action DrawPage;
            public string Title;
        }

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
            public bool IsPage;
            public string Label;
            public Action PageDrawAction;
            public string Tooltip;
        }
    }
}
#endif