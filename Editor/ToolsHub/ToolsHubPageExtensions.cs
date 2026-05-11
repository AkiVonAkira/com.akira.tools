#if UNITY_EDITOR
using System;
using Akira.UI;
using UnityEditor;
using UnityEngine;

namespace Akira.ToolsHub
{
    /// <summary>
    /// Extension methods for IToolsHubPage.
    /// Handles ALL layout responsibility - pages just provide content!
    /// </summary>
    public static class ToolsHubPageExtensions
    {
        private static Vector2 _scrollPosition;

        /// <summary>
        /// Show this page in the ToolsHub Manager
        /// </summary>
        public static void ShowInToolsHub(this IToolsHubPage page)
        {
            _scrollPosition = Vector2.zero; // Reset scroll when opening page
            ToolsHubManager.ShowPage(page.Title, () => page.DrawPage(), page.OnPageResult);
        }

        /// <summary>
        /// Draw the complete page with proper layout.
        /// This extension handles ALL layout - pages just provide content!
        /// </summary>
        public static void DrawPage(this IToolsHubPage page)
        {
            DrawPageHeader(() => page.DrawHeader());
            GUILayout.Space(8);
            DrawPageContent(() => page.DrawContent());
            GUILayout.Space(8);
            DrawPageContentFooter(() => page.DrawContentFooter());
            GUILayout.Space(8);
            DrawPageFooter(() => page.DrawFooter());
        }

        private static void DrawPageHeader(Action drawContent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();
            
            drawContent?.Invoke();
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawPageContent(Action drawContent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            drawContent?.Invoke();
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawPageContentFooter(Action drawContent)
        {
            // Sticky content footer - appears below scroll view, above action buttons
            // Use for forms, extended UI like add package form, preset manager, etc.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();
            
            drawContent?.Invoke();
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawPageFooter(Action drawContent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();
            
            drawContent?.Invoke();
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif