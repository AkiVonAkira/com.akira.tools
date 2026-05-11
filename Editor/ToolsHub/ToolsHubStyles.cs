#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Akira.ToolsHub
{
    /// <summary>
    /// Centralized styling for ToolsHub Manager UI
    /// All colors, styles, and visual constants in one place for consistency
    /// </summary>
    public static class ToolsHubStyles
    {
        // Color Palette (matching ToolsHubWindow aesthetic)
        public static readonly Color HeaderBgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public static readonly Color TabBarBgColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        public static readonly Color StatusBarBgColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        public static readonly Color ContentBgColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        
        public static readonly Color FoldoutBgColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        public static readonly Color FoldoutBorderColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        public static readonly Color DividerColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        
        public static readonly Color ButtonNormalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        public static readonly Color ButtonHoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        public static readonly Color ButtonActiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public static readonly Color ButtonTextColor = Color.white;
        
        public static readonly Color TitleTextColor = Color.white;
        public static readonly Color SubtitleTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        public static readonly Color StatusTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        
        // Measurements
        public const float HeaderPadding = 15f;
        public const float ContentPadding = 20f;
        public const float StatusBarPadding = 8f;
        public const float TabButtonHeight = 35f;
        public const float MenuButtonHeight = 28f;
        public const float FoldoutHeight = 24f;
        
        public const float ButtonGap = 8f;
        public const float SideGap = 4f;
        public const int MaxButtonsPerRow = 4;
        
        // Cached Textures
        private static Texture2D _normalBg;
        private static Texture2D _hoverBg;
        private static Texture2D _activeBg;
        private static Texture2D _headerBg;
        private static Texture2D _statusBarBg;
        private static Texture2D _popupIcon;
        
        // Cached Styles
        private static GUIStyle _headerTitleStyle;
        private static GUIStyle _headerSubtitleStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _compactFoldoutStyle;
        private static GUIStyle _statusLabelStyle;
        private static GUIStyle _pageTitleStyle;
        private static GUIStyle _toolbarStyle;
        
        #region Textures
        
        public static Texture2D NormalBg
        {
            get
            {
                if (_normalBg == null)
                    _normalBg = CreateColorTexture(ButtonNormalColor);
                return _normalBg;
            }
        }
        
        public static Texture2D HoverBg
        {
            get
            {
                if (_hoverBg == null)
                    _hoverBg = CreateColorTexture(ButtonHoverColor);
                return _hoverBg;
            }
        }
        
        public static Texture2D ActiveBg
        {
            get
            {
                if (_activeBg == null)
                    _activeBg = CreateColorTexture(ButtonActiveColor);
                return _activeBg;
            }
        }
        
        public static Texture2D HeaderBg
        {
            get
            {
                if (_headerBg == null)
                    _headerBg = CreateColorTexture(HeaderBgColor);
                return _headerBg;
            }
        }
        
        public static Texture2D StatusBarBg
        {
            get
            {
                if (_statusBarBg == null)
                    _statusBarBg = CreateColorTexture(StatusBarBgColor);
                return _statusBarBg;
            }
        }
        
        public static Texture2D PopupIcon
        {
            get
            {
                if (_popupIcon == null)
                {
                    var iconPath = "Assets/popup.png";
                    _popupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                }
                return _popupIcon;
            }
        }
        
        #endregion
        
        #region Styles
        
        public static GUIStyle HeaderTitleStyle
        {
            get
            {
                if (_headerTitleStyle == null)
                {
                    _headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 20,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = TitleTextColor },
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                }
                return _headerTitleStyle;
            }
        }
        
        public static GUIStyle HeaderSubtitleStyle
        {
            get
            {
                if (_headerSubtitleStyle == null)
                {
                    _headerSubtitleStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = SubtitleTextColor },
                        padding = new RectOffset(0, 0, 2, 0)
                    };
                }
                return _headerSubtitleStyle;
            }
        }
        
        public static GUIStyle ButtonStyle
        {
            get
            {
                if (_buttonStyle == null)
                {
                    _buttonStyle = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12,
                        fontStyle = FontStyle.Normal,
                        normal = { background = NormalBg, textColor = ButtonTextColor },
                        hover = { background = HoverBg, textColor = ButtonTextColor },
                        active = { background = ActiveBg, textColor = ButtonTextColor },
                        padding = new RectOffset(8, 8, 4, 4),
                        margin = new RectOffset(0, 0, 0, 0),
                        border = new RectOffset(4, 4, 4, 4),
                        overflow = new RectOffset(0, 0, 0, 0),
                        wordWrap = false,
                        clipping = TextClipping.Clip
                    };
                }
                return _buttonStyle;
            }
        }
        
        public static GUIStyle CompactFoldoutStyle
        {
            get
            {
                if (_compactFoldoutStyle == null)
                {
                    _compactFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white },
                        onNormal = { textColor = Color.white },
                        focused = { textColor = Color.white },
                        onFocused = { textColor = Color.white },
                        padding = new RectOffset(18, 4, 2, 2),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                }
                return _compactFoldoutStyle;
            }
        }
        
        public static GUIStyle StatusLabelStyle
        {
            get
            {
                if (_statusLabelStyle == null)
                {
                    _statusLabelStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = StatusTextColor }
                    };
                }
                return _statusLabelStyle;
            }
        }
        
        public static GUIStyle PageTitleStyle
        {
            get
            {
                if (_pageTitleStyle == null)
                {
                    _pageTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 16,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = TitleTextColor },
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(0, 0, 4, 4)
                    };
                }
                return _pageTitleStyle;
            }
        }
        
        public static GUIStyle ToolbarStyle
        {
            get
            {
                if (_toolbarStyle == null)
                {
                    _toolbarStyle = new GUIStyle(EditorStyles.toolbar)
                    {
                        fixedHeight = 22
                    };
                }
                return _toolbarStyle;
            }
        }
        
        #endregion
        
        #region Helpers
        
        private static Texture2D CreateColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply(false, true);
            return tex;
        }
        
        public static void ClearCache()
        {
            _normalBg = null;
            _hoverBg = null;
            _activeBg = null;
            _headerBg = null;
            _statusBarBg = null;
            _headerTitleStyle = null;
            _headerSubtitleStyle = null;
            _buttonStyle = null;
            _compactFoldoutStyle = null;
            _statusLabelStyle = null;
            _pageTitleStyle = null;
            _toolbarStyle = null;
        }
        
        #endregion
    }
}
#endif
