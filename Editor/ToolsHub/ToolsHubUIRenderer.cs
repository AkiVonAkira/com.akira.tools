#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Editor.Files;
using UnityEditor;
using UnityEngine;

namespace Akira.ToolsHub
{
    /// <summary>
    /// UI Renderer for ToolsHub Manager - handles all IMGUI drawing with modern, flexible layouts
    /// Inspired by ToolsHubWindow design but using IMGUI instead of UIElements
    /// </summary>
    public class ToolsHubUIRenderer
    {
        private readonly ToolsHubManagerViewModel _viewModel;
        private Vector2 _scrollPosition;
        private Vector2 _menuScrollPosition;
        private string _lastPageError;
        
        public ToolsHubUIRenderer(ToolsHubManagerViewModel viewModel)
        {
            _viewModel = viewModel;
        }
        
        #region Main Layout
        
        public void DrawUI()
        {
            DrawToolbar();

            // Only show header when on a page (not on menu)
            if (_viewModel.HasPages)
            {
                DrawHeader();
            }
            
            // Main content area - flexible scrolling
            if (_viewModel.CurrentPage != null)
            {
                DrawCurrentPage();
            }
            else
            {
                DrawMenuView();
            }
        }
        
        #endregion
        
        #region Header
        
        private void DrawHeader()
        {
            var headerRect = EditorGUILayout.BeginVertical(GUILayout.Height(70));
            EditorGUI.DrawRect(headerRect, ToolsHubStyles.HeaderBgColor);
            
            GUILayout.Space(ToolsHubStyles.HeaderPadding);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(ToolsHubStyles.ContentPadding);
            
            EditorGUILayout.BeginVertical();
            GUILayout.Label("AKIRA TOOLS HUB", ToolsHubStyles.HeaderTitleStyle);
            GUILayout.Label("Unity Editor Enhancement Suite", ToolsHubStyles.HeaderSubtitleStyle);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(ToolsHubStyles.ContentPadding);
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(ToolsHubStyles.HeaderPadding);
            EditorGUILayout.EndVertical();
        }
        
        #endregion
        
        #region Toolbar
        
        private void DrawToolbar()
        {
            var toolbarRect = EditorGUILayout.BeginHorizontal(ToolsHubStyles.ToolbarStyle);
            EditorGUI.DrawRect(toolbarRect, ToolsHubStyles.TabBarBgColor);
            
            // Back/Forward buttons
            DrawBackForwardButtons();
            
            GUILayout.FlexibleSpace();
            
            // Notification area
            DrawToolbarNotification();
            
            GUILayout.FlexibleSpace();
            
            // Refresh button
            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                _viewModel.ExecuteRefresh();
            }
            
            // Settings dropdown (if on menu view)
            if (_viewModel.CurrentPage == null)
            {
                DrawSettingsButton();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawBackForwardButtons()
        {
            EditorGUI.BeginDisabledGroup(!_viewModel.CanGoBack);
            if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _viewModel.NavigateBack();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.BeginDisabledGroup(!_viewModel.CanGoForward);
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _viewModel.NavigateForward();
            }
            EditorGUI.EndDisabledGroup();
        }
        
        private void DrawToolbarNotification()
        {
            if (!string.IsNullOrEmpty(_viewModel.ToolbarNotification))
            {
                var notifStyle = new GUIStyle(EditorStyles.toolbarButton)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                GUILayout.Label(_viewModel.ToolbarNotification, notifStyle, GUILayout.MinWidth(200));
                
                if (_viewModel.ShouldClearNotification())
                {
                    _viewModel.ClearNotification();
                }
            }
        }
        
