using System;
using UnityEngine;

namespace Akira.Tools.Core
{
    /// <summary>
    /// Settings manager for Akira Tools
    /// Handles persistent settings storage and retrieval
    /// </summary>
    public static class ToolsSettings
    {
        private const string PREFS_PREFIX = "AkiraTools_";
        
        // Setting keys
        private const string DEBUG_MODE_KEY = PREFS_PREFIX + "DebugMode";
        private const string AUTO_REFRESH_KEY = PREFS_PREFIX + "AutoRefresh";
        private const string MAX_RETRIES_KEY = PREFS_PREFIX + "MaxRetries";
        private const string RETRY_DELAY_KEY = PREFS_PREFIX + "RetryDelay";
        private const string SHOW_NOTIFICATIONS_KEY = PREFS_PREFIX + "ShowNotifications";
        private const string RECENT_PACKAGES_KEY = PREFS_PREFIX + "RecentPackages";
        private const string DEFAULT_ROOT_PATH_KEY = PREFS_PREFIX + "DefaultRootPath";
        private const string LAST_PRESET_KEY = PREFS_PREFIX + "LastPreset";

        #region General Settings

        /// <summary>
        /// Enable or disable debug logging
        /// </summary>
        public static bool DebugMode
        {
            get => UnityEditor.EditorPrefs.GetBool(DEBUG_MODE_KEY, false);
            set
            {
                UnityEditor.EditorPrefs.SetBool(DEBUG_MODE_KEY, value);
                ErrorHandler.DebugMode = value;
            }
        }

        /// <summary>
        /// Enable or disable auto-refresh after operations
        /// </summary>
        public static bool AutoRefresh
        {
            get => UnityEditor.EditorPrefs.GetBool(AUTO_REFRESH_KEY, true);
            set => UnityEditor.EditorPrefs.SetBool(AUTO_REFRESH_KEY, value);
        }

        /// <summary>
        /// Show notification popups for operations
        /// </summary>
        public static bool ShowNotifications
        {
            get => UnityEditor.EditorPrefs.GetBool(SHOW_NOTIFICATIONS_KEY, true);
            set => UnityEditor.EditorPrefs.SetBool(SHOW_NOTIFICATIONS_KEY, value);
        }

        #endregion

        #region Network Settings

        /// <summary>
        /// Maximum number of retry attempts for network operations
        /// </summary>
        public static int MaxRetries
        {
            get => UnityEditor.EditorPrefs.GetInt(MAX_RETRIES_KEY, 3);
            set => UnityEditor.EditorPrefs.SetInt(MAX_RETRIES_KEY, Mathf.Clamp(value, 1, 10));
        }

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        public static int RetryDelay
        {
            get => UnityEditor.EditorPrefs.GetInt(RETRY_DELAY_KEY, 1000);
            set => UnityEditor.EditorPrefs.SetInt(RETRY_DELAY_KEY, Mathf.Clamp(value, 100, 10000));
        }

        #endregion

        #region Folder Settings

        /// <summary>
        /// Default root path for folder creation
        /// </summary>
        public static string DefaultRootPath
        {
            get => UnityEditor.EditorPrefs.GetString(DEFAULT_ROOT_PATH_KEY, "Assets/_Project");
            set => UnityEditor.EditorPrefs.SetString(DEFAULT_ROOT_PATH_KEY, value);
        }

        /// <summary>
        /// Last used preset name
        /// </summary>
        public static string LastPreset
        {
            get => UnityEditor.EditorPrefs.GetString(LAST_PRESET_KEY, "Standard Project");
            set => UnityEditor.EditorPrefs.SetString(LAST_PRESET_KEY, value);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Reset all settings to default values
        /// </summary>
        public static void ResetToDefaults()
        {
            UnityEditor.EditorPrefs.DeleteKey(DEBUG_MODE_KEY);
            UnityEditor.EditorPrefs.DeleteKey(AUTO_REFRESH_KEY);
            UnityEditor.EditorPrefs.DeleteKey(MAX_RETRIES_KEY);
            UnityEditor.EditorPrefs.DeleteKey(RETRY_DELAY_KEY);
            UnityEditor.EditorPrefs.DeleteKey(SHOW_NOTIFICATIONS_KEY);
            UnityEditor.EditorPrefs.DeleteKey(DEFAULT_ROOT_PATH_KEY);
            UnityEditor.EditorPrefs.DeleteKey(LAST_PRESET_KEY);
            
            ErrorHandler.Log("Settings reset to defaults");
        }

        /// <summary>
        /// Export settings to JSON
        /// </summary>
        public static string ExportSettings()
        {
            var settings = new SettingsData
            {
                DebugMode = DebugMode,
                AutoRefresh = AutoRefresh,
                ShowNotifications = ShowNotifications,
                MaxRetries = MaxRetries,
                RetryDelay = RetryDelay,
                DefaultRootPath = DefaultRootPath,
                LastPreset = LastPreset
            };

            return JsonUtility.ToJson(settings, true);
        }

        /// <summary>
        /// Import settings from JSON
        /// </summary>
        public static bool ImportSettings(string json)
        {
            try
            {
                var settings = JsonUtility.FromJson<SettingsData>(json);
                
                DebugMode = settings.DebugMode;
                AutoRefresh = settings.AutoRefresh;
                ShowNotifications = settings.ShowNotifications;
                MaxRetries = settings.MaxRetries;
                RetryDelay = settings.RetryDelay;
                DefaultRootPath = settings.DefaultRootPath;
                LastPreset = settings.LastPreset;

                ErrorHandler.Log("Settings imported successfully");
                return true;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError($"Failed to import settings: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    [Serializable]
    internal class SettingsData
    {
        public bool DebugMode;
        public bool AutoRefresh;
        public bool ShowNotifications;
        public int MaxRetries;
        public int RetryDelay;
        public string DefaultRootPath;
        public string LastPreset;
    }
}

