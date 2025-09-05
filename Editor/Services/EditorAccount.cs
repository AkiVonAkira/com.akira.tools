#if UNITY_EDITOR
using System;
using System.Reflection;

namespace akira.EditorServices
{
    // Lightweight helper for detecting Unity Editor sign-in state without taking a hard dependency
    public static class EditorAccount
    {
        public static bool IsSignedIn()
        {
            try
            {
                var type = Type.GetType("UnityEditor.Connect.UnityConnect, UnityEditor", throwOnError: false);
                if (type == null) return false;
                var instanceProp = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null) return false;
                var loggedInProp = type.GetProperty("loggedIn", BindingFlags.Public | BindingFlags.Instance);
                if (loggedInProp == null) return false;
                var val = loggedInProp.GetValue(instance);
                return val is bool b && b;
            }
            catch { return false; }
        }
    }
}
#endif

