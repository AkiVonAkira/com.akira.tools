#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using akira.ToolsHub;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace akira.Packages
{
    public static class PackageManager
    {
        // Cache refresh constants
        private const float CACHE_REFRESH_INTERVAL_MINUTES = 10;
        private const int REQUEST_TIMEOUT_SECONDS = 30;

        // Package status cache
        private static Dictionary<string, PackageInfo> _installedPackagesCache;
        private static bool _refreshingPackageCache;
        private static DateTime _lastPackageCacheRefresh = DateTime.MinValue;

        // Active requests
        private static readonly Dictionary<string, AddRequest> _activeAddRequests = new();
        private static readonly Dictionary<string, RemoveRequest> _activeRemoveRequests = new();
        private static ListRequest _activeListRequest;

        private static bool _processRequests;

        // Events to notify about installation progress
        public static event Action<string, float> OnPackageInstallProgress;
        public static event Action<string, bool> OnPackageInstallComplete;
        public static event Action<string> OnPackageInstallError;
        public static event Action OnAllPackagesInstallComplete;

        // Try to resolve an installed package info by flexible ID matching
        private static bool TryFindInstalledPackageInfo(string packageId, out PackageInfo info)
        {
            info = null;
            if (_installedPackagesCache == null) return false;

            // Direct lookup
            if (_installedPackagesCache.TryGetValue(packageId, out var direct))
            {
                info = direct;
                return true;
            }

            // Git/URL or other forms: iterate values and use matching
            foreach (var pkg in _installedPackagesCache.Values)
                if (PackageIdUtils.IsPackageIdMatch(packageId, pkg))
                {
                    info = pkg;
                    return true;
                }

            return false;
        }

        // Install a list of packages
        public static void InstallPackages(IEnumerable<string> packageIds, IProgress<float> progress = null)
        {
            var ids = packageIds.ToList();
            var totalCount = ids.Count;

            if (totalCount == 0)
            {
                OnAllPackagesInstallComplete?.Invoke();
                return;
            }

            // Start processing packages one by one
            var completedCount = 0;
            _processRequests = true;

            // Clear any existing requests
            _activeAddRequests.Clear();

            foreach (var packageId in ids)
            {
                OnPackageInstallProgress?.Invoke(packageId, 0f);
                AddPackageRequest(packageId);
            }

            // Set up a callback to check progress regularly
            EditorApplication.update += CheckPackageInstallProgress;

            // Local function to check progress
            void CheckPackageInstallProgress()
            {
                if (!_processRequests)
                {
                    EditorApplication.update -= CheckPackageInstallProgress;
                    return;
                }

                var allComplete = true;
                var completedPackages = new List<string>();

                // Check each request
                foreach (var entry in _activeAddRequests)
                {
                    var packageId = entry.Key;
                    var request = entry.Value;

                    if (request == null) continue;

                    if (request.IsCompleted)
                    {
                        var success = request.Status == StatusCode.Success;
                        completedCount++;

                        // Report completion
                        OnPackageInstallProgress?.Invoke(packageId, 1f);
                        OnPackageInstallComplete?.Invoke(packageId, success);

                        // Track for removal
                        completedPackages.Add(packageId);

                        if (!success && request.Error != null)
                        {
                            var label = GetPackageNameFromId(packageId, request.Result);
                            ToolsHubManager.ShowNotification($"Failed to install {label}: {request.Error.message}", "error");
                            Debug.LogError($"Failed to install package {packageId}: {request.Error.message}");
                        }
                    }
                    else
                    {
                        // Still in progress
                        allComplete = false;
                        OnPackageInstallProgress?.Invoke(packageId, 0.5f);
                    }
                }

                // Remove completed packages
                foreach (var packageId in completedPackages) _activeAddRequests.Remove(packageId);

                // Update overall progress
                var overallProgress = totalCount > 0 ? (float)completedCount / totalCount : 1f;
                progress?.Report(overallProgress);

                // If all done, finish up
                if (allComplete && _activeAddRequests.Count == 0)
                {
                    _processRequests = false;
                    RefreshPackageCache();
                    OnAllPackagesInstallComplete?.Invoke();
                    EditorApplication.update -= CheckPackageInstallProgress;
                }
            }
        }

        // Add a package with a request on the main thread
        private static void AddPackageRequest(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return;

            try
            {
                // Create the request on the main thread
                var addArg = PackageIdUtils.NormalizeForAdd(packageId);
                var request = Client.Add(addArg);
                _activeAddRequests[packageId] = request;
            }
            catch (Exception ex)
            {
                var label = GetPackageNameFromId(packageId);
                ToolsHubManager.ShowNotification($"Error adding {label}", "error");
                Debug.LogError($"Error creating add request for {packageId}: {ex.Message}");
                OnPackageInstallError?.Invoke(packageId);
            }
        }

        // Install a single package
        public static void InstallPackage(string packageId, Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(packageId))
                return;

            progressCallback?.Invoke(0.1f);
            // Emit UI progress event for consistency with batch installs
            OnPackageInstallProgress?.Invoke(packageId, 0f);
            _processRequests = true;

            try
            {
                // Create the request on the main thread
                var addArg = PackageIdUtils.NormalizeForAdd(packageId);
                var request = Client.Add(addArg);
                _activeAddRequests[packageId] = request;

                // Set up a callback to check progress
                EditorApplication.update += CheckProgress;

                void CheckProgress()
                {
                    if (!_processRequests)
                    {
                        EditorApplication.update -= CheckProgress;
                        return;
                    }

                    if (request.IsCompleted)
                    {
                        var success = request.Status == StatusCode.Success;

                        progressCallback?.Invoke(1f);
                        OnPackageInstallProgress?.Invoke(packageId, 1f);
                        OnPackageInstallComplete?.Invoke(packageId, success);

                        var label = GetPackageNameFromId(packageId, request.Result);
                        if (success)
                        {
                            ToolsHubManager.ShowNotification($"Installed {label}.", "success");
                        }
                        else if (request.Error != null)
                        {
                            ToolsHubManager.ShowNotification($"Failed to install {label}", "error");
                            Debug.LogError($"Failed to install package {packageId}: {request.Error.message}");
                            OnPackageInstallError?.Invoke(packageId);
                        }

                        _activeAddRequests.Remove(packageId);
                        RefreshPackageCache();

                        // Signal overall completion for single install to allow UI to clear
                        OnAllPackagesInstallComplete?.Invoke();

                        EditorApplication.update -= CheckProgress;
                    }
                    else
                    {
                        // Mid-progress updates to move UI from 0%
                        progressCallback?.Invoke(0.5f);
                        OnPackageInstallProgress?.Invoke(packageId, 0.5f);
                    }
                }
            }
            catch (Exception ex)
            {
                var label = GetPackageNameFromId(packageId);
                ToolsHubManager.ShowNotification($"Error installing {label}", "error");
                Debug.LogError($"Error installing package {packageId}: {ex.Message}");
                OnPackageInstallError?.Invoke(packageId);
            }
        }

        // Remove a package
        public static async void RemovePackage(string packageId, Action<bool> onComplete = null)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                onComplete?.Invoke(false);
                return;
            }

            try
            {
                // Resolve to installed package name for Git/URL inputs
                string idForRemoval = packageId;
                if (PackageIdUtils.IsGitLike(packageId))
                {
                    await RefreshPackageCacheIfNeeded();
                    if (_installedPackagesCache != null)
                    {
                        foreach (var pkg in _installedPackagesCache.Values)
                        {
                            if (PackageIdUtils.IsPackageIdMatch(packageId, pkg))
                            {
                                idForRemoval = pkg.name; // must be canonical name for removal
                                break;
                            }
                        }
                    }
                }

                // If still not a canonical name (heuristic), try normalization fallback
                if (!idForRemoval.Contains('.'))
                {
                    // Not a typical com.* name; attempt to use installed cache lookup again
                    await RefreshPackageCacheIfNeeded();
                    if (_installedPackagesCache != null && _installedPackagesCache.TryGetValue(idForRemoval, out var info))
                        idForRemoval = info.name;
                }

                // Create the request on the main thread
                var request = Client.Remove(idForRemoval);
                _activeRemoveRequests[idForRemoval] = request;

                // Set up a callback to check progress
                EditorApplication.update += CheckProgress;

                void CheckProgress()
                {
                    if (request.IsCompleted)
                    {
                        var success = request.Status == StatusCode.Success;

                        var label = GetPackageNameFromId(idForRemoval);
                        if (success)
                        {
                            ToolsHubManager.ShowNotification($"Removed {label}.", "success");
                        }
                        else if (request.Error != null)
                        {
                            ToolsHubManager.ShowNotification($"Failed to remove {label}", "error");
                            Debug.LogError($"Failed to remove package {idForRemoval}: {request.Error.message}");
                        }

                        _activeRemoveRequests.Remove(idForRemoval);
                        RefreshPackageCache();
                        onComplete?.Invoke(success);
                        EditorApplication.update -= CheckProgress;
                    }
                }
            }
            catch (Exception ex)
            {
                var label = GetPackageNameFromId(packageId);
                ToolsHubManager.ShowNotification($"Error removing {label}", "error");
                Debug.LogError($"Error removing package {packageId}: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }

        // Check if a package is installed
        public static async Task<bool> IsPackageInstalled(string packageId)
        {
            await RefreshPackageCacheIfNeeded();

            if (_installedPackagesCache == null)
                return false;

            // For regular packages, direct lookup
            if (_installedPackagesCache.ContainsKey(packageId))
                return true;

            // For Git packages or other special cases, need to check each package
            if (PackageIdUtils.IsGitLike(packageId))
            {
                if (TryFindInstalledPackageInfo(packageId, out _)) return true;
            }

            return false;
        }

        // Get package info if installed
        public static async Task<PackageInfo> GetPackageInfo(string packageId)
        {
            await RefreshPackageCacheIfNeeded();

            if (_installedPackagesCache == null)
                return null;

            // Direct lookup for regular packages
            if (_installedPackagesCache.TryGetValue(packageId, out var info))
                return info;

            // For Git packages or other special cases, need to check each package
            if (PackageIdUtils.IsGitLike(packageId))
            {
                if (TryFindInstalledPackageInfo(packageId, out var found)) return found;
            }

            return null;
        }

        // Check if package has an update available
        public static async Task<bool> HasPackageUpdate(string packageId)
        {
            var info = await GetPackageInfo(packageId);

            if (info == null)
                return false;

            // Registry packages: compare against latest
            if (info.source == PackageSource.Registry)
                return !string.IsNullOrEmpty(info.versions.latest) && info.versions.latest != info.version;

            // Git packages: Unity doesn't provide remote comparison; treat as no automatic update available.
            // A future enhancement could poll the git remote for tags/commits.
            return false;
        }

        // Get all installed packages
        public static async Task<Dictionary<string, PackageInfo>> GetInstalledPackages()
        {
            await RefreshPackageCacheIfNeeded();
            return _installedPackagesCache ?? new Dictionary<string, PackageInfo>();
        }

        // Refresh the package cache
        public static async Task RefreshPackageCacheIfNeeded()
        {
            if (_installedPackagesCache == null ||
                (_refreshingPackageCache == false &&
                 (DateTime.Now - _lastPackageCacheRefresh).TotalMinutes > CACHE_REFRESH_INTERVAL_MINUTES))
                await RefreshPackageCache();
        }

        // Force refresh the package cache
        public static Task RefreshPackageCache()
        {
            var tcs = new TaskCompletionSource<bool>();

            if (_refreshingPackageCache)
            {
                tcs.SetResult(false);
                return tcs.Task;
            }

            _refreshingPackageCache = true;

            try
            {
                // Create the list request on the main thread
                _activeListRequest = Client.List(true); // true for includeIndirectDependencies

                // Setup a timeout to prevent infinite waiting
                var timeoutCancellationSource = new CancellationTokenSource();

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(REQUEST_TIMEOUT_SECONDS),
                    timeoutCancellationSource.Token);

                // Set up a callback to check progress
                EditorApplication.update += CheckProgress;

                void CheckProgress()
                {
                    // Check if timed out
                    if (timeoutTask.IsCompleted && !timeoutTask.IsCanceled)
                    {
                        // Timeout occurred
                        if (Debug.isDebugBuild) Debug.LogWarning("Package listing request timed out");
                        _refreshingPackageCache = false;
                        EditorApplication.update -= CheckProgress;
                        tcs.TrySetResult(false);
                        return;
                    }

                    if (_activeListRequest.IsCompleted)
                    {
                        // Cancel the timeout since we've completed
                        timeoutCancellationSource.Cancel();

                        if (_activeListRequest.Status == StatusCode.Success)
                        {
                            _installedPackagesCache = new Dictionary<string, PackageInfo>();

                            // Log all found packages to help diagnose issues (only in debug builds)
                            if (Debug.isDebugBuild)
                            {
                                Debug.Log(
                                    $"Package refresh complete. Found {_activeListRequest.Result.Count()} packages.");

                                foreach (var package in _activeListRequest.Result)
                                    // Debug package information for Git packages
                                    if (package.source is PackageSource.Git or PackageSource.Embedded)
                                        Debug.Log(
                                            $"Found Git/Embedded package - Name: {package.name}, ID: {package.packageId}, Source: {package.source}");
                            }

                            // Always build the cache, but only log in debug builds
                            foreach (var package in _activeListRequest.Result)
                                _installedPackagesCache[package.name] = package;

                            _lastPackageCacheRefresh = DateTime.Now;
                            tcs.TrySetResult(true);
                        }
                        else if (_activeListRequest.Error != null)
                        {
                            ToolsHubManager.ShowNotification("Failed to list packages", "error");
                            Debug.LogError($"Failed to list packages: {_activeListRequest.Error.message}");
                            tcs.TrySetResult(false);
                        }
                        else
                        {
                            tcs.TrySetResult(false);
                        }

                        _refreshingPackageCache = false;
                        EditorApplication.update -= CheckProgress;
                    }
                }
            }
            catch (Exception ex)
            {
                ToolsHubManager.ShowNotification("Error refreshing package information", "error");
                Debug.LogError($"Error refreshing package cache: {ex.Message}");
                _refreshingPackageCache = false;
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        // Friendly label resolution from package id
        public static string GetPackageNameFromId(string packageId, PackageInfo info = null)
        {
            if (string.IsNullOrWhiteSpace(packageId)) return packageId;

            try
            {
                // Prefer provided info (e.g., from AddRequest.Result)
                if (info != null)
                {
                    var nameFromInfo = !string.IsNullOrEmpty(info.displayName) ? info.displayName : info.name;
                    if (!string.IsNullOrEmpty(nameFromInfo)) return nameFromInfo;
                }

                // Try installed cache
                if (_installedPackagesCache != null)
                {
                    if (_installedPackagesCache.TryGetValue(packageId, out var cached))
                        return !string.IsNullOrEmpty(cached.displayName) ? cached.displayName : cached.name;

                    if (TryFindInstalledPackageInfo(packageId, out var found))
                        return !string.IsNullOrEmpty(found.displayName) ? found.displayName : found.name;
                }

                // Git/URL fallback to repo name
                if (PackageIdUtils.IsGitLike(packageId))
                {
                    var normalized = PackageIdUtils.NormalizePackageId(packageId);
                    if (!string.IsNullOrEmpty(normalized)) return normalized;
                }
            }
            catch
            {
                // ignore and fall back
            }

            return packageId;
        }
    }
}
#endif
