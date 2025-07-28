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

        // Helper methods for Git package ID normalization
        private static string NormalizePackageId(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return packageId;

            // If it's a Git package, extract just the repo name without the git+ prefix
            if (packageId.StartsWith("git+"))
            {
                // Extract the repository name from the URL
                var repoUrl = packageId.Substring(4); // Remove "git+" prefix

                // Get the last part of the URL (the repo name)
                var urlParts = repoUrl.Split('/');

                if (urlParts.Length > 0)
                {
                    // Extract repository name and remove .git extension if present
                    var repoName = urlParts[urlParts.Length - 1].Replace(".git", "");

                    // Check if the repository name is empty
                    if (!string.IsNullOrEmpty(repoName))
                        return repoName;
                }
            }

            return packageId;
        }

        private static bool IsPackageIdMatch(string packageId, PackageInfo installedPackage)
        {
            if (string.IsNullOrEmpty(packageId) || installedPackage == null)
                return false;

            // Direct match
            if (packageId == installedPackage.name)
                return true;

            // For Git packages, try to match the repository name
            if (packageId.StartsWith("git+"))
            {
                var normalizedId = NormalizePackageId(packageId);

                // Try matching with just the normalized ID
                if (normalizedId == installedPackage.name)
                    return true;

                // Also match where normalized ID is contained in the installed package ID
                if (installedPackage.packageId != null &&
                    (installedPackage.packageId.Contains(normalizedId) ||
                     installedPackage.packageId.Contains(packageId.Substring(4)))) // Without git+ prefix
                    return true;

                // Extract repo name from URL and check if it matches the package name
                if (packageId.Contains("/"))
                {
                    var parts = packageId.Substring(4).Split('/'); // Remove git+ prefix
                    var repoName = parts[parts.Length - 1].Replace(".git", "");

                    if (repoName.Equals(installedPackage.name, StringComparison.OrdinalIgnoreCase) ||
                        (installedPackage.name.Contains(".") &&
                         installedPackage.name.EndsWith(repoName, StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
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
                            ToolsHubManger.ShowNotification(
                                $"Failed to install package {packageId}: {request.Error.message}", "error");
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
                var request = Client.Add(packageId);
                _activeAddRequests[packageId] = request;
            }
            catch (Exception ex)
            {
                ToolsHubManger.ShowNotification($"Error adding package {packageId}", "error");
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
            _processRequests = true;

            try
            {
                // Create the request on the main thread
                var request = Client.Add(packageId);
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
                        OnPackageInstallComplete?.Invoke(packageId, success);

                        if (success)
                        {
                            ToolsHubManger.ShowNotification($"Package {packageId} installed successfully.", "success");
                        }
                        else if (request.Error != null)
                        {
                            ToolsHubManger.ShowNotification($"Failed to install package {packageId}", "error");
                            Debug.LogError($"Failed to install package {packageId}: {request.Error.message}");
                            OnPackageInstallError?.Invoke(packageId);
                        }

                        _activeAddRequests.Remove(packageId);
                        RefreshPackageCache();
                        EditorApplication.update -= CheckProgress;
                    }
                    else
                    {
                        progressCallback?.Invoke(0.5f);
                    }
                }
            }
            catch (Exception ex)
            {
                ToolsHubManger.ShowNotification($"Error installing package {packageId}", "error");
                Debug.LogError($"Error installing package {packageId}: {ex.Message}");
                OnPackageInstallError?.Invoke(packageId);
            }
        }

        // Remove a package
        public static void RemovePackage(string packageId, Action<bool> onComplete = null)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                onComplete?.Invoke(false);

                return;
            }

            try
            {
                // Create the request on the main thread
                var request = Client.Remove(packageId);
                _activeRemoveRequests[packageId] = request;

                // Set up a callback to check progress
                EditorApplication.update += CheckProgress;

                void CheckProgress()
                {
                    if (request.IsCompleted)
                    {
                        var success = request.Status == StatusCode.Success;

                        if (success)
                        {
                            ToolsHubManger.ShowNotification($"Package {packageId} removed successfully.", "success");
                        }
                        else if (request.Error != null)
                        {
                            ToolsHubManger.ShowNotification($"Failed to remove package {packageId}", "error");
                            Debug.LogError($"Failed to remove package {packageId}: {request.Error.message}");
                        }

                        _activeRemoveRequests.Remove(packageId);
                        RefreshPackageCache();
                        onComplete?.Invoke(success);
                        EditorApplication.update -= CheckProgress;
                    }
                }
            }
            catch (Exception ex)
            {
                ToolsHubManger.ShowNotification($"Error removing package {packageId}", "error");
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
            if (packageId.StartsWith("git+"))
            {
                // Additional logging for Git package checking (only in debug builds)
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"Checking if Git package is installed: {packageId}");
                    var normalizedId = NormalizePackageId(packageId);
                    Debug.Log($"Normalized package ID: {normalizedId}");
                }

                foreach (var package in _installedPackagesCache.Values)
                    if (IsPackageIdMatch(packageId, package))
                    {
                        if (Debug.isDebugBuild) Debug.Log($"Found matching Git package: {packageId} as {package.name}");

                        return true;
                    }

                if (Debug.isDebugBuild) Debug.Log($"Git package not found: {packageId}");
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
            if (packageId.StartsWith("git+"))
                foreach (var package in _installedPackagesCache.Values)
                    if (IsPackageIdMatch(packageId, package))
                    {
                        if (Debug.isDebugBuild)
                            Debug.Log(
                                $"Found Git package info for {packageId}: {package.name}, version {package.version}");

                        return package;
                    }

            return null;
        }

        // Check if package has an update available
        public static async Task<bool> HasPackageUpdate(string packageId)
        {
            var info = await GetPackageInfo(packageId);

            if (info == null)
                return false;

            // Check if there's a newer version - compare enum value properly
            return info.source == PackageSource.Registry &&
                   !string.IsNullOrEmpty(info.versions.latest) &&
                   info.versions.latest != info.version;
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
                            ToolsHubManger.ShowNotification("Failed to list packages", "error");
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
                ToolsHubManger.ShowNotification("Error refreshing package information", "error");
                Debug.LogError($"Error refreshing package cache: {ex.Message}");
                _refreshingPackageCache = false;
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }
    }
}
#endif