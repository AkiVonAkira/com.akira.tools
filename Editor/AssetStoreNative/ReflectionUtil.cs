#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;

namespace akira.AssetStoreNative
{
    internal static class ReflectionUtil
    {
        public static Assembly UnityEditorAssembly => typeof(UnityEditor.Editor).Assembly;

        public static Type FindType(string fullName)
        {
            // Try exact match first
            var t = UnityEditorAssembly.GetType(fullName, throwOnError: false);
            if (t != null) return t;
            // Fallback: search all loaded assemblies (editor domain)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(fullName, throwOnError: false); } catch { t = null; }
                if (t != null) return t;
            }
            return null;
        }

        public static object CreateInstance(Type t, params object[] args)
        {
            if (t == null) return null;
            try { return Activator.CreateInstance(t, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, binder: null, args: args, culture: null); }
            catch { return null; }
        }

        public static object GetEnumValue(string enumFullName, string name)
        {
            var et = FindType(enumFullName);
            if (et == null || !et.IsEnum) return null;
            try { return Enum.Parse(et, name); }
            catch { return null; }
        }

        public static object Call(object target, string methodName, params object[] args)
        {
            if (target == null) return null;
            var t = target is Type type ? type : target.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var mi = t.GetMethods(flags).FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == (args?.Length ?? 0));
            if (mi == null)
            {
                // Fallback: first with same name
                mi = t.GetMethods(flags).FirstOrDefault(m => m.Name == methodName);
            }
            if (mi == null) return null;
            try { return mi.Invoke(target is Type ? null : target, args); }
            catch { return null; }
        }

        public static object GetProp(object target, string propName)
        {
            if (target == null) return null;
            var t = target is Type type ? type : target.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var pi = t.GetProperty(propName, flags);
            if (pi == null) return null;
            try { return pi.GetValue(target is Type ? null : target); }
            catch { return null; }
        }

        public static object GetField(object target, string fieldName)
        {
            if (target == null) return null;
            var t = target is Type type ? type : target.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var fi = t.GetField(fieldName, flags);
            if (fi == null) return null;
            try { return fi.GetValue(target is Type ? null : target); }
            catch { return null; }
        }
    }
}
#endif

