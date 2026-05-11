#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Editor.Files;
using UnityEditor;

namespace Akira.ToolsHub
{
    /// <summary>
    /// ViewModel for ToolsHub Manager - handles business logic and state management
    /// Separates data/logic from UI rendering following MVVM pattern
    /// </summary>
    public class ToolsHubManagerViewModel
    {
        // State
        private List<MethodInfo> _cachedMethods = new();
        private MenuNode _rootNode;
        private readonly List<PageState> _pageStack = new();
        private int _currentPageIndex = -1;
        private Action _pageRefreshHandler;
        private bool _refreshQueued;

        // Notification state
        private string _toolbarNotification;
        private double _toolbarNotificationTime;

        // Events for UI updates
        public event Action OnStateChanged;
        public event Action OnMenuRefreshed;

        // Properties
        public MenuNode RootNode => _rootNode;
        public List<PageState> PageStack => _pageStack;
        public int CurrentPageIndex => _currentPageIndex;
        public PageState CurrentPage => _currentPageIndex >= 0 && _currentPageIndex < _pageStack.Count 
            ? _pageStack[_currentPageIndex] 
            : null;
        public string ToolbarNotification => _toolbarNotification;
        public double ToolbarNotificationTime => _toolbarNotificationTime;
        public bool HasPages => _pageStack.Count > 0;
        public bool CanGoBack => _pageStack.Count > 0; // Can go back if we have any pages (back to menu)
        public bool CanGoForward => _currentPageIndex >= 0 && _currentPageIndex < _pageStack.Count - 1;

        #region Initialization

        public void Initialize()
        {
            WarmupMenuCache();
        }

        public void WarmupMenuCache()
        {
            GetMenuButtonMethods();
        }

        #endregion

        #region Menu Management

        public void RefreshMenuTree()
        {
            var methods = GetMenuButtonMethods();
            BuildMenuTree(methods);
            OnMenuRefreshed?.Invoke();
            OnStateChanged?.Invoke();
        }

        private List<MethodInfo> GetMenuButtonMethods()
        {
            if (_cachedMethods == null || _cachedMethods.Count == 0)
            {
                _cachedMethods = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.FullName.StartsWith("Unity") && !a.FullName.StartsWith("System"))
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
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
                if (attr == null) continue;

                var parts = attr.Path.Split('/');
                var currentNode = _rootNode;
                var currentPath = "";

                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                    var child = currentNode.Children.FirstOrDefault(c => c.Name == part);

                    if (child == null)
                    {
                        child = new MenuNode { Name = part, Path = currentPath };
                        currentNode.Children.Add(child);
                    }
                    currentNode = child;
                }

                var buttonInfo = new ButtonInfo
                {
                    Label = attr.ButtonText,
                    Tooltip = attr.Tooltip,
                    IsPage = attr.IsPage,
                    Action = attr.IsPage ? null : () => method.Invoke(null, null)
                };

                if (attr.IsPage)
                {
                    buttonInfo.PageDrawAction = TryGetCustomPageDraw(method);
                }

