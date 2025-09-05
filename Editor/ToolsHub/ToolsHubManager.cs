#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using akira.UI;
using Editor.Files;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace akira.ToolsHub
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

    public enum PageOperationResult
    {
        Success,
        Failure,
        Cancelled
    }

    public class ToolsHubManager : EditorWindow
    {
        private static ToolsHubManager _instance;
        private static List<MethodInfo> _cachedMethods = new();
        private static Texture2D _normalBg;
        private static Texture2D _hoverBg;
        private static Texture2D _activeBg;
        private static string _toolbarNotification;
        private static double _toolbarNotificationTime;
        private static bool _refreshQueued;
        private readonly float _buttonHeight = 28;
        private readonly Color _foldoutBgColor = new(0.18f, 0.18f, 0.18f, 1f);
        private readonly Color _foldoutBorderColor = new(0.35f, 0.35f, 0.35f, 1f);
        private readonly List<PageState> _pageStack = new();
        private GUIStyle _buttonStyle;
        private GUIStyle _compactFoldoutStyle;
        private int _currentPageIndex = -1;
        private GUIStyle _headerStyle;
        private Texture2D _popupIcon;
        private MenuNode _rootNode;
        private Vector2 _scrollPosition;
        private IMGUIContainer _imguiContainer;
        private static readonly float MinWidth = 480;
        private static readonly float MinHeight = 600;
        private static Action _pageRefreshHandler; // new: per-page refresh hook

        private void OnEnable()
        {
            _instance = this;
            minSize = new Vector2(MinWidth, MinHeight);

            WarmupMenuCache();
            RefreshMenuTree();
            EditorApplication.delayCall += RefreshOpenWindowMenu;
            ClearPageStack();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.minWidth = MinWidth;
            root.style.minHeight = MinHeight;

            if (_imguiContainer == null)
            {
                _imguiContainer = new IMGUIContainer(DrawIMGUI)
                {
                    style = { flexGrow = 1 }
                };
                root.Add(_imguiContainer);
            }
        }

        private void OnDisable()
        {
            _cachedMethods = null;
            ClearPageStack();
        }

        private void DrawIMGUI()
        {
            try { InitializeStyles(); } catch (Exception ex) { Debug.LogWarning($"ToolsHub styles init skipped: {ex.Message}"); }

            DrawToolbar();

            if (_currentPageIndex > -1 && _currentPageIndex < _pageStack.Count)
            {
                var page = _pageStack[_currentPageIndex];
                PageLayout.ResetState(page.Title);
                GUILayout.Space(8);

                if (!string.IsNullOrEmpty(page.Title))
                {
                    page.DrawPage?.Invoke();

                    if (!PageLayout.HasHeaderBeenDrawn)
                    {
                        GUILayout.Label(page.Title, _headerStyle);
                        GUILayout.Space(8);
                    }
                }
                else
                {
                    GUILayout.Label("Unnamed Page", _headerStyle);
                    GUILayout.Space(8);
                    page.DrawPage?.Invoke();
                }

                return;
            }

            // No page: ensure refresh handler is cleared
            _pageRefreshHandler = null;

            GUILayout.Space(8);
            GUILayout.Label("Akira Tools Hub", _headerStyle ?? EditorStyles.boldLabel);
            GUILayout.Space(8);

            if (_rootNode == null)
            {
                EditorGUILayout.HelpBox("Loading menu…", MessageType.Info);
                // Queue a single refresh if not already queued to avoid flooding delayCall on every repaint
                if (!_refreshQueued)
                {
                    _refreshQueued = true;
                    EditorApplication.delayCall += () =>
                    {
                        try { RefreshOpenWindowMenu(); }
                        finally { _refreshQueued = false; }
                    };
                }
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
                    UIEditorUtils.DrawDividerLine();
                DrawNode(node, 0, true);
                first = false;
            }

            EditorGUILayout.EndScrollView();
            DrawRecentRenames();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawBackForwardButtons();
            GUILayout.Space(8);

            if (!string.IsNullOrEmpty(_toolbarNotification))
            {
                var notifStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.yellow },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                notifStyle.richText = true;
                GUILayout.FlexibleSpace();
                GUILayout.Label(_toolbarNotification, notifStyle, GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrEmpty(_toolbarNotification) &&
                EditorApplication.timeSinceStartup - _toolbarNotificationTime > 10)
            {
                _toolbarNotification = null;
                Repaint();
            }

            // Toolbar Refresh now calls page hook if available, otherwise refreshes menu
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                if (_pageRefreshHandler != null)
                {
                    try { _pageRefreshHandler.Invoke(); }
                    catch (Exception ex) { Debug.LogError($"Page refresh error: {ex.Message}"); }
                }
                else
                {
                    RefreshMenuTree();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void SetPageRefreshHandler(Action handler)
        {
            _pageRefreshHandler = handler;
        }

        public static void ClearPageRefreshHandler()
        {
            _pageRefreshHandler = null;
        }

        public static void ShowPage(string title, Action drawMethod, Action<PageOperationResult> onResult = null)
        {
            if (_instance == null)
                _instance = GetWindow<ToolsHubManager>("Akira Tools Hub");
            PageLayout.ResetState(title);

            if (_instance._currentPageIndex < _instance._pageStack.Count - 1)
                _instance._pageStack.RemoveRange(_instance._currentPageIndex + 1,
                    _instance._pageStack.Count - _instance._currentPageIndex - 1);
            _instance._pageStack.Add(new PageState { Title = title, DrawPage = drawMethod, OnResult = onResult });
            _instance._currentPageIndex = _instance._pageStack.Count - 1;
            _pageRefreshHandler = null; // new page, clear old handler
            _instance.Repaint();
        }

        public static void ClosePage(PageOperationResult result)
        {
            if (_instance == null || _instance._currentPageIndex < 0 || _instance._pageStack.Count == 0)
                return;
            var page = _instance._pageStack[_instance._currentPageIndex];
            page.OnResult?.Invoke(result);
            _instance._pageStack.RemoveAt(_instance._currentPageIndex);
            _instance._currentPageIndex = _instance._pageStack.Count - 1;
            _pageRefreshHandler = null; // leaving page
            _instance.Repaint();
        }

        public static void ClosePages(int count, PageOperationResult result)
        {
            if (_instance == null || _instance._pageStack.Count == 0) return;

            for (var i = 0; i < count && _instance._pageStack.Count > 0; i++)
                if (_instance._currentPageIndex >= 0 && _instance._currentPageIndex < _instance._pageStack.Count)
                {
                    var page = _instance._pageStack[_instance._currentPageIndex];
                    page.OnResult?.Invoke(result);
                    _instance._pageStack.RemoveAt(_instance._currentPageIndex);
                    _instance._currentPageIndex--;
                }

            _instance._currentPageIndex =
                Math.Max(0, Math.Min(_instance._pageStack.Count - 1, _instance._currentPageIndex));

            if (_instance._pageStack.Count == 0)
                _instance._currentPageIndex = -1;
            _pageRefreshHandler = null; // after navigation
            _instance.Repaint();
        }

        public static void ShowNotification(string message, string type = "info")
        {
            if (_instance == null)
                _instance = GetWindow<ToolsHubManager>("Akira Tools Hub");

            switch (type.ToLower())
            {
                case "success":
                    message = $"<color=green>{message}</color>";

                    break;
                case "warning":
                    message = $"<color=yellow>{message}</color>";

                    break;
                case "error":
                    message = $"<color=red>{message}</color>";

                    break;
                default:
                    message = $"<color=white>{message}</color>";

                    break;
            }

            _toolbarNotification = message;
            _toolbarNotificationTime = EditorApplication.timeSinceStartup;

            if (_instance != null)
                _instance.Repaint();
        }

        // Warmup cached method list so first-time opening doesn't require a manual refresh
        public static void WarmupMenuCache()
        {
            try { _ = GetMenuButtonMethods(); } catch { /* ignore */ }
        }

        // External-safe way to refresh menu of an open window (no instance => no-op)
        public static void RefreshOpenWindowMenu()
        {
            if (_instance != null)
            {
                _instance.RefreshMenuTree();
                _instance.Repaint();
            }
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
            // Hide the recent rename list entirely when asset prefixing is disabled
            if (!AutoAssetPrefix.Enabled)
                return;

            var displayCount = Mathf.Min(AutoAssetPrefix.RecentRenameDisplayCount, AutoAssetPrefix.RecentRenames.Count);

            if (displayCount > 0)
            {
                UIEditorUtils.DrawDividerLine();
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
                    var currentPath = RenameLogStore.GetCurrentAssetPathForRename(entry);
                    var fileExists = !string.IsNullOrEmpty(currentPath) && File.Exists(currentPath);
                    var textStyle = new GUIStyle(EditorStyles.label);

                    if (!fileExists)
                    {
                        textStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
                        textStyle.fontStyle = FontStyle.Italic;
                        textStyle.richText = true;

                        GUILayout.Label($"<s>{entry.OldName}  →  {entry.NewName}</s>", textStyle,
                            GUILayout.ExpandWidth(true));
                    }
                    else
                    {
                        GUILayout.Label($"{entry.OldName}  →  {entry.NewName}", textStyle, GUILayout.ExpandWidth(true));
                    }

                    EditorGUI.BeginDisabledGroup(!fileExists);

                    if (GUILayout.Button("Ping", GUILayout.Width(36)))
                        if (fileExists)
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<Object>(currentPath);

                            if (obj != null)
                                EditorGUIUtility.PingObject(obj);
                        }

                    EditorGUI.EndDisabledGroup();
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
            var typeName = method.DeclaringType?.Name;

            if (typeName != null)
            {
                var drawMethod = method.DeclaringType?.GetMethod("Draw",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (drawMethod != null && drawMethod.ReturnType == typeof(void) &&
                    drawMethod.GetParameters().Length == 0)
                    return () => drawMethod.Invoke(null, null);

                var pageMethod = method.DeclaringType?.GetMethod(method.Name + "_Page",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (pageMethod != null && pageMethod.ReturnType == typeof(void) &&
                    pageMethod.GetParameters().Length == 0)
                    return () => pageMethod.Invoke(null, null);

                var folderCustomizationMethod = method.DeclaringType?.GetMethod("DrawFolderCustomizationPage",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (folderCustomizationMethod != null && folderCustomizationMethod.ReturnType == typeof(void) &&
                    folderCustomizationMethod.GetParameters().Length == 0)
                    return () => folderCustomizationMethod.Invoke(null, null);
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
                var baseHeader = EditorStyles.boldLabel ?? new GUIStyle();
                _headerStyle = new GUIStyle(baseHeader)
                {
                    fontSize = 16, alignment = TextAnchor.MiddleCenter
                };
                _headerStyle.normal.textColor = Color.white;
            }

            if (_buttonStyle == null)
            {
                var mini = EditorStyles.miniButton ?? new GUIStyle(GUI.skin.button);
                _buttonStyle = new GUIStyle(mini)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fixedHeight = _buttonHeight,
                    padding = new RectOffset(8, 8, 8, 8),
                    border = new RectOffset(3, 3, 3, 3)
                };
                _buttonStyle.normal.textColor = Color.white;
                _buttonStyle.hover.textColor = Color.white;
                _buttonStyle.active.textColor = Color.white;
                // backgrounds depend on generated textures
                _buttonStyle.normal.background = _normalBg;
                _buttonStyle.hover.background = _hoverBg;
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
                var assetPath = "Packages/com.akira.tools/Assets/popup.png";
                try { _popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath); } catch { _popupIcon = null; }
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
            UIEditorUtils.DrawRectBorder(headerRect, _foldoutBorderColor, 1);
            var arrowRect = new Rect(headerRect.x + indent * 12 + arrowPadding, headerRect.y + 4, arrowSize, arrowSize);

            var labelRect = new Rect(arrowRect.xMax + 2, headerRect.y,
                headerRect.width - (arrowRect.xMax - headerRect.x), headerRect.height);
            var style = _compactFoldoutStyle ?? EditorStyles.label;
            EditorGUI.BeginChangeCheck();
            isOpen = EditorGUI.Foldout(arrowRect, isOpen, GUIContent.none, false);
            var arrowChanged = EditorGUI.EndChangeCheck();
            EditorGUI.LabelField(labelRect, node.Name, style);
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
                        UIEditorUtils.DrawDividerLine();
                    DrawNode(child, indent + 1);
                    firstChild = false;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawButtonGrid(List<ButtonInfo> buttons)
        {
            if (buttons == null || buttons.Count == 0) return;

            const float gap = 8f;               // horizontal gap between buttons
            const float sideGap = 4f;           // left/right padding inside container
            const int maxButtonsPerRow = 4;     // cap columns per row

            var total = buttons.Count;
            var index = 0;

            // Measure label widths with current style (includes style padding)
            var measured = new float[total];
            for (var i = 0; i < total; i++)
            {
                var gc = new GUIContent(buttons[i].Label, buttons[i].Tooltip);
                measured[i] = Mathf.Ceil(_buttonStyle.CalcSize(gc).x);
            }

            while (index < total)
            {
                // Derive available row width from the current view width (stable across layout/repaint)
                var availableWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 20f);

                // Determine how many buttons to place in this row based on content widths
                var remaining = total - index;
                var countThisRow = Mathf.Min(maxButtonsPerRow, remaining);

                while (countThisRow > 1)
                {
                    var candidateWidth = (availableWidth - gap * (countThisRow - 1)) / countThisRow;
                    var required = 0f;

                    for (var k = 0; k < countThisRow; k++)
                        required = Mathf.Max(required, measured[index + k]);

                    if (candidateWidth + 0.5f >= required)
                        break;

                    countThisRow--;
                }

                // Row: equal-width layout using GUILayout so Unity fills space; no explicit width reservation
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(sideGap);

                for (var i = 0; i < countThisRow && index < total; i++)
                {
                    var btn = buttons[index];
                    var content = new GUIContent(btn.Label, btn.Tooltip);
                    var pressed = false;

                    if (btn.IsPage)
                    {
                        if (GUILayout.Button(content, _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(_buttonHeight)))
                            pressed = true;
                        var rect = GUILayoutUtility.GetLastRect();

                        if (_popupIcon != null)
                        {
                            const float iconSize = 16f;
                            var iconRect = new Rect(rect.xMax - iconSize - 4, rect.y + 2, iconSize, iconSize);
                            var prev = GUI.color;
                            GUI.color = Color.white;
                            GUI.DrawTexture(iconRect, _popupIcon, ScaleMode.ScaleToFit, true);
                            GUI.color = prev;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(content, _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(_buttonHeight)))
                            pressed = true;
                    }

                    if (i < countThisRow - 1)
                        GUILayout.Space(gap);

                    if (pressed)
                    {
                        if (btn.IsPage)
                        {
                            if (_currentPageIndex < _pageStack.Count - 1)
                                _pageStack.RemoveRange(_currentPageIndex + 1, _pageStack.Count - _currentPageIndex - 1);

                            if (_currentPageIndex == -1 || _pageStack.Count == 0 ||
                                _pageStack[_currentPageIndex].Title != btn.Label)
                            {
                                _pageStack.Add(new PageState
                                {
                                    Title = btn.Label,
                                    DrawPage = btn.PageDrawAction ?? (() =>
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
                            btn.Action?.Invoke();
                        }
                    }

                    index++;
                }
                
                GUILayout.Space(sideGap);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply(false, true);

            return tex;
        }

        [MenuItem("Tools/Akira Tools Hub")]
        public static void ShowWindow()
        {
            var window = GetWindow<ToolsHubManager>("Akira Tools Hub");
            window.minSize = new Vector2(MinWidth, MinHeight);
            _instance = window;
        }

        private class PageState
        {
            public Action DrawPage;
            public Action<PageOperationResult> OnResult;
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

    // Auto-initialize the Tools Hub menu cache on editor load and after assembly reloads
    [InitializeOnLoad]
    internal static class ToolsHubAutoInit
    {
        static ToolsHubAutoInit()
        {
            // Delay call to ensure assemblies are fully loaded
            EditorApplication.delayCall += () =>
            {
                ToolsHubManager.WarmupMenuCache();
                ToolsHubManager.RefreshOpenWindowMenu();
            };

            AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    ToolsHubManager.WarmupMenuCache();
                    ToolsHubManager.RefreshOpenWindowMenu();
                };
            };
        }
    }
}
#endif