        private void DrawSettingsButton()
        {
            if (GUILayout.Button("⚙", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                // Settings menu could be expanded here
            }
        }
        
        #endregion
        
        #region Menu View
        
        private void DrawMenuView()
        {
            if (_viewModel.RootNode == null) return;
            
            // Show title below toolbar on home page
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(ToolsHubStyles.ContentPadding);
            EditorGUILayout.BeginVertical();
            GUILayout.Label("AKIRA TOOLS HUB", ToolsHubStyles.HeaderTitleStyle);
            GUILayout.Label("Unity Editor Enhancement Suite", ToolsHubStyles.HeaderSubtitleStyle);
            EditorGUILayout.EndVertical();
            GUILayout.Space(ToolsHubStyles.ContentPadding);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            
            // Scrollable content area
            _menuScrollPosition = EditorGUILayout.BeginScrollView(
                _menuScrollPosition,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none
            );
            
            GUILayout.Space(10);
            
            // Draw settings row if needed
            if (_viewModel.IsAutoAssetPrefixEnabled())
            {
                DrawSettingsRow();
            }
            
            // Draw recent renames
            DrawRecentRenames();
            
            // Draw menu tree
            DrawMenuTree();
            
            GUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawMenuTree()
        {
            foreach (var child in _viewModel.RootNode.Children)
            {
                DrawNode(child, 0, true);
                GUILayout.Space(4);
            }
        }
        
        private void DrawNode(ToolsHubManagerViewModel.MenuNode node, int indent, bool isTopLevel = false)
        {
            var hasContent = node.Children.Count > 0 || node.Buttons.Count > 0;
            if (!hasContent) return;
            
            var isOpen = ToolsHubSettings.GetFoldoutState(node.Path);
            
            // Foldout header with modern styling
            var headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(ToolsHubStyles.FoldoutHeight), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, ToolsHubStyles.FoldoutBgColor);
            UI.UIEditorUtils.DrawRectBorder(headerRect, ToolsHubStyles.FoldoutBorderColor, 1);
            
            var arrowSize = 12f;
            var arrowPadding = 4f;
            var arrowRect = new Rect(
                headerRect.x + indent * 16 + arrowPadding,
                headerRect.y + (headerRect.height - arrowSize) / 2,
                arrowSize,
                arrowSize
            );
            
            var labelRect = new Rect(
                arrowRect.xMax + 6,
                headerRect.y,
                headerRect.width - (arrowRect.xMax - headerRect.x) - 6,
                headerRect.height
            );
            
            // Draw foldout arrow
            EditorGUI.BeginChangeCheck();
            isOpen = EditorGUI.Foldout(arrowRect, isOpen, GUIContent.none, false);
            var arrowChanged = EditorGUI.EndChangeCheck();
            
            // Draw label
            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = isTopLevel ? 13 : 12,
                fontStyle = isTopLevel ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = Color.white },
                padding = new RectOffset(0, 0, 4, 4)
            };
            EditorGUI.LabelField(labelRect, node.Name, labelStyle);
            
            // Handle clicks
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
            
            // Draw content if open
            if (isOpen)
            {
                EditorGUILayout.BeginVertical();
                GUILayout.Space(4);
                
                if (node.Buttons.Count > 0)
                {
                    DrawButtonGrid(node.Buttons);
                }
                
                var firstChild = true;
                foreach (var child in node.Children)
                {
                    if (!firstChild)
                    {
                        GUILayout.Space(2);
                        UI.UIEditorUtils.DrawDividerLine();
                        GUILayout.Space(2);
                    }
                    DrawNode(child, indent + 1);
                    firstChild = false;
                }
                
                GUILayout.Space(4);
                EditorGUILayout.EndVertical();
            }
        }
        
