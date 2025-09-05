#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace akira.AssetStoreNative
{
    // Reflection bridge into UnityEditor.PackageManager.UI.Internal Asset Store services
    internal static class AssetStoreBridge
    {
        private static object _servicesContainer; // UnityEditor.PackageManager.UI.Internal.ServicesContainer.instance
        private static object _assetStoreRestApi; // UnityEditor.PackageManager.UI.Internal.IAssetStoreRestAPI
        private static Type _typeServicesContainer;
        private static Type _typeIAssetStoreRestAPI;

        private static bool Ensure()
        {
            if (_assetStoreRestApi != null) return true;
            // Resolve services container
            _typeServicesContainer = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.ServicesContainer");
            _typeIAssetStoreRestAPI = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreRestAPI");
            if (_typeServicesContainer == null || _typeIAssetStoreRestAPI == null)
                return false;

            // Try ScriptableSingleton<ServicesContainer>.instance first (static generic base)
            try
            {
                var tSingletonGeneric = ReflectionUtil.FindType("UnityEditor.ScriptableSingleton`1");
                if (tSingletonGeneric != null)
                {
                    var tSingleton = tSingletonGeneric.MakeGenericType(_typeServicesContainer);
                    var instPropGeneric = tSingleton.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _servicesContainer = instPropGeneric?.GetValue(null);
                }
            }
            catch { _servicesContainer = null; }

            // Fallback: try derived static properties (some versions expose them)
            if (_servicesContainer == null)
            {
                var instProp = _typeServicesContainer.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                _servicesContainer = instProp?.GetValue(null);
                if (_servicesContainer == null)
                {
                    instProp = _typeServicesContainer.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _servicesContainer = instProp?.GetValue(null);
                }
            }

            // IMPORTANT: do NOT create a new ServicesContainer instance; we must use Unity's singleton
            if (_servicesContainer == null) return false;

            // Resolve IAssetStoreRestAPI via generic Resolve<T>()
            object api = null;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric != null)
            {
                try
                {
                    var gm1 = resolveGeneric.MakeGenericMethod(_typeIAssetStoreRestAPI);
                    api = gm1.Invoke(_servicesContainer, null);
                }
                catch { api = null; }

                if (api == null)
                {
                    try
                    {
                        var reload = _typeServicesContainer.GetMethod("Reload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        reload?.Invoke(_servicesContainer, null);
                        var gm2 = resolveGeneric.MakeGenericMethod(_typeIAssetStoreRestAPI);
                        api = gm2.Invoke(_servicesContainer, null);
                    }
                    catch { api = null; }
                }
            }
            _assetStoreRestApi = api;
            return _assetStoreRestApi != null;
        }

        public static bool IsAvailable() => Ensure();

        // Query if user is signed in via Unity Connect
        public static bool IsSignedIn()
        {
            try
            {
                var tUc = ReflectionUtil.FindType("UnityEditor.Connect.UnityConnect");
                var instance = tUc?.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                if (instance == null) return false;
                var loggedIn = tUc.GetProperty("loggedIn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance) as bool?;
                if (loggedIn.HasValue) return loggedIn.Value;
                var isUserLoggedIn = tUc.GetProperty("isUserLoggedIn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance) as bool?;
                return isUserLoggedIn ?? false;
            }
            catch { return false; }
        }

        // Get product detail info
        public static void GetProductDetail(long productId, Action<Dictionary<string, object>> onOk, Action<string> onErr = null)
        {
            if (!Ensure()) { onErr?.Invoke("AssetStore service unavailable."); return; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var mi = _assetStoreRestApi.GetType().GetMethod("GetProductDetail", flags);
            if (mi == null) { onErr?.Invoke("GetProductDetail not found."); return; }

            var productInfoType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreProductInfo");
            var uiErrorType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");
            var typedSuccess = BuildTypedCallback(productInfoType, (obj) =>
            {
                var map = FlattenObject(obj);
                onOk?.Invoke(map);
            });
            var typedError = BuildTypedCallback(uiErrorType, (obj) =>
            {
                var msg = obj?.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj) as string;
                onErr?.Invoke(string.IsNullOrEmpty(msg) ? "Unknown error." : msg);
            });

            mi.Invoke(_assetStoreRestApi, new object[] { productId, typedSuccess, typedError });
        }

        // Robust ownership using purchases endpoint filtered by productId
        public static void IsOwned(long productId, Action<bool> onOk, Action<string> onErr = null)
        {
            GetOwnedProducts(0, 1, new List<long> { productId }, (ids, _) =>
            {
                onOk?.Invoke(ids != null && ids.Contains(productId));
            }, onErr);
        }

        // Get owned product ids using purchases endpoint (paged), optional filter by ids
        public static void GetOwnedProducts(int offset, int limit, List<long> filterProductIds, Action<List<long>, int> onOk, Action<string> onErr = null)
        {
            if (!Ensure()) { onErr?.Invoke("AssetStore service unavailable."); return; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var mi = _assetStoreRestApi.GetType().GetMethod("GetPurchases", flags);
            if (mi == null) { onErr?.Invoke("GetPurchases not found."); return; }
            var purchasesArgsType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.PurchasesQueryArgs");
            var purchasesType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStorePurchases");
            var uiErrorType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");

            var args = CreatePurchasesArgs(purchasesArgsType, offset, limit);
            if (args == null) { onErr?.Invoke("Unable to construct PurchasesQueryArgs."); return; }

            if (filterProductIds != null && filterProductIds.Count > 0)
            {
                // Set productIds list
                var field = purchasesArgsType.GetField("productIds", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && typeof(IEnumerable).IsAssignableFrom(field.FieldType))
                {
                    field.SetValue(args, filterProductIds);
                }
                else
                {
                    TrySetProperty(args, "productIds", filterProductIds);
                }
            }

            var typedSuccess = BuildTypedCallback(purchasesType, (obj) =>
            {
                var ids = TryExtractProductIdsFromPurchases(obj) ?? new List<long>();
                var total = TryGetIntProperty(obj, new[] { "total", "totalResults", "totalCount" });
                onOk?.Invoke(ids, total);
            });
            var typedError = BuildTypedCallback(uiErrorType, (obj) =>
            {
                var msg = obj?.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj) as string;
                onErr?.Invoke(string.IsNullOrEmpty(msg) ? "Unknown error." : msg);
            });

            mi.Invoke(_assetStoreRestApi, new object[] { args, typedSuccess, typedError });
        }

        // Check if user has accepted terms; required before downloads in some cases
        public static void CheckTermsAndConditions(Action<bool> onOk, Action<string> onErr = null)
        {
            if (!Ensure()) { onErr?.Invoke("AssetStore service unavailable."); return; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var mi = _assetStoreRestApi.GetType().GetMethod("CheckTermsAndConditions", flags);
            if (mi == null) { onErr?.Invoke("CheckTermsAndConditions not found."); return; }
            var uiErrorType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");
            var typedSuccess = BuildTypedCallback(typeof(bool), (obj) =>
            {
                var ok = false;
                if (obj is bool b) ok = b; else bool.TryParse(obj?.ToString(), out ok);
                onOk?.Invoke(ok);
            });
            var typedError = BuildTypedCallback(uiErrorType, (obj) =>
            {
                var msg = obj?.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj) as string;
                onErr?.Invoke(string.IsNullOrEmpty(msg) ? "Unknown error." : msg);
            });
            mi.Invoke(_assetStoreRestApi, new object[] { typedSuccess, typedError });
        }

        private static object CreatePurchasesArgs(Type purchasesArgsType, int offset, int limit)
        {
            if (purchasesArgsType == null) return null;
            // Try ctor: (int startIndex = 0, int limit = 0, string searchText = null, PageFilters filters = null)
            var ctors = purchasesArgsType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            object args = null;
            foreach (var ci in ctors)
            {
                var ps = ci.GetParameters();
                if (ps.Length == 4)
                {
                    // Accept either (int,int,string,PageFilters) or (int,long,string,PageFilters)
                    try
                    {
                        var p0 = Convert.ToInt32(offset);
                        var p1 = ps[1].ParameterType == typeof(long) ? (object)Convert.ToInt64(limit) : (object)Convert.ToInt32(limit);
                        object p3 = null; // PageFilters
                        args = ci.Invoke(new object[] { p0, p1, null, p3 });
                        break;
                    }
                    catch { }
                }
            }
            // Fallback: try parameterless (older versions) if present
            if (args == null)
            {
                try { args = Activator.CreateInstance(purchasesArgsType, nonPublic: true); } catch { args = null; }
            }
            if (args != null)
            {
                // Ensure fields are set in case the chosen ctor ignored them
                TrySetProperty(args, "startIndex", offset);
                TrySetProperty(args, "limit", Convert.ToInt64(limit));
            }
            return args;
        }

        // Backward-compatible single call with default paging
        public static void GetOwnedProducts(Action<List<long>> onOk, Action<string> onErr = null)
        {
            GetOwnedProducts(0, 500, null, (ids, _) => onOk?.Invoke(ids), onErr);
        }

        // Request a time-limited download URL for the legacy .unitypackage
        public static void GetDownloadUrl(long productId, Action<string> onOk, Action<string> onErr = null)
        {
            if (!Ensure()) { onErr?.Invoke("AssetStore service unavailable."); return; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var mi = _assetStoreRestApi.GetType().GetMethod("GetDownloadDetail", flags);
            if (mi == null) { onErr?.Invoke("GetDownloadDetail not found."); return; }
            var downloadInfoType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadInfo");
            var uiErrorType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");

            var typedSuccess = BuildTypedCallback(downloadInfoType, (obj) =>
            {
                var url = ExtractStringProperty(obj, new[] { "url", "downloadUrl", "URI", "uri" });
                if (string.IsNullOrEmpty(url))
                    onErr?.Invoke("Download URL not available.");
                else onOk?.Invoke(url);
            });
            var typedError = BuildTypedCallback(uiErrorType, (obj) =>
            {
                var msg = obj?.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj) as string;
                onErr?.Invoke(string.IsNullOrEmpty(msg) ? "Unknown error." : msg);
            });

            mi.Invoke(_assetStoreRestApi, new object[] { productId, typedSuccess, typedError });
        }

        // New: start Unity's native download manager for a product id
        public static void StartNativeDownload(long productId, Action<bool> onOk = null, Action<string> onErr = null)
        {
            if (!Ensure()) { onErr?.Invoke("AssetStore service unavailable."); return; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) { onErr?.Invoke("ServicesContainer.Resolve<T>() not found."); return; }

            // Try resolve IAssetStoreDownloadManager, then concrete type
            var tIManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreDownloadManager");
            var tManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager");
            object mgr = null;
            try
            {
                if (tIManager != null)
                {
                    var gm = resolveGeneric.MakeGenericMethod(tIManager);
                    mgr = gm.Invoke(_servicesContainer, null);
                }
            }
            catch { mgr = null; }
            if (mgr == null && tManager != null)
            {
                try
                {
                    var gm = resolveGeneric.MakeGenericMethod(tManager);
                    mgr = gm.Invoke(_servicesContainer, null);
                }
                catch { mgr = null; }
            }
            if (mgr == null) { onErr?.Invoke("AssetStoreDownloadManager not available."); return; }

            // Find Download(IEnumerable<long>) first, otherwise fallback to private Download(long)
            var mgrType = mgr.GetType();
            MethodInfo miDownloadEnumerable = null;
            MethodInfo miDownloadSingle = null;
            foreach (var m in mgrType.GetMethods(flags).Where(m => m.Name == "Download" && m.GetParameters().Length == 1))
            {
                var p = m.GetParameters()[0].ParameterType;
                if (typeof(IEnumerable).IsAssignableFrom(p) && p != typeof(string))
                {
                    // Prefer something compatible with long[] / IEnumerable<long>
                    if (p.IsAssignableFrom(typeof(long[])) || p.IsAssignableFrom(typeof(List<long>)) || p.IsGenericType)
                    {
                        miDownloadEnumerable = m;
                        break;
                    }
                }
                else if (p == typeof(long) || p == typeof(Int64) || p == typeof(int))
                {
                    miDownloadSingle = m;
                }
            }

            try
            {
                bool started = false;
                if (miDownloadEnumerable != null)
                {
                    started = (bool)(miDownloadEnumerable.Invoke(mgr, new object[] { new long[] { productId } }) ?? false);
                }
                else if (miDownloadSingle != null)
                {
                    miDownloadSingle.Invoke(mgr, new object[] { productId });
                    started = true; // no bool return on single variant
                }
                else
                {
                    onErr?.Invoke("No suitable Download method found on manager.");
                    return;
                }
                onOk?.Invoke(started);
            }
            catch (Exception ex)
            {
                onErr?.Invoke($"Failed to start download: {ex.Message}");
            }
        }

        // New: try get the downloaded .unitypackage path from the Asset Store cache
        public static string TryGetDownloadedPackagePath(long productId)
        {
            if (!Ensure()) return null;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) return null;
            var tICache = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreCache");
            var tCache = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreCache");
            object cache = null;
            try
            {
                if (tICache != null)
                {
                    var gm = resolveGeneric.MakeGenericMethod(tICache);
                    cache = gm.Invoke(_servicesContainer, null);
                }
            }
            catch { cache = null; }
            if (cache == null && tCache != null)
            {
                try
                {
                    var gm = resolveGeneric.MakeGenericMethod(tCache);
                    cache = gm.Invoke(_servicesContainer, null);
                }
                catch { cache = null; }
            }
            if (cache == null) return null;

            var miGetLocalInfo = cache.GetType().GetMethod("GetLocalInfo", flags);
            if (miGetLocalInfo == null) return null;
            object localInfo = null;
            try
            {
                var nullablePid = (long?)productId; // boxed Nullable<long>
                localInfo = miGetLocalInfo.Invoke(cache, new object[] { nullablePid });
            }
            catch { localInfo = null; }
            if (localInfo == null) return null;
            var path = localInfo.GetType().GetProperty("packagePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(localInfo) as string;
            if (string.IsNullOrEmpty(path)) return null;
            try { return File.Exists(path) ? path : null; } catch { return null; }
        }

        // New: invoke Unity's internal installer to import the downloaded package
        public static bool InstallDownloadedPackage(long productId, bool interactive)
        {
            if (!Ensure()) return false;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) return false;
            var tIInstaller = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStorePackageInstaller");
            var tInstaller = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStorePackageInstaller");
            object installer = null;
            try
            {
                if (tIInstaller != null)
                {
                    var gm = resolveGeneric.MakeGenericMethod(tIInstaller);
                    installer = gm.Invoke(_servicesContainer, null);
                }
            }
            catch { installer = null; }
            if (installer == null && tInstaller != null)
            {
                try
                {
                    var gm = resolveGeneric.MakeGenericMethod(tInstaller);
                    installer = gm.Invoke(_servicesContainer, null);
                }
                catch { installer = null; }
            }
            if (installer == null) return false;
            var miInstall = installer.GetType().GetMethod("Install", flags);
            if (miInstall == null) return false;
            try
            {
                miInstall.Invoke(installer, new object[] { productId, interactive });
                return true;
            }
            catch { return false; }
        }

        public static string Diagnose()
        {
            try
            {
                var lines = new List<string>();
                var tSvc = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.ServicesContainer");
                lines.Add($"ServicesContainer type: {(tSvc != null ? "found" : "missing")}");
                var tApi = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreRestAPI");
                lines.Add($"IAssetStoreRestAPI type: {(tApi != null ? "found" : "missing")}");
                if (tSvc == null || tApi == null) return string.Join("\n", lines);

                object inst = null;
                // Attempt via ScriptableSingleton<T>.instance
                try
                {
                    var tSingletonGeneric = ReflectionUtil.FindType("UnityEditor.ScriptableSingleton`1");
                    if (tSingletonGeneric != null)
                    {
                        var tSingleton = tSingletonGeneric.MakeGenericType(tSvc);
                        var instPropGeneric = tSingleton.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        inst = instPropGeneric?.GetValue(null);
                        lines.Add($"ScriptableSingleton<T>.instance: {(inst != null ? "ok" : "null")}");
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"ScriptableSingleton instance exception: {ex.GetType().Name}: {ex.Message}");
                }

                // Fallback derived static properties
                if (inst == null)
                {
                    var alt = tSvc.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                           ?? tSvc.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                    inst = alt;
                    lines.Add($"Derived static instance: {(inst != null ? "ok" : "null")}");
                }

                // Last resort: construct
                if (inst == null)
                {
                    try
                    {
                        inst = Activator.CreateInstance(tSvc, nonPublic: true);
                        var reload = tSvc.GetMethod("Reload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        reload?.Invoke(inst, null);
                        lines.Add("Constructed ServicesContainer: ok");
                    }
                    catch (Exception ex)
                    {
                        lines.Add($"Construct ServicesContainer failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                if (inst == null) return string.Join("\n", lines);

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var resolveGeneric = tSvc.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
                lines.Add($"Resolve<T>() method: {(resolveGeneric != null ? "found" : "missing")}");
                if (resolveGeneric == null) return string.Join("\n", lines);

                object api = null;
                try
                {
                    var gm = resolveGeneric.MakeGenericMethod(tApi);
                    api = gm.Invoke(inst, null);
                    lines.Add($"Resolve<IAssetStoreRestAPI> result: {(api != null ? "ok" : "null")}");
                }
                catch (Exception ex)
                {
                    lines.Add($"Resolve exception: {ex.GetType().Name}: {ex.Message}");
                }

                if (api == null)
                {
                    try
                    {
                        var reload = tSvc.GetMethod("Reload", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        reload?.Invoke(inst, null);
                        var gm2 = resolveGeneric.MakeGenericMethod(tApi);
                        api = gm2.Invoke(inst, null);
                        lines.Add($"After Reload(), resolve result: {(api != null ? "ok" : "null")}");
                    }
                    catch (Exception ex)
                    {
                        lines.Add($"Reload/Resolve exception: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                return $"Diagnose failed: {ex.GetType().Name}: {ex.Message}";
            }
        }

        // Debug subscriber state
        private static bool _debugAttached;
        private static Delegate _dbgStateChanged;
        private static Delegate _dbgProgress;
        private static Delegate _dbgFinalized;
        private static Delegate _dbgError;

        public static bool EnableDownloadDebugLogging(bool enable = true)
        {
            if (!Ensure()) return false;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) return false;

            // Proactively ensure Unity's native download delegate is registered so progress can flow
            try { EnsureNativeDownloadWiring(); } catch { }

            var tIManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreDownloadManager");
            var tManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager");
            object mgr = null;
            try { if (tIManager != null) mgr = resolveGeneric.MakeGenericMethod(tIManager).Invoke(_servicesContainer, null); } catch { }
            if (mgr == null && tManager != null) { try { mgr = resolveGeneric.MakeGenericMethod(tManager).Invoke(_servicesContainer, null); } catch { } }
            if (mgr == null) { Debug.LogWarning("[AssetStoreDebug] DownloadManager not available."); return false; }

            var mgrType = mgr.GetType();
            var tOp = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadOperation");
            var tUIErr = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");
            if (tOp == null) { Debug.LogWarning("[AssetStoreDebug] DownloadOperation type not found."); return false; }

            var evState = mgrType.GetEvent("onDownloadStateChanged", flags);
            var evProg = mgrType.GetEvent("onDownloadProgress", flags);
            var evFin = mgrType.GetEvent("onDownloadFinalized", flags);
            var evErr = mgrType.GetEvent("onDownloadError", flags);

            void Detach()
            {
                try
                {
                    if (evState != null && _dbgStateChanged != null) evState.RemoveEventHandler(mgr, _dbgStateChanged);
                    if (evProg != null && _dbgProgress != null) evProg.RemoveEventHandler(mgr, _dbgProgress);
                    if (evFin != null && _dbgFinalized != null) evFin.RemoveEventHandler(mgr, _dbgFinalized);
                    if (evErr != null && _dbgError != null) evErr.RemoveEventHandler(mgr, _dbgError);
                }
                catch { }
                _dbgStateChanged = _dbgProgress = _dbgFinalized = _dbgError = null;
                _debugAttached = false;
            }

            if (!enable)
            {
                Detach();
                Debug.Log("[AssetStoreDebug] Download event logging disabled");
                return true;
            }

            // Attach
            try
            {
                // Shared printer
                Action<object, string> print = (opObj, label) =>
                {
                    if (opObj == null) return;
                    try
                    {
                        var t = opObj.GetType();
                        var pid = t.GetProperty("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(opObj);
                        var state = t.GetProperty("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(opObj);
                        var pct = t.GetProperty("progressPercentage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(opObj);
                        Debug.Log($"[AssetStoreDebug] {label} pid={pid} state={state} progress={(pct ?? 0):0.00}");
                    }
                    catch { }
                };

                if (evState != null)
                {
                    _dbgStateChanged = BuildTypedCallback(tOp, o => print(o, "StateChanged"));
                    evState.AddEventHandler(mgr, _dbgStateChanged);
                }
                if (evProg != null)
                {
                    _dbgProgress = BuildTypedCallback(tOp, o => print(o, "Progress"));
                    evProg.AddEventHandler(mgr, _dbgProgress);
                }
                if (evFin != null)
                {
                    _dbgFinalized = BuildTypedCallback(tOp, o =>
                    {
                        print(o, "Finalized");
                        try
                        {
                            var pidVal = (o?.GetType().GetProperty("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o))?.ToString();
                            if (long.TryParse(pidVal, out var pid))
                            {
                                var path = TryGetDownloadedPackagePath(pid);
                                if (!string.IsNullOrEmpty(path)) Debug.Log($"[AssetStoreDebug] Local package path: {path}");
                            }
                        }
                        catch { }
                    });
                    evFin.AddEventHandler(mgr, _dbgFinalized);
                }
                if (evErr != null && tUIErr != null)
                {
                    var handlerType = typeof(Action<,>).MakeGenericType(tOp, tUIErr);
                    var dlg = new Action<object, object>((op, err) =>
                    {
                        try
                        {
                            var msg = err?.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(err) as string;
                            var code = err?.GetType().GetProperty("operationErrorCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(err);
                            Debug.LogError($"[AssetStoreDebug] Error code={code} msg={msg}");
                        }
                        catch { }
                    });
                    _dbgError = Delegate.CreateDelegate(handlerType, dlg, dlg.GetType().GetMethod("Invoke"));
                    evErr.AddEventHandler(mgr, _dbgError);
                }

                _debugAttached = true;
                Debug.Log($"[AssetStoreDebug] Attached to {mgrType.FullName}: state={(evState!=null)}, progress={(evProg!=null)}, finalized={(evFin!=null)}, error={(evErr!=null)}");
                return true;
            }
            catch (Exception ex)
            {
                Detach();
                Debug.LogWarning($"[AssetStoreDebug] Failed to attach download debug handlers: {ex.Message}");
                return false;
            }
        }


        // Keep a delegate registered with UnityEditor.AssetStoreUtils to forward download progress into the manager
        private static bool _delegateRegistered;
        private static ScriptableObject _bridgeDelegate;

        // ScriptableObject that Unity calls back during Asset Store downloads
        private class BridgeDownloadDelegate : ScriptableObject
        {
            // UnityEditor.AssetStoreUtils expects this exact signature
            public void OnDownloadProgress(string downloadId, string message, ulong bytes, ulong total, int errorCode)
            {
                try
                {
                    // Forward to the real manager so it can drive operations and events
                    if (!Ensure()) return;
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
                    if (resolveGeneric == null) return;
                    var tManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager");
                    object mgr = null;
                    try { if (tManager != null) mgr = resolveGeneric.MakeGenericMethod(tManager).Invoke(_servicesContainer, null); } catch { }
                    if (mgr == null) return;

                    // Call public OnDownloadProgress on the manager
                    var mi = tManager.GetMethod("OnDownloadProgress", flags);
                    if (mi != null)
                    {
                        mi.Invoke(mgr, new object[] { downloadId, message, bytes, total, errorCode });
                    }
                }
                catch { }
            }
        }

        // Public: ensure native wiring so progress events reach the manager even if Unity didn't register its delegate yet
        public static void EnsureNativeDownloadWiring()
        {
            try
            {
                if (!Ensure()) return;
                if (_delegateRegistered && _bridgeDelegate != null) return;

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
                if (resolveGeneric == null) return;

                // Resolve AssetStoreUtils
                var tIUtils = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreUtils");
                var tUtils = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreUtils");
                object utils = null;
                try { if (tIUtils != null) utils = resolveGeneric.MakeGenericMethod(tIUtils).Invoke(_servicesContainer, null); } catch { }
                if (utils == null && tUtils != null)
                {
                    try { utils = resolveGeneric.MakeGenericMethod(tUtils).Invoke(_servicesContainer, null); } catch { }
                }
                if (utils == null) return;

                // Create delegate once
                if (_bridgeDelegate == null)
                    _bridgeDelegate = ScriptableObject.CreateInstance<BridgeDownloadDelegate>();

                // Register
                var miReg = utils.GetType().GetMethod("RegisterDownloadDelegate", flags);
                if (miReg != null)
                {
                    miReg.Invoke(utils, new object[] { _bridgeDelegate });
                    _delegateRegistered = true;
                }
            }
            catch { }
        }

        private static void TrySetProperty(object obj, string name, object value)
        {
            if (obj == null) return;
            var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi != null && pi.CanWrite)
            {
                try { pi.SetValue(obj, value); } catch { }
                return;
            }
            var fi = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (fi != null)
            {
                try { fi.SetValue(obj, value); } catch { }
            }
        }

        private static int TryGetIntProperty(object obj, IEnumerable<string> names)
        {
            if (obj == null) return -1;
            var t = obj.GetType();
            foreach (var n in names)
            {
                var pi = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi != null)
                {
                    try
                    {
                        var v = pi.GetValue(obj);
                        if (v is int i) return i;
                        if (v is long l) return (int)l;
                        if (int.TryParse(v?.ToString(), out var ii)) return ii;
                    }
                    catch { }
                }
                var fi = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (fi != null)
                {
                    try
                    {
                        var v = fi.GetValue(obj);
                        if (v is int i2) return i2;
                        if (v is long l2) return (int)l2;
                        if (int.TryParse(v?.ToString(), out var ii2)) return ii2;
                    }
                    catch { }
                }
            }
            return -1;
        }

        private static Delegate BuildTypedCallback(Type argType, Action<object> handler)
        {
            var param = Expression.Parameter(argType, "x");
            var handlerConst = Expression.Constant(handler);
            var invoke = handler.GetType().GetMethod("Invoke");
            var call = Expression.Call(handlerConst, invoke, Expression.Convert(param, typeof(object)));
            var lambda = Expression.Lambda(typeof(Action<>).MakeGenericType(argType), call, param);
            return lambda.Compile();
        }

        private static string ExtractStringProperty(object obj, IEnumerable<string> candidates)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            foreach (var name in candidates)
            {
                var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi != null && pi.PropertyType == typeof(string))
                {
                    var val = pi.GetValue(obj) as string;
                    if (!string.IsNullOrEmpty(val)) return val;
                }
                var fi = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (fi != null && fi.FieldType == typeof(string))
                {
                    var val = fi.GetValue(obj) as string;
                    if (!string.IsNullOrEmpty(val)) return val;
                }
            }
            // Fallback: scan all string props
            foreach (var pi in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (pi.PropertyType == typeof(string))
                {
                    var val = pi.GetValue(obj) as string;
                    if (!string.IsNullOrEmpty(val) && val.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return val;
                }
            }
            return null;
        }

        private static List<long> TryExtractProductIdsFromPurchases(object purchasesObj)
        {
            var ids = new List<long>();
            if (purchasesObj == null) return ids;
            var t = purchasesObj.GetType();

            // Prefer property 'productIds' which returns IEnumerable<long>
            var productIdsProp = t.GetProperty("productIds", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (productIdsProp != null && typeof(IEnumerable).IsAssignableFrom(productIdsProp.PropertyType))
            {
                try
                {
                    var enumerable = productIdsProp.GetValue(purchasesObj) as IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item is long l) ids.Add(l);
                            else if (item is int i) ids.Add(i);
                            else if (long.TryParse(item?.ToString(), out var l2)) ids.Add(l2);
                        }
                        return ids;
                    }
                }
                catch { }
            }

            // Fallback: look for list field/property and extract productId from each element
            var listMember = (MemberInfo)t.GetProperty("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                           ?? t.GetField("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) as MemberInfo;
            if (listMember != null)
            {
                try
                {
                    var listObj = (listMember is PropertyInfo lpi) ? lpi.GetValue(purchasesObj) : (listMember as FieldInfo)?.GetValue(purchasesObj);
                    var enumerable = listObj as IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item == null) continue;
                            var it = item.GetType();
                            var pidVal = it.GetField("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item)
                                      ?? it.GetProperty("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item);
                            if (pidVal is long pl) ids.Add(pl);
                            else if (pidVal is int pi) ids.Add(pi);
                            else if (long.TryParse(pidVal?.ToString(), out var pl2)) ids.Add(pl2);
                        }
                        return ids;
                    }
                }
                catch { }
            }

            return ids;
        }

        private static Dictionary<string, object> FlattenObject(object obj)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (obj == null) return map;
            var t = obj.GetType();
            foreach (var pi in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try { map[pi.Name] = pi.GetValue(obj); } catch { }
            }
            foreach (var fi in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try { map[fi.Name] = fi.GetValue(obj); } catch { }
            }
            return map;
        }

        // Fetch purchases with names (paged). Optionally filter by productIds.
        public static void GetOwnedPurchases(int offset, int limit, List<long> filterProductIds, Action<List<OwnedAssetInfo>, int> onOk, Action<string> onErr = null)
        {
            if (!Ensure()) { onErr?.Invoke("AssetStore service unavailable."); return; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var mi = _assetStoreRestApi.GetType().GetMethod("GetPurchases", flags);
            if (mi == null) { onErr?.Invoke("GetPurchases not found."); return; }
            var purchasesArgsType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.PurchasesQueryArgs");
            var purchasesType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStorePurchases");
            var uiErrorType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");

            var args = CreatePurchasesArgs(purchasesArgsType, offset, limit);
            if (args == null) { onErr?.Invoke("Unable to construct PurchasesQueryArgs."); return; }
            if (filterProductIds != null && filterProductIds.Count > 0)
            {
                var field = purchasesArgsType.GetField("productIds", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && typeof(IEnumerable).IsAssignableFrom(field.FieldType))
                    field.SetValue(args, filterProductIds);
                else
                    TrySetProperty(args, "productIds", filterProductIds);
            }

            var typedSuccess = BuildTypedCallback(purchasesType, (obj) =>
            {
                var items = ExtractOwnedAssetInfos(obj) ?? new List<OwnedAssetInfo>();
                var total = TryGetIntProperty(obj, new[] { "total", "totalResults", "totalCount" });
                onOk?.Invoke(items, total);
            });
            var typedError = BuildTypedCallback(uiErrorType, (obj) =>
            {
                var msg = obj?.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj) as string;
                onErr?.Invoke(string.IsNullOrEmpty(msg) ? "Unknown error." : msg);
            });
            mi.Invoke(_assetStoreRestApi, new object[] { args, typedSuccess, typedError });
        }

        // Try to replicate the internal download via AssetStoreUtils (fallback when DownloadManager fails)
        public static void StartInternalDownloadFallback(long productId, Action<bool> onOk = null, Action<string> onErr = null)
        {
            if (!Ensure()) { onErr?.Invoke("AssetStore service unavailable."); return; }
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) { onErr?.Invoke("ServicesContainer.Resolve<T>() not found."); return; }

            var tIUtils = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreUtils");
            var tUtils = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreUtils");
            object utils = null;
            try { if (tIUtils != null) utils = resolveGeneric.MakeGenericMethod(tIUtils).Invoke(_servicesContainer, null); } catch { }
            if (utils == null && tUtils != null)
            {
                try { utils = resolveGeneric.MakeGenericMethod(tUtils).Invoke(_servicesContainer, null); } catch { }
            }
            if (utils == null) { onErr?.Invoke("AssetStoreUtils not available."); return; }

            var miGetDetail = _assetStoreRestApi.GetType().GetMethod("GetDownloadDetail", flags);
            var downloadInfoType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadInfo");
            var uiErrorType = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");
            if (miGetDetail == null || downloadInfoType == null) { onErr?.Invoke("GetDownloadDetail not available."); return; }

            var success = BuildTypedCallback(downloadInfoType, (obj) =>
            {
                try
                {
                    var url = ExtractStringProperty(obj, new[] { "url" });
                    var key = ExtractStringProperty(obj, new[] { "key" });
                    var destProp = obj.GetType().GetProperty("destination", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var destination = destProp?.GetValue(obj) as string[] ?? new string[0];
                    string p1 = destination.Length > 0 ? destination[0] : string.Empty;
                    string p2 = destination.Length > 1 ? destination[1] : string.Empty;
                    string p3 = destination.Length > 2 ? destination[2] : string.Empty;
                    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) { onErr?.Invoke("Invalid download info."); return; }

                    var t = utils.GetType();
                    var miCheck = t.GetMethod("CheckDownload", flags);
                    var miDown = t.GetMethod("Download", flags);
                    if (miCheck == null || miDown == null) { onErr?.Invoke("AssetStoreUtils methods not found."); return; }

                    string downloadId = $"content__{productId}";
                    var destArr = new string[] { p1, p2, p3 };
                    var jsonCheck = miCheck.Invoke(utils, new object[] { downloadId, url, destArr, key }) as string;
                    bool resume = false;
                    if (!string.IsNullOrEmpty(jsonCheck))
                    {
                        var s = jsonCheck.ToLowerInvariant();
                        resume = s.Contains("\"in_progress\":true");
                    }

                    string jsonData = $"{{\"download\":{{\"url\":\"{url}\",\"key\":\"{key}\"}}}}";
                    miDown.Invoke(utils, new object[] { downloadId, url, destArr, key, jsonData, resume });
                    onOk?.Invoke(true);
                }
                catch (Exception ex)
                {
                    onErr?.Invoke($"Internal download failed: {ex.Message}");
                }
            });
            var errorCb = BuildTypedCallback(uiErrorType, (obj) =>
            {
                var msg = obj?.GetType().GetProperty("message", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj) as string;
                onErr?.Invoke(string.IsNullOrEmpty(msg) ? "Unknown error." : msg);
            });

            miGetDetail.Invoke(_assetStoreRestApi, new object[] { productId, success, errorCb });
        }

        // Helper: extract productId + displayName from AssetStorePurchases
        private static List<OwnedAssetInfo> ExtractOwnedAssetInfos(object purchasesObj)
        {
            var list = new List<OwnedAssetInfo>();
            if (purchasesObj == null) return list;
            var t = purchasesObj.GetType();
            var listMember = (MemberInfo)t.GetProperty("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                           ?? t.GetField("list", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) as MemberInfo;
            if (listMember == null) return list;
            var rawList = (listMember is PropertyInfo lpi) ? lpi.GetValue(purchasesObj) : (listMember as FieldInfo)?.GetValue(purchasesObj);
            var enumerable = rawList as IEnumerable;
            if (enumerable == null) return list;
            foreach (var item in enumerable)
            {
                if (item == null) continue;
                long id = 0;
                string name = null;
                try
                {
                    var it = item.GetType();
                    var pidVal = it.GetField("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item)
                              ?? it.GetProperty("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item);
                    if (pidVal is long l) id = l; else if (pidVal is int i) id = i; else long.TryParse(pidVal?.ToString(), out id);
                    name = (it.GetField("displayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item) as string)
                        ?? (it.GetProperty("displayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item) as string)
                        ?? (it.GetProperty("packageName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item) as string)
                        ?? (it.GetField("packageName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item) as string);
                }
                catch { }
                if (id > 0)
                    list.Add(new OwnedAssetInfo { ProductId = id, DisplayName = name });
            }
            return list;
        }

        // Register a temporary listener that installs when the native download for the given productId finalizes
        public static bool RegisterOneShotInstallOnDownloadComplete(long productId, bool interactive)
        {
            if (!Ensure()) return false;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) return false;
            var tManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager");
            var tIManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreDownloadManager");
            object mgr = null;
            try { if (tIManager != null) mgr = resolveGeneric.MakeGenericMethod(tIManager).Invoke(_servicesContainer, null); } catch { }
            if (mgr == null && tManager != null) { try { mgr = resolveGeneric.MakeGenericMethod(tManager).Invoke(_servicesContainer, null); } catch { } }
            if (mgr == null) return false;

            var mgrType = mgr.GetType();
            var tOp = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadOperation");
            var tUIErr = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.UIError");
            if (tOp == null) return false;

            // Event infos
            var evFinalized = mgrType.GetEvent("onDownloadFinalized", flags);
            var evError = mgrType.GetEvent("onDownloadError", flags);

            // Keep delegates to unsubscribe after firing once
            Delegate finalizedDel = null;
            Delegate errorDel = null;

            Action<object> onFinalizedCore = (opObj) =>
            {
                try
                {
                    if (opObj == null) return;
                    var pv = opObj.GetType().GetProperty("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(opObj);
                    long pid = 0;
                    if (pv is long l) pid = l;
                    else if (pv is int i) pid = i;
                    else long.TryParse(pv?.ToString(), out pid);
                    if (pid != productId) return;
                    // Unsubscribe
                    if (evFinalized != null && finalizedDel != null) evFinalized.RemoveEventHandler(mgr, finalizedDel);
                    if (evError != null && errorDel != null) evError.RemoveEventHandler(mgr, errorDel);
                    // Install
                    EditorApplication.delayCall += () => InstallDownloadedPackage(productId, interactive);
                }
                catch { }
            };

            Action<object, object> onErrorCore = (opObj, errObj) =>
            {
                try
                {
                    if (opObj == null) return;
                    var pv = opObj.GetType().GetProperty("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(opObj);
                    long pid = 0;
                    if (pv is long l) pid = l;
                    else if (pv is int i) pid = i;
                    else long.TryParse(pv?.ToString(), out pid);
                    if (pid != productId) return;
                    // Unsubscribe on error for this product
                    if (evFinalized != null && finalizedDel != null) evFinalized.RemoveEventHandler(mgr, finalizedDel);
                    if (evError != null && errorDel != null) evError.RemoveEventHandler(mgr, errorDel);
                }
                catch { }
            };

            // Build delegates with correct signatures
            if (evFinalized != null)
            {
                // Build via expression wrapper to match Action<AssetStoreDownloadOperation>
                finalizedDel = BuildTypedCallback(tOp, o => onFinalizedCore(o));
                evFinalized.AddEventHandler(mgr, finalizedDel);
            }

            if (evError != null && tUIErr != null)
            {
                var handlerType = typeof(Action<,>).MakeGenericType(tOp, tUIErr);
                // Build wrapper that ignores UIError and routes op to our core
                var dlg = new Action<object, object>((op, err) => onErrorCore(op, err));
                errorDel = Delegate.CreateDelegate(handlerType, dlg, dlg.GetType().GetMethod("Invoke"));
                evError.AddEventHandler(mgr, errorDel);
            }

            return true;
        }

        // New: watch AssetStoreCache.onLocalInfosChanged and auto-install when target productId appears
        public static bool RegisterInstallOnCacheChange(long productId, bool interactive)
        {
            if (!Ensure()) return false;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) return false;

            var tICache = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreCache");
            var tCache = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreCache");
            var tLocalInfo = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreLocalInfo");
            if (tLocalInfo == null) return false;

            object cache = null;
            try { if (tICache != null) cache = resolveGeneric.MakeGenericMethod(tICache).Invoke(_servicesContainer, null); } catch { }
            if (cache == null && tCache != null) { try { cache = resolveGeneric.MakeGenericMethod(tCache).Invoke(_servicesContainer, null); } catch { } }
            if (cache == null) return false;

            var evt = cache.GetType().GetEvent("onLocalInfosChanged", flags);
            if (evt == null) return false;

            // Build Action<IEnumerable<AssetStoreLocalInfo>, IEnumerable<AssetStoreLocalInfo>>
            var tEnumerableLocal = typeof(IEnumerable<>).MakeGenericType(tLocalInfo);
            var handlerType = typeof(Action<,>).MakeGenericType(tEnumerableLocal, tEnumerableLocal);

            Delegate del = null; // will hold reference for removal
            Action<object, object> core = (addedOrUpdated, removed) =>
            {
                try
                {
                    bool MatchInEnumerable(object enumerable)
                    {
                        if (enumerable is IEnumerable seq)
                        {
                            foreach (var item in seq)
                            {
                                if (item == null) continue;
                                var pv = item.GetType().GetProperty("productId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item);
                                long pid = 0;
                                if (pv is long l) pid = l; else if (pv is int i) pid = i; else long.TryParse(pv?.ToString(), out pid);
                                if (pid == productId) return true;
                            }
                        }
                        return false;
                    }

                    if (MatchInEnumerable(addedOrUpdated))
                    {
                        // Unsubscribe once triggered
                        try { if (del != null) evt.RemoveEventHandler(cache, del); } catch { }
                        // Install via internal installer; fallback to AssetDatabase if needed
                        EditorApplication.delayCall += () =>
                        {
                            if (!InstallDownloadedPackage(productId, interactive))
                            {
                                var path = TryGetDownloadedPackagePath(productId);
                                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                {
                                    try { AssetDatabase.ImportPackage(path, interactive); } catch { }
                                }
                            }
                        };
                    }
                }
                catch { }
            };

            var invokeMi = core.GetType().GetMethod("Invoke");
            del = Delegate.CreateDelegate(handlerType, core, invokeMi);
            try { evt.AddEventHandler(cache, del); return true; } catch { return false; }
        }

        // New helper: check if a native Asset Store download is currently active for a product
        public static bool IsDownloadInProgress(long productId)
        {
            if (!Ensure()) return false;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) return false;

            var tIManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreDownloadManager");
            var tManager = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager");
            object mgr = null;
            try { if (tIManager != null) mgr = resolveGeneric.MakeGenericMethod(tIManager).Invoke(_servicesContainer, null); } catch { }
            if (mgr == null && tManager != null) { try { mgr = resolveGeneric.MakeGenericMethod(tManager).Invoke(_servicesContainer, null); } catch { } }
            if (mgr == null) return false;

            try
            {
                var miGet = mgr.GetType().GetMethod("GetDownloadOperation", flags);
                if (miGet == null) return false;
                object nullablePid = (long?)productId;
                var op = miGet.Invoke(mgr, new object[] { nullablePid });
                if (op == null) return false;
                var tOp = op.GetType();
                var inProgress = tOp.GetProperty("isInProgress", flags)?.GetValue(op) as bool?;
                var inPause = tOp.GetProperty("isInPause", flags)?.GetValue(op) as bool?;
                return (inProgress ?? false) || (inPause ?? false);
            }
            catch { return false; }
        }

        // New: raw access to AssetStoreLocalInfo
        public static object GetLocalInfoRaw(long productId)
        {
            if (!Ensure()) return null;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var resolveGeneric = _typeServicesContainer.GetMethods(flags).FirstOrDefault(m => m.Name == "Resolve" && m.IsGenericMethodDefinition);
            if (resolveGeneric == null) return null;
            var tICache = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.IAssetStoreCache");
            var tCache = ReflectionUtil.FindType("UnityEditor.PackageManager.UI.Internal.AssetStoreCache");
            object cache = null;
            try { if (tICache != null) cache = resolveGeneric.MakeGenericMethod(tICache).Invoke(_servicesContainer, null); } catch { }
            if (cache == null && tCache != null)
            {
                try { cache = resolveGeneric.MakeGenericMethod(tCache).Invoke(_servicesContainer, null); } catch { }
            }
            if (cache == null) return null;
            var miGetLocalInfo = cache.GetType().GetMethod("GetLocalInfo", flags);
            if (miGetLocalInfo == null) return null;
            try
            {
                object nullablePid = (long?)productId;
                return miGetLocalInfo.Invoke(cache, new object[] { nullablePid });
            }
            catch { return null; }
        }

        // New: dump local info (properties/fields) as a dictionary for diagnostics
        public static Dictionary<string, object> TryGetLocalInfoMap(long productId)
        {
            try
            {
                var obj = GetLocalInfoRaw(productId);
                return FlattenObject(obj);
            }
            catch { return new Dictionary<string, object>(); }
        }

        // New: best-effort detection whether product was imported into the project
        public static bool IsImported(long productId)
        {
            var info = GetLocalInfoRaw(productId);
            if (info == null) return false;
            var t = info.GetType();
            object GetProp(string name)
            {
                try { return t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(info); } catch { return null; }
            }
            object GetField(string name)
            {
                try { return t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(info); } catch { return null; }
            }
            bool TryBool(object o, out bool b)
            {
                if (o is bool bb) { b = bb; return true; }
                if (o is string s && bool.TryParse(s, out var bp)) { b = bp; return true; }
                b = false; return false;
            }
            long TryLong(object o)
            {
                if (o is long l) return l;
                if (o is int i) return i;
                if (o is string s && long.TryParse(s, out var lp)) return lp;
                return 0;
            }

            // Common flags observed across Unity versions (guessed names; tolerant)
            var namesBool = new[] { "isImported", "imported", "isInProject", "installed" };
            foreach (var n in namesBool)
            {
                var v = GetProp(n) ?? GetField(n);
                if (TryBool(v, out var b)) return b;
            }

            // Timestamp-based heuristics
            var stampNames = new[] { "importTimestamp", "lastImportedTime", "lastImportedTicks" };
            foreach (var n in stampNames)
            {
                var v = GetProp(n) ?? GetField(n);
                if (v != null && TryLong(v) > 0) return true;
            }

            // Imported path recorded?
            var pathNames = new[] { "importedPath", "lastImportedPath", "installedPath" };
            foreach (var n in pathNames)
            {
                var v = (GetProp(n) ?? GetField(n)) as string;
                if (!string.IsNullOrWhiteSpace(v)) return true;
            }

            // Fallback: asset database may have imported assets tracked; not accessible here. Assume not imported.
            return false;
        }
    }
}
#endif
