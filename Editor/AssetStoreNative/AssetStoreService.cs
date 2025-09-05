#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace akira.AssetStoreNative
{
    // High-level API your UI can call to get ownership and download assets
    public static class AssetStoreService
    {
        public static bool IsAvailable => AssetStoreBridge.IsAvailable();
        public static bool IsSignedIn => AssetStoreBridge.IsSignedIn();

        // Simple owned fetch (single page default). For all, use GetAllOwnedProducts.
        public static void GetOwnedProducts(Action<List<long>> onOk, Action<string> onErr = null)
        {
            if (!IsAvailable) { onErr?.Invoke("Unity Package Manager Asset Store service is not available in this Editor version."); return; }
            if (!IsSignedIn) { onErr?.Invoke("You need to be signed in to Unity."); return; }
            AssetStoreBridge.GetOwnedProducts(onOk, onErr);
        }

        // New: fetch all owned with display names (auto-page)
        public static void GetAllOwnedPurchases(Action<List<OwnedAssetInfo>> onOk, Action<string> onErr = null, int pageSize = 200)
        {
            if (!IsAvailable) { onErr?.Invoke("Unity Package Manager Asset Store service is not available in this Editor version."); return; }
            if (!IsSignedIn) { onErr?.Invoke("You need to be signed in to Unity."); return; }

            var acc = new List<OwnedAssetInfo>();
            int fetched = 0;
            int total = int.MaxValue;

            void NextPage()
            {
                if (fetched >= total) { onOk?.Invoke(acc); return; }
                AssetStoreBridge.GetOwnedPurchases(fetched, pageSize, null, (items, t) =>
                {
                    total = (t > 0) ? t : total;
                    if (items != null && items.Count > 0)
                    {
                        acc.AddRange(items);
                        fetched += items.Count;
                        if (items.Count < pageSize && t <= 0) onOk?.Invoke(acc); else NextPage();
                    }
                    else onOk?.Invoke(acc);
                }, err => onErr?.Invoke(err));
            }

            NextPage();
        }

        // Auto-page across purchases endpoint to fetch complete list (ids only)
        public static void GetAllOwnedProducts(Action<List<long>> onOk, Action<string> onErr = null, int pageSize = 200)
        {
            if (!IsAvailable) { onErr?.Invoke("Unity Package Manager Asset Store service is not available in this Editor version."); return; }
            if (!IsSignedIn) { onErr?.Invoke("You need to be signed in to Unity."); return; }

            var acc = new List<long>();
            int fetched = 0;
            int total = int.MaxValue;

            void NextPage()
            {
                if (fetched >= total) { onOk?.Invoke(acc); return; }
                AssetStoreBridge.GetOwnedProducts(fetched, pageSize, null, (ids, t) =>
                {
                    total = (t > 0) ? t : total; // if API provides a total, use it; otherwise continue until an empty page
                    if (ids != null && ids.Count > 0)
                    {
                        acc.AddRange(ids);
                        fetched += ids.Count;
                        if (ids.Count < pageSize && t <= 0)
                        {
                            // likely last page even without total
                            onOk?.Invoke(acc);
                        }
                        else NextPage();
                    }
                    else
                    {
                        onOk?.Invoke(acc); // no more
                    }
                }, err => onErr?.Invoke(err));
            }

            NextPage();
        }

        public static void IsOwned(long productId, Action<bool> onOk, Action<string> onErr = null)
        {
            if (!IsAvailable) { onErr?.Invoke("Service unavailable."); return; }
            AssetStoreBridge.IsOwned(productId, onOk, onErr);
        }

        public static void GetProductDetail(long productId, Action<Dictionary<string, object>> onOk, Action<string> onErr = null)
        {
            if (!IsAvailable) { onErr?.Invoke("Service unavailable."); return; }
            AssetStoreBridge.GetProductDetail(productId, onOk, onErr);
        }

        public static void DownloadAndImport(long productId, bool showImportDialog = true)
        {
            if (!IsAvailable) { EditorUtility.DisplayDialog("Asset Store", "Service unavailable.", "OK"); return; }
            if (!IsSignedIn) { EditorUtility.DisplayDialog("Asset Store", "You need to be signed in to download.", "OK"); return; }

            // Start the streamlined download process following Unity's pattern
            var operation = new DownloadOperation(productId, showImportDialog);
            operation.Start();
        }

        // Streamlined download operation that follows Unity's native pattern
        private class DownloadOperation
        {
            private readonly long _productId;
            private readonly bool _showImportDialog;
            private string _downloadId;
            private bool _isActive;

            public DownloadOperation(long productId, bool showImportDialog)
            {
                _productId = productId;
                _showImportDialog = showImportDialog;
                _downloadId = $"content__{productId}";
            }

            public void Start()
            {
                if (_isActive) return;
                _isActive = true;

                // Step 1: Check Terms of Service (following Unity's pattern)
                AssetStoreBridge.CheckTermsAndConditions(accepted =>
                {
                    if (!accepted)
                    {
                        ShowTermsDialog();
                        return;
                    }
                    BeginDownload();
                }, error =>
                {
                    // Continue even if terms check fails (Unity's behavior)
                    Debug.LogWarning($"Terms check failed: {error}. Proceeding with download.");
                    BeginDownload();
                });
            }

            private void ShowTermsDialog()
            {
                if (EditorUtility.DisplayDialog("Asset Store Terms",
                    "You need to accept the Asset Store terms before downloading.",
                    "Open Browser", "Cancel"))
                {
                    Application.OpenURL("https://assetstore.unity.com/account/term");
                }
                _isActive = false;
            }

            private void BeginDownload()
            {
                // Step 2: Ensure download delegate is registered for progress callbacks
                AssetStoreBridge.EnsureNativeDownloadWiring();

                // Step 3: Try Unity's native download manager first
                AssetStoreBridge.StartNativeDownload(_productId, success =>
                {
                    if (success)
                    {
                        // Native download started, monitor via cache polling
                        MonitorNativeDownload();
                    }
                    else
                    {
                        // Fallback to manual download process
                        FallbackDownload();
                    }
                }, error =>
                {
                    Debug.LogWarning($"Native download failed: {error}. Trying fallback.");
                    FallbackDownload();
                });
            }

            private void MonitorNativeDownload()
            {
                // Register one-shot completion handler
                AssetStoreBridge.RegisterOneShotInstallOnDownloadComplete(_productId, _showImportDialog);
                AssetStoreBridge.RegisterInstallOnCacheChange(_productId, _showImportDialog);

                // Start polling for completion
                EditorCoroutineRunner.StartEditorCoroutine(PollForCompletion());
            }

            private void FallbackDownload()
            {
                // Use internal AssetStoreUtils as fallback
                AssetStoreBridge.StartInternalDownloadFallback(_productId, success =>
                {
                    if (success)
                    {
                        MonitorNativeDownload(); // Same monitoring for internal downloads
                    }
                    else
                    {
                        LegacyDownload(); // Last resort
                    }
                }, error =>
                {
                    Debug.LogWarning($"Internal download failed: {error}. Using legacy method.");
                    LegacyDownload();
                });
            }

            private void LegacyDownload()
            {
                // Final fallback using direct URL download
                AssetStoreBridge.GetDownloadUrl(_productId, url =>
                {
                    EditorCoroutineRunner.StartEditorCoroutine(DirectDownload(url));
                }, error =>
                {
                    ShowError($"Download failed: {error}");
                });
            }

            private IEnumerator PollForCompletion()
            {
                const double pollInterval = 0.5;
                const double maxWaitMinutes = 10;
                var startTime = EditorApplication.timeSinceStartup;
                var nextPoll = startTime;

                while (_isActive)
                {
                    var now = EditorApplication.timeSinceStartup;
                    
                    if (now >= nextPoll)
                    {
                        // Check if package is downloaded and cached
                        var path = AssetStoreBridge.TryGetDownloadedPackagePath(_productId);
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            CompleteDownload(path);
                            yield break;
                        }

                        // If the native manager reports no operation in progress anymore, stop polling quietly
                        if (!AssetStoreBridge.IsDownloadInProgress(_productId))
                        {
                            EditorUtility.ClearProgressBar();
                            _isActive = false;
                            yield break;
                        }

                        nextPoll = now + pollInterval;
                    }

                    // Timeout check
                    if ((now - startTime) / 60.0 > maxWaitMinutes)
                    {
                        // If the manager is no longer reporting in-progress, treat as success (import may already be queued)
                        if (!AssetStoreBridge.IsDownloadInProgress(_productId))
                        {
                            EditorUtility.ClearProgressBar();
                            _isActive = false;
                            yield break;
                        }

                        ShowError("Download timed out. Please try again.");
                        yield break;
                    }

                    var progress = (float)((now - startTime) / (maxWaitMinutes * 60.0));
                    EditorUtility.DisplayProgressBar("Downloading Asset", $"Product {_productId}", progress);
                    yield return null;
                }
            }

            private IEnumerator DirectDownload(string url)
            {
                var tempDir = "Temp/AssetStoreDownloads";
                Directory.CreateDirectory(tempDir);
                var filePath = Path.Combine(tempDir, $"{_productId}.unitypackage");

                using var request = UnityWebRequest.Get(url);
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    EditorUtility.DisplayProgressBar("Downloading Asset", 
                        $"Product {_productId}", operation.progress);
                    yield return null;
                }

                EditorUtility.ClearProgressBar();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var data = request.downloadHandler.data;
                        // Validate it's a proper unitypackage (gzip format)
                        if (data != null && data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B)
                        {
                            File.WriteAllBytes(filePath, data);
                            CompleteDownload(filePath);
                        }
                        else
                        {
                            ShowError("Downloaded file is not a valid Unity package.");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Failed to save package: {ex.Message}");
                    }
                }
                else
                {
                    ShowError($"Download failed: {request.error}");
                }
            }

            private void CompleteDownload(string packagePath)
            {
                EditorUtility.ClearProgressBar();
                _isActive = false;

                try
                {
                    // Try Unity's internal installer first (preserves metadata)
                    if (!AssetStoreBridge.InstallDownloadedPackage(_productId, _showImportDialog))
                    {
                        // Fallback to standard import
                        AssetDatabase.ImportPackage(packagePath, _showImportDialog);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to import package: {ex.Message}");
                }
            }

            private void ShowError(string message)
            {
                EditorUtility.ClearProgressBar();
                _isActive = false;
                Debug.LogError(message);
                EditorUtility.DisplayDialog("Download Error", message, "OK");
            }
        }
    }

    // Lightweight coroutine runner for editor coroutines
    internal class EditorCoroutineRunner : ScriptableObject
    {
        private IEnumerator _routine;
        private static List<EditorCoroutineRunner> _active = new List<EditorCoroutineRunner>();

        public static void StartEditorCoroutine(IEnumerator routine)
        {
            if (routine == null) return;
            var runner = CreateInstance<EditorCoroutineRunner>();
            runner._routine = routine;
            _active.Add(runner);
            EditorApplication.update += runner.Update;
        }

        private void Update()
        {
            try
            {
                if (_routine == null || !_routine.MoveNext())
                    Stop();
            }
            catch (Exception)
            {
                Stop();
            }
        }

        private void Stop()
        {
            EditorApplication.update -= Update;
            _active.Remove(this);
            DestroyImmediate(this);
        }
    }
}
#endif
