using System;
using System.Collections.Generic;
using UnityEngine;

namespace Akira.Tools.Core
{
    /// <summary>
    /// Centralized error handling system with retry logic and user-friendly messages
    /// </summary>
    public static class ErrorHandler
    {
        private const string LOG_PREFIX = "[AkiraTools]";
        private static bool debugMode = true;
        
        public static bool DebugMode
        {
            get => debugMode;
            set => debugMode = value;
        }

        #region Logging Methods

        /// <summary>
        /// Log an error message with the AkiraTools prefix
        /// </summary>
        public static void LogError(string message, UnityEngine.Object context = null)
        {
            Debug.LogError($"{LOG_PREFIX} {message}", context);
        }

        /// <summary>
        /// Log a warning message with the AkiraTools prefix
        /// </summary>
        public static void LogWarning(string message, UnityEngine.Object context = null)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}", context);
        }

        /// <summary>
        /// Log an info message with the AkiraTools prefix
        /// </summary>
        public static void Log(string message, UnityEngine.Object context = null)
        {
            if (debugMode)
            {
                Debug.Log($"{LOG_PREFIX} {message}", context);
            }
        }

        /// <summary>
        /// Log an exception with additional context
        /// </summary>
        public static void LogException(Exception exception, string context = "", UnityEngine.Object unityContext = null)
        {
            string message = string.IsNullOrEmpty(context) 
                ? $"Exception occurred: {exception.Message}" 
                : $"Exception in {context}: {exception.Message}";
            
            Debug.LogError($"{LOG_PREFIX} {message}\n{exception.StackTrace}", unityContext);
        }

        #endregion

        #region Try-Catch Wrappers

        /// <summary>
        /// Execute an action with error handling
        /// </summary>
        public static void Try(Action action, Action<Exception> onError = null, string context = "")
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                LogException(ex, context);
                onError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Execute a function with error handling and return result
        /// </summary>
        public static T Try<T>(Func<T> func, T defaultValue = default, Action<Exception> onError = null, string context = "")
        {
            try
            {
                return func != null ? func.Invoke() : defaultValue;
            }
            catch (Exception ex)
            {
                LogException(ex, context);
                onError?.Invoke(ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Execute an async action with error handling
        /// </summary>
        public static async System.Threading.Tasks.Task TryAsync(
            Func<System.Threading.Tasks.Task> asyncAction, 
            Action<Exception> onError = null, 
            string context = "")
        {
            try
            {
                if (asyncAction != null)
                {
                    await asyncAction.Invoke();
                }
            }
            catch (Exception ex)
            {
                LogException(ex, context);
                onError?.Invoke(ex);
            }
        }

        #endregion

        #region Retry Logic

        /// <summary>
        /// Execute an action with retry logic for network operations
        /// </summary>
        public static void TryWithRetry(
            Action action,
            int maxRetries = 3,
            int delayMs = 1000,
            Action<Exception> onFinalError = null,
            string context = "")
        {
            Exception lastException = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Log($"Attempt {attempt}/{maxRetries} for: {context}");
                    action?.Invoke();
                    return; // Success
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxRetries)
                    {
                        LogWarning($"Attempt {attempt} failed, retrying in {delayMs}ms: {ex.Message}");
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }
            
            // All retries failed
            LogError($"All {maxRetries} attempts failed for: {context}");
            if (lastException != null)
            {
                LogException(lastException, context);
                onFinalError?.Invoke(lastException);
            }
        }

        /// <summary>
        /// Execute a function with retry logic and return result
        /// </summary>
        public static T TryWithRetry<T>(
            Func<T> func,
            T defaultValue = default,
            int maxRetries = 3,
            int delayMs = 1000,
            Action<Exception> onFinalError = null,
            string context = "")
        {
            Exception lastException = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Log($"Attempt {attempt}/{maxRetries} for: {context}");
                    return func != null ? func.Invoke() : defaultValue;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxRetries)
                    {
                        LogWarning($"Attempt {attempt} failed, retrying in {delayMs}ms: {ex.Message}");
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }
            
            // All retries failed
            LogError($"All {maxRetries} attempts failed for: {context}");
            if (lastException != null)
            {
                LogException(lastException, context);
                onFinalError?.Invoke(lastException);
            }
            
            return defaultValue;
        }

        #endregion

        #region User-Friendly Error Messages

        private static readonly Dictionary<string, string> ErrorCodeMessages = new Dictionary<string, string>
        {
            { "PKG001", "Failed to resolve package dependencies. Please check your manifest.json for conflicts." },
            { "PKG002", "The Git URL provided is invalid. Please verify the URL format." },
            { "PKG003", "Package installation failed. Please check your internet connection." },
            { "FLD001", "Failed to create folder structure. Please check write permissions." },
            { "FLD002", "Folder already exists. Please choose a different location." },
            { "NET001", "Network operation timed out. Please check your internet connection." },
            { "NET002", "Maximum retry attempts reached. Please try again later." },
            { "REF001", "Reflection operation failed. The target method or field may not exist." },
            { "REF002", "Assembly not found. Please ensure all dependencies are installed." }
        };

        /// <summary>
        /// Get user-friendly error message for error code
        /// </summary>
        public static string GetErrorMessage(string errorCode)
        {
            if (ErrorCodeMessages.TryGetValue(errorCode, out string message))
            {
                return message;
            }
            return "An unknown error occurred. Please check the console for details.";
        }

        /// <summary>
        /// Log error with error code and user-friendly message
        /// </summary>
        public static void LogErrorWithCode(string errorCode, string additionalInfo = "")
        {
            string message = GetErrorMessage(errorCode);
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                message += $"\nDetails: {additionalInfo}";
            }
            LogError($"[{errorCode}] {message}");
        }

        #endregion

        #region Reflection Helpers

        /// <summary>
        /// Safely invoke a method using reflection with fallback
        /// </summary>
        public static object SafeReflectionInvoke(
            object target,
            string methodName,
            object[] parameters = null,
            Action fallback = null)
        {
            return Try(() =>
            {
                var method = target.GetType().GetMethod(methodName,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (method == null)
                {
                    LogWarning($"Method '{methodName}' not found via reflection. Using fallback.");
                    fallback?.Invoke();
                    return null;
                }

                return method.Invoke(target, parameters);
            },
            defaultValue: null,
            onError: (ex) =>
            {
                LogErrorWithCode("REF001", $"Method: {methodName}");
                fallback?.Invoke();
            },
            context: $"Reflection invoke: {methodName}");
        }

        /// <summary>
        /// Safely get a field value using reflection with fallback
        /// </summary>
        public static T SafeReflectionGetField<T>(
            object target,
            string fieldName,
            T defaultValue = default,
            Action fallback = null)
        {
            return Try(() =>
            {
                var field = target.GetType().GetField(fieldName,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (field == null)
                {
                    LogWarning($"Field '{fieldName}' not found via reflection. Using fallback.");
                    fallback?.Invoke();
                    return defaultValue;
                }

                return (T)field.GetValue(target);
            },
            defaultValue: defaultValue,
            onError: (ex) =>
            {
                LogErrorWithCode("REF001", $"Field: {fieldName}");
                fallback?.Invoke();
            },
            context: $"Reflection get field: {fieldName}");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate a condition and log error if false
        /// </summary>
        public static bool Validate(bool condition, string errorMessage)
        {
            if (!condition)
            {
                LogError(errorMessage);
            }
            return condition;
        }

        /// <summary>
        /// Validate object is not null
        /// </summary>
        public static bool ValidateNotNull(object obj, string objectName)
        {
            bool isValid = obj != null;
            if (!isValid)
            {
                LogError($"{objectName} is null. Operation cannot continue.");
            }
            return isValid;
        }

        /// <summary>
        /// Validate string is not null or empty
        /// </summary>
        public static bool ValidateNotEmpty(string str, string paramName)
        {
            bool isValid = !string.IsNullOrEmpty(str);
            if (!isValid)
            {
                LogError($"{paramName} is null or empty. Operation cannot continue.");
            }
            return isValid;
        }

        #endregion
    }
}

