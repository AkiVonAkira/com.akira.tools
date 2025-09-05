#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using akira.ToolsHub;
using UnityEditor;
using UnityEngine;

namespace akira.UI
{
    /// <summary>
    ///     Utility for standardizing page layouts in the ToolsHub.
    /// </summary>
    public static class PageLayout
    {
        // Layout constants
        private static readonly Color FooterBorderColor = new(0.3f, 0.3f, 0.3f, 1f);

        // New standardized footer colors
        private static readonly Color PrimaryButtonColor = new(0.3f, 0.6f, 0.9f, 1f);
        private static readonly Color SecondaryButtonColor = new(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color DangerButtonColor = new(0.8f, 0.3f, 0.3f, 1f);

        // Store scroll positions by content area ID
        private static readonly Dictionary<string, Vector2> _scrollPositions = new();

        // Track active content areas to ensure proper nesting
        private static readonly HashSet<string> _activeContentAreas = new();

        private static bool _inContentHeader;
        private static string _currentPage = string.Empty;

        /// <summary>
        ///     Check if a header has been drawn for the current page
        /// </summary>
        public static bool HasHeaderBeenDrawn { get; private set; }

        /// <summary>
        ///     Reset the internal state for a new page
        /// </summary>
        public static void ResetState(string pageTitle = null)
        {
            HasHeaderBeenDrawn = false;
            _inContentHeader = false;
            _activeContentAreas.Clear();

            if (pageTitle != null)
                _currentPage = pageTitle;
        }

        /// <summary>
        ///     Draws a page header with title and optional description.
        /// </summary>
        public static void DrawPageHeader(string title, string description = null)
        {
            if (string.IsNullOrEmpty(title))
                title = "Untitled Page";

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, wordWrap = true
            };

            GUILayout.Space(8);
            GUILayout.Label(title, headerStyle);

            if (!string.IsNullOrEmpty(description))
            {
                var descriptionStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter, wordWrap = true
                };
                GUILayout.Space(2);
                GUILayout.Label(description, descriptionStyle);
            }

            GUILayout.Space(8);
            DrawSeparator();

            HasHeaderBeenDrawn = true;
        }

        /// <summary>
        ///     Begin a content header section for controls like buttons, toggles, etc.
        ///     Must be paired with EndContentHeader.
        /// </summary>
        public static void BeginContentSpacing()
        {
            _inContentHeader = true;
            EditorGUILayout.BeginVertical();
        }

        /// <summary>
        ///     End a content header section started with BeginContentHeader.
        /// </summary>
        public static void EndContentSpacing()
        {
            if (_inContentHeader)
            {
                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
                _inContentHeader = false;
            }
        }

        /// <summary>
        ///     Begins a scrollable content section that fills available space.
        ///     Must be paired with EndPageContent with the same contentId.
        /// </summary>
        /// <param name="contentId">Unique identifier for this content area</param>
        /// <param name="expandHeight">Whether this content area should expand to fill available space</param>
        /// <returns>Returns true if content should be drawn</returns>
        public static bool BeginPageContent(string contentId = "default", bool expandHeight = true)
        {
            // Prevent duplicate content areas with same ID
            if (_activeContentAreas.Contains(contentId))
            {
                Debug.LogError(
                    $"Content area with ID '{contentId}' is already active. Make sure to call EndPageContent before starting a new one.");

                return false;
            }

            // Create a unique key for this scroll position
            var scrollKey = $"{_currentPage}_{contentId}";

            // Get the stored scroll position for this content area or create a new one
            if (!_scrollPositions.TryGetValue(scrollKey, out var scrollPos))
                scrollPos = Vector2.zero;

            // Start a vertical group that will contain the scroll view
            EditorGUILayout.BeginVertical(expandHeight ? GUILayout.ExpandHeight(true) : GUILayout.ExpandHeight(false));

            // Create a scroll view that will expand based on parameter - never show horizontal scrollbar
            var newScrollPos = EditorGUILayout.BeginScrollView(
                scrollPos,
                GUIStyle.none, // No horizontal scrollbar
                GUI.skin.verticalScrollbar, // Use default vertical scrollbar
                expandHeight ? GUILayout.ExpandHeight(true) : GUILayout.ExpandHeight(false),
                GUILayout.ExpandWidth(true)
            );

            // Make sure we update the scroll position for this content area
            _scrollPositions[scrollKey] = newScrollPos;

            // Handle mouse wheel events explicitly to ensure scrolling works
            var currentEvent = Event.current;

            if (currentEvent.type == EventType.ScrollWheel &&
                _scrollPositions.ContainsKey(scrollKey))
            {
                var storedPos = _scrollPositions[scrollKey];
                storedPos.y += currentEvent.delta.y * 20f; // Adjust scroll speed
                _scrollPositions[scrollKey] = storedPos;
                currentEvent.Use(); // Consume the event
                GUI.changed = true;
            }

            // Track this content area as active
            _activeContentAreas.Add(contentId);

            return true;
        }

        /// <summary>
        ///     Ends a content section started with BeginPageContent.
        /// </summary>
        /// <param name="contentId">Must match the contentId parameter used in BeginPageContent</param>
        public static void EndPageContent(string contentId = "default")
        {
            if (_activeContentAreas.Contains(contentId))
            {
                // End the scroll view
                EditorGUILayout.EndScrollView();

                // End the vertical group
                EditorGUILayout.EndVertical();

                // Remove this content area from active tracking
                _activeContentAreas.Remove(contentId);
            }
        }

        /// <summary>
        ///     Begins a footer section with a separator line.
        ///     Must be paired with EndPageFooter.
        /// </summary>
        public static void BeginPageFooter()
        {
            DrawSeparator();
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
        }

        /// <summary>
        ///     Ends a footer section started with BeginPageFooter.
        /// </summary>
        public static void EndPageFooter()
        {
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        /// <summary>
        ///     Helper to draw a visual separator line.
        /// </summary>
        public static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, FooterBorderColor);
        }

        /// <summary>
        ///     Creates a Cancel button that closes the current page when clicked.
        /// </summary>
        public static bool DrawCancelButton(float width = 0)
        {
            var options = width > 0
                ? new[] { GUILayout.Height(30), GUILayout.Width(width) }
                : new[] { GUILayout.Height(30) };

            if (GUILayout.Button("Cancel", options))
            {
                ToolsHubManager.ClosePage(PageOperationResult.Cancelled);

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Creates an action button with appropriate styling.
        /// </summary>
        public static bool DrawActionButton(string label, float width = 0)
        {
            var options = width > 0
                ? new[] { GUILayout.Height(30), GUILayout.Width(width) }
                : new[] { GUILayout.Height(30) };

            return GUILayout.Button(label, options);
        }

        // ========= Standardized, wrapping footer buttons =========
        public enum FooterButtonStyle { Primary, Secondary, Danger }

        public struct FooterButton
        {
            public string Label;
            public Action OnClick;
            public FooterButtonStyle Style;
            public bool Enabled;
            public float MinWidth; // 0 = auto
        }

        public static void DrawFooterButtons(IEnumerable<FooterButton> buttons, bool alignRight = false,
            int maxPerRow = 3, float gap = 8f, float height = 30f)
        {
            if (buttons == null) return;

            var list = new List<FooterButton>(buttons);
            if (list.Count == 0) return;

            // Measure widths
            var style = GUI.skin.button;
            var measured = new float[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                var content = new GUIContent(list[i].Label);
                measured[i] = Mathf.Ceil(style.CalcSize(content).x) + 6f; // padding
                if (list[i].MinWidth > 0)
                    measured[i] = Mathf.Max(measured[i], list[i].MinWidth);
            }

            var idx = 0;
            var total = list.Count;

            while (idx < total)
            {
                var availableWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 24f);
                var remaining = total - idx;
                var countThisRow = Mathf.Min(maxPerRow, remaining);

                // Fit as many as possible based on measured widths
                while (countThisRow > 1)
                {
                    var candidateWidth = (availableWidth - gap * (countThisRow - 1));
                    float required = 0f;
                    for (var k = 0; k < countThisRow; k++) required += measured[idx + k];

                    if (candidateWidth + 0.5f >= required) break;
                    countThisRow--;
                }

                // Row
                EditorGUILayout.BeginHorizontal();
                if (alignRight) GUILayout.FlexibleSpace();

                for (var i = 0; i < countThisRow && idx < total; i++)
                {
                    var b = list[idx];
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = b.Style switch
                    {
                        FooterButtonStyle.Primary => PrimaryButtonColor,
                        FooterButtonStyle.Danger => DangerButtonColor,
                        _ => SecondaryButtonColor
                    };

                    EditorGUI.BeginDisabledGroup(!b.Enabled);
                    if (GUILayout.Button(b.Label, GUILayout.Height(height))) b.OnClick?.Invoke();
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = prev;

                    if (i < countThisRow - 1) GUILayout.Space(gap);
                    idx++;
                }

                if (!alignRight) GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
        }

        /// <summary>
        /// Draws a split footer: left-aligned buttons (typically Cancel/Close) on the left,
        /// and other action buttons right-aligned on the right, flowing right-to-left.
        /// Extra right-side buttons wrap onto additional right-aligned rows below.
        /// </summary>
        public static void DrawFooterSplit(IEnumerable<FooterButton> leftButtons,
            IEnumerable<FooterButton> rightButtons,
            float gap = 8f, float height = 30f)
        {
            GUILayout.Space(8);

            var leftList = leftButtons != null ? new List<FooterButton>(leftButtons) : new List<FooterButton>();
            var rightList = rightButtons != null ? new List<FooterButton>(rightButtons) : new List<FooterButton>();

            // First row: Left-most row with left buttons and as many right buttons as fit on the right
            // Measure left row width (single row, no wrapping)
            float MeasureButtonWidth(FooterButton b)
            {
                var w = Mathf.Ceil(GUI.skin.button.CalcSize(new GUIContent(b.Label)).x) + 6f;
                if (b.MinWidth > 0) w = Mathf.Max(w, b.MinWidth);

                return w;
            }

            float leftWidth = 0f;
            if (leftList.Count > 0)
            {
                for (var i = 0; i < leftList.Count; i++)
                    leftWidth += MeasureButtonWidth(leftList[i]) + (i == 0 ? 0f : gap);
            }

            // Measure all right buttons
            var rightWidths = new float[rightList.Count];
            for (var i = 0; i < rightList.Count; i++) rightWidths[i] = MeasureButtonWidth(rightList[i]);

            // Determine how many right buttons fit on the first row
            var availableWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 24f);
            // Reserve some room for spacing between left and right groups
            var firstRowAvailableForRight = Mathf.Max(0f, availableWidth - leftWidth - gap);

            // Accumulate widths from left to right (but render right-to-left). We'll pick as many as we can.
            int firstRowCount = 0;
            float used = 0f;
            for (var i = 0; i < rightList.Count; i++)
            {
                var add = rightWidths[i] + (firstRowCount == 0 ? 0f : gap);
                if (used + add <= firstRowAvailableForRight) { used += add; firstRowCount++; }
                else break;
            }

            // First row
            EditorGUILayout.BeginHorizontal();

            // Left group (render in natural order)
            if (leftList.Count > 0)
            {
                for (var i = 0; i < leftList.Count; i++)
                {
                    DrawFooterButton(leftList[i], height);
                    if (i < leftList.Count - 1) GUILayout.Space(gap);
                }
            }

            GUILayout.FlexibleSpace();

            // Right group first row (render in natural order, aligned right; visually appears right-to-left)
            if (firstRowCount > 0)
            {
                // To ensure tight packing at the right, draw a flexible space first
                // Note: we’re already inside a horizontal with a FlexibleSpace before this block
                for (var i = 0; i < firstRowCount; i++)
                {
                    if (i > 0) GUILayout.Space(gap);
                    DrawFooterButton(rightList[i], height);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Additional rows for remaining right-side buttons
            var idx = firstRowCount;
            while (idx < rightList.Count)
            {
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // Fit as many as possible in this row
                used = 0f;
                var startIdx = idx;
                var countThisRow = 0;

                while (idx < rightList.Count)
                {
                    var add = rightWidths[idx] + (countThisRow == 0 ? 0f : gap);
                    if (used + add <= availableWidth) { used += add; countThisRow++; idx++; }
                    else break;
                }

                // Draw this row
                for (var i = 0; i < countThisRow; i++)
                {
                    if (i > 0) GUILayout.Space(gap);
                    DrawFooterButton(rightList[startIdx + i], height);
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
        }

        private static void DrawFooterButton(FooterButton b, float height)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = b.Style switch
            {
                FooterButtonStyle.Primary => PrimaryButtonColor,
                FooterButtonStyle.Danger => DangerButtonColor,
                _ => SecondaryButtonColor
            };

            EditorGUI.BeginDisabledGroup(!b.Enabled);
            if (GUILayout.Button(b.Label, GUILayout.Height(height))) b.OnClick?.Invoke();
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = prev;
        }
    }
}
#endif