        private void DrawButtonGrid(List<ToolsHubManagerViewModel.ButtonInfo> buttons)
        {
            if (buttons == null || buttons.Count == 0) return;
            
            var total = buttons.Count;
            var index = 0;
            
            // Measure button widths
            var measured = new float[total];
            for (var i = 0; i < total; i++)
            {
                var gc = new GUIContent(buttons[i].Label, buttons[i].Tooltip);
                measured[i] = Mathf.Ceil(ToolsHubStyles.ButtonStyle.CalcSize(gc).x);
            }
            
            while (index < total)
            {
                var availableWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 40f);
                var remaining = total - index;
                var countThisRow = Mathf.Min(ToolsHubStyles.MaxButtonsPerRow, remaining);
                
                // Calculate optimal button count for this row
                while (countThisRow > 1)
                {
                    var candidateWidth = (availableWidth - ToolsHubStyles.ButtonGap * (countThisRow - 1)) / countThisRow;
                    var required = 0f;
                    
                    for (var k = 0; k < countThisRow; k++)
                        required = Mathf.Max(required, measured[index + k]);
                    
                    if (candidateWidth + 0.5f >= required)
                        break;
                    
                    countThisRow--;
                }
                
                // Draw row
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(ToolsHubStyles.SideGap);
                
                for (var i = 0; i < countThisRow && index < total; i++)
                {
                    var btn = buttons[index];
                    var content = new GUIContent(btn.Label, btn.Tooltip);
                    var pressed = false;
                    
                    // Draw button
                    if (GUILayout.Button(content, ToolsHubStyles.ButtonStyle, 
                        GUILayout.ExpandWidth(true), 
                        GUILayout.Height(ToolsHubStyles.MenuButtonHeight)))
                    {
                        pressed = true;
                    }
                    
                    // Draw popup icon for pages
                    if (btn.IsPage)
                    {
                        var rect = GUILayoutUtility.GetLastRect();
                        if (ToolsHubStyles.PopupIcon != null)
                        {
                            const float iconSize = 16f;
                            var iconRect = new Rect(rect.xMax - iconSize - 4, rect.y + (rect.height - iconSize) / 2, iconSize, iconSize);
                            var prev = GUI.color;
                            GUI.color = new Color(1f, 1f, 1f, 0.6f);
                            GUI.DrawTexture(iconRect, ToolsHubStyles.PopupIcon, ScaleMode.ScaleToFit, true);
                            GUI.color = prev;
                        }
                    }
                    
                    if (i < countThisRow - 1)
                        GUILayout.Space(ToolsHubStyles.ButtonGap);
                    
                    // Handle button press
                    if (pressed)
                    {
                        if (btn.IsPage)
                        {
                            _viewModel.OpenPageFromButton(btn);
                        }
                        else
                        {
                            btn.Action?.Invoke();
                        }
                    }
                    
                    index++;
                }
                
                GUILayout.Space(ToolsHubStyles.SideGap);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(3);
            }
        }
        
        private void DrawSettingsRow()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(ToolsHubStyles.ContentPadding);
            
            GUILayout.Label("Recent Renames to Display:", GUILayout.Width(160));
            var newCount = EditorGUILayout.IntSlider(_viewModel.GetRecentRenameDisplayCount(), 0, 20);
            
            if (newCount != _viewModel.GetRecentRenameDisplayCount())
                _viewModel.SetRecentRenameDisplayCount(newCount);
            
            GUILayout.Space(ToolsHubStyles.ContentPadding);
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(8);
        }
        
        private void DrawRecentRenames()
        {
            var displayCount = _viewModel.GetRecentRenameDisplayCount();
            if (!_viewModel.IsAutoAssetPrefixEnabled() || displayCount <= 0) return;
            
            var log = AutoAssetPrefix.RecentRenames;
            if (log == null || log.Count == 0) return;
            
            var itemsToShow = Mathf.Min(displayCount, log.Count);
            
            EditorGUILayout.BeginVertical();
            GUILayout.Space(4);
            
            var headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(22));
            EditorGUI.DrawRect(headerRect, ToolsHubStyles.FoldoutBgColor);
            UI.UIEditorUtils.DrawRectBorder(headerRect, ToolsHubStyles.FoldoutBorderColor, 1);
            
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Color.white },
                padding = new RectOffset(8, 8, 4, 4)
            };
            EditorGUI.LabelField(headerRect, $"📝 Recent Renames ({itemsToShow})", headerStyle);
            
            GUILayout.Space(2);
            
            for (var i = 0; i < itemsToShow; i++)
            {
                var entry = log[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(8);
                
                // Get icon if available
                UnityEngine.Texture icon = null;
                if (!string.IsNullOrEmpty(entry.IconPath))
                {
                    var iconAssetPath = AssetDatabase.GUIDToAssetPath(entry.IconPath);
                    if (!string.IsNullOrEmpty(iconAssetPath))
                        icon = AssetDatabase.GetCachedIcon(iconAssetPath);
                }
                
                // Show icon
                if (icon != null)
                    GUILayout.Label(icon, GUILayout.Width(16), GUILayout.Height(16));
                else
                    GUILayout.Label("•", GUILayout.Width(16));
                
                // Check if file still exists
                var currentPath = RenameLogStore.GetCurrentAssetPathForRename(entry);
                var fileExists = !string.IsNullOrEmpty(currentPath) && System.IO.File.Exists(currentPath);
                
                // Create button style for the entry
                var buttonStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    normal = { textColor = fileExists ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.9f, 0.3f, 0.3f) },
                    hover = { textColor = fileExists ? Color.white : new Color(1f, 0.5f, 0.5f) },
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = fileExists ? FontStyle.Normal : FontStyle.Italic
                };
                
                // Make the whole entry clickable
                var content = new GUIContent(
                    $"{entry.OldName} → {entry.NewName}",
                    fileExists ? "Click to ping in Project window" : "File no longer exists"
                );
                
                if (GUILayout.Button(content, buttonStyle, GUILayout.ExpandWidth(true)))
                {
                    if (fileExists)
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(currentPath);
                        if (obj != null)
                            EditorGUIUtility.PingObject(obj);
                    }
                }
                
                GUILayout.Space(8);
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.Space(8);
            UI.UIEditorUtils.DrawDividerLine();
            GUILayout.Space(8);
            
            EditorGUILayout.EndVertical();
        }
        
        #endregion
        
        #region Page View
        
        private void DrawCurrentPage()
        {
            var page = _viewModel.CurrentPage;
            if (page == null) return;
            
            // Pages now handle their own scroll views, so we don't wrap them here
            GUILayout.Space(10);
            
            // Page title
            if (!string.IsNullOrEmpty(page.Title))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(ToolsHubStyles.ContentPadding);
                
                GUILayout.Label(page.Title, ToolsHubStyles.PageTitleStyle);
                
                GUILayout.Space(ToolsHubStyles.ContentPadding);
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(8);
                UI.UIEditorUtils.DrawDividerLine();
                GUILayout.Space(12);
            }
            
            // Page content
            // Wrap in try-catch but only store error during Layout event to maintain consistent control counts
            if (Event.current.type == EventType.Layout)
            {
                // Clear error and try to draw during layout
                _lastPageError = null;
                try
                {
                    UI.PageLayout.ResetState(page.Title);
                    page.DrawPage?.Invoke();
                }
                catch (Exception ex)
                {
                    _lastPageError = ex.Message;
                    Debug.LogException(ex);
                }
            }
            else
            {
                // During repaint, use the error state from layout
                try
                {
                    UI.PageLayout.ResetState(page.Title);
                    page.DrawPage?.Invoke();
                }
                catch (Exception)
                {
                    // Ignore exceptions during repaint to avoid double logging
                }
            }
            
            // Show error box if one occurred (will be consistent across layout/repaint)
            if (!string.IsNullOrEmpty(_lastPageError))
            {
                EditorGUILayout.HelpBox($"Error drawing page: {_lastPageError}", MessageType.Error);
            }
            
            GUILayout.Space(20);
        }
        
        #endregion

    }
}
#endif
