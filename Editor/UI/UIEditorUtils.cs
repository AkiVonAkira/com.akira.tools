#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace akira.UI
{
    /// <summary>
    /// Shared lightweight IMGUI utilities used across Tools Hub pages.
    /// </summary>
    public static class UIEditorUtils
    {
        // Shared theme (kept in sync with ToolsHubManager foldout styling)
        public static readonly Color FoldoutBgColor = new(0.18f, 0.18f, 0.18f, 1f);
        public static readonly Color FoldoutBorderColor = new(0.35f, 0.35f, 0.35f, 1f);
        public static readonly Color DividerColor = new(0.25f, 0.25f, 0.25f, 1f);

        private static Texture2D _solidTexture;
        private static Color _solidTextureColor;

        /// <summary>
        /// Returns a 1x1 solid color texture (cached by last color used).
        /// </summary>
        public static Texture2D GetSolidTexture(Color color)
        {
            if (_solidTexture == null)
            {
                _solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _solidTextureColor = new Color(0, 0, 0, 0);
            }

            if (_solidTextureColor != color)
            {
                _solidTexture.SetPixel(0, 0, color);
                _solidTexture.Apply(false, true);
                _solidTextureColor = color;
            }

            return _solidTexture;
        }

        /// <summary>
        /// Draws a 1px (or thicker) border around a rect.
        /// </summary>
        public static void DrawRectBorder(Rect rect, Color color, int thickness = 1)
        {
            if (thickness <= 0) thickness = 1;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        }

        /// <summary>
        /// Draws a thin divider line across full width.
        /// </summary>
        public static void DrawDividerLine(Color? color = null)
        {
            var rect = EditorGUILayout.GetControlRect(false, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, color ?? DividerColor);
        }
    }
}
#endif
