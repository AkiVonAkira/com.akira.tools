#if UNITY_EDITOR
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
                fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }
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
                ToolsHubManger.ClosePage(PageOperationResult.Cancelled);

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
    }
}
#endif