                currentNode.Buttons.Add(buttonInfo);
            }
        }

        private Action TryGetCustomPageDraw(MethodInfo method)
        {
            var typeName = method.DeclaringType?.FullName;
            if (typeName != null)
            {
                var declaringType = method.DeclaringType;
                
                // First try {MethodName}_Page pattern (e.g., ShowPage_Page, ShowPackagesPage_Page)
                var methodBaseName = method.Name;
                var pageMethodName = $"{methodBaseName}_Page";
                var pageMethod = declaringType.GetMethod(pageMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (pageMethod != null && pageMethod.ReturnType == typeof(void) && pageMethod.GetParameters().Length == 0)
                    return () => pageMethod.Invoke(null, null);
                
                // Then try common patterns
                var drawMethod = declaringType.GetMethod("DrawPage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (drawMethod != null && drawMethod.ReturnType == typeof(void) && drawMethod.GetParameters().Length == 0)
                    return () => drawMethod.Invoke(null, null);

                var pageMethod2 = declaringType.GetMethod("Page", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (pageMethod2 != null && pageMethod2.ReturnType == typeof(void) && pageMethod2.GetParameters().Length == 0)
                    return () => pageMethod2.Invoke(null, null);

                var folderCustomizationMethod = declaringType.GetMethod("FolderCustomizationPage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (folderCustomizationMethod != null && folderCustomizationMethod.ReturnType == typeof(void) && folderCustomizationMethod.GetParameters().Length == 0)
                    return () => folderCustomizationMethod.Invoke(null, null);
            }

            return () =>
            {
                method.Invoke(null, null);
            };
        }

        #endregion

        #region Page Navigation

        public void ShowPage(string title, Action drawMethod, Action<PageOperationResult> onResult = null)
        {
            if (_currentPageIndex < _pageStack.Count - 1)
                _pageStack.RemoveRange(_currentPageIndex + 1, _pageStack.Count - _currentPageIndex - 1);

            _pageStack.Add(new PageState { Title = title, DrawPage = drawMethod, OnResult = onResult });
            _currentPageIndex = _pageStack.Count - 1;
            OnStateChanged?.Invoke();
        }

        public void OpenPageFromButton(ButtonInfo button)
        {
            if (_currentPageIndex < _pageStack.Count - 1)
                _pageStack.RemoveRange(_currentPageIndex + 1, _pageStack.Count - _currentPageIndex - 1);

            if (_currentPageIndex == -1 || _pageStack.Count == 0 || _pageStack[_currentPageIndex].Title != button.Label)
            {
                _pageStack.Add(new PageState
                {
                    Title = button.Label,
                    DrawPage = button.PageDrawAction ?? (() => { })
                });
                _currentPageIndex = _pageStack.Count - 1;
                OnStateChanged?.Invoke();
            }
        }

        public void ClosePage(PageOperationResult result)
        {
            if (_currentPageIndex < 0 || _pageStack.Count == 0) return;

            var page = _pageStack[_currentPageIndex];
            _pageStack.RemoveAt(_currentPageIndex);
            _currentPageIndex = Math.Max(-1, _currentPageIndex - 1);

            page.OnResult?.Invoke(result);
            OnStateChanged?.Invoke();
        }

        public void ClosePages(int count, PageOperationResult result)
        {
            if (_pageStack.Count == 0) return;

            count = Math.Min(count, _pageStack.Count);
            var firstToRemove = _pageStack.Count - count;

            for (var i = _pageStack.Count - 1; i >= firstToRemove; i--)
            {
                var page = _pageStack[i];
                page.OnResult?.Invoke(result);
                _pageStack.RemoveAt(i);
            }

            _currentPageIndex = Math.Min(_currentPageIndex, _pageStack.Count - 1);
            if (_pageStack.Count == 0)
                _currentPageIndex = -1;

            OnStateChanged?.Invoke();
        }

        public void NavigateBack()
        {
            if (CanGoBack)
            {
                _currentPageIndex--;
                // When going back past the first page, return to menu (_currentPageIndex becomes -1)
                if (_currentPageIndex < -1)
                    _currentPageIndex = -1;
                OnStateChanged?.Invoke();
            }
        }

        public void NavigateForward()
        {
            if (CanGoForward)
            {
                _currentPageIndex++;
                OnStateChanged?.Invoke();
            }
        }

        public void ClearPageStack()
        {
            _pageStack.Clear();
            _currentPageIndex = -1;
            OnStateChanged?.Invoke();
        }

        #endregion

        #region Notifications

        public void ShowNotification(string message, string type = "info")
        {
            switch (type.ToLower())
            {
                case "success":
                    _toolbarNotification = $"✓ {message}";
                    break;
                case "warning":
                    _toolbarNotification = $"⚠ {message}";
                    break;
                case "error":
                    _toolbarNotification = $"✕ {message}";
                    break;
                default:
                    _toolbarNotification = $"ℹ {message}";
                    break;
            }

            _toolbarNotificationTime = EditorApplication.timeSinceStartup;
            OnStateChanged?.Invoke();
        }

        public bool ShouldClearNotification()
        {
            return !string.IsNullOrEmpty(_toolbarNotification) &&
                   EditorApplication.timeSinceStartup - _toolbarNotificationTime > 10;
        }

        public void ClearNotification()
        {
            _toolbarNotification = null;
            OnStateChanged?.Invoke();
        }

        #endregion

        #region Refresh Management

        public void SetPageRefreshHandler(Action handler)
        {
            _pageRefreshHandler = handler;
        }

        public void ClearPageRefreshHandler()
        {
            _pageRefreshHandler = null;
        }

        public void ExecuteRefresh()
        {
            if (_pageRefreshHandler != null)
            {
                _pageRefreshHandler.Invoke();
            }
            else
            {
                RefreshMenuTree();
            }
        }

        public void QueueRefresh()
        {
            if (!_refreshQueued)
            {
                _refreshQueued = true;
                EditorApplication.delayCall += () =>
                {
                    _refreshQueued = false;
                    RefreshMenuTree();
                };
            }
        }

        #endregion

        #region Settings

        public int GetRecentRenameDisplayCount()
        {
            return AutoAssetPrefix.RecentRenameDisplayCount;
        }

        public void SetRecentRenameDisplayCount(int count)
        {
            AutoAssetPrefix.RecentRenameDisplayCount = count;
        }

        public bool IsAutoAssetPrefixEnabled()
        {
            return AutoAssetPrefix.Enabled;
        }

        #endregion

        #region Nested Classes

        public class PageState
        {
            public Action DrawPage;
            public Action<PageOperationResult> OnResult;
            public string Title;
        }

        public class MenuNode
        {
            public readonly List<ButtonInfo> Buttons = new();
            public readonly List<MenuNode> Children = new();
            public string Name;
            public string Path;
        }

        public class ButtonInfo
        {
            public Action Action;
            public bool IsPage;
            public string Label;
            public Action PageDrawAction;
            public string Tooltip;
        }

        #endregion
    }
}
#endif
