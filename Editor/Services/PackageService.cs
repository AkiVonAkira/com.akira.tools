using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Akira.Tools.Core;

namespace Akira.Tools.Services
{
    /// <summary>
    /// Service layer for package management operations
    /// Separates business logic from UI layer
    /// </summary>
    public class PackageService
    {
        private AddRequest addRequest;
        private RemoveRequest removeRequest;
        private ListRequest listRequest;
        private SearchRequest searchRequest;

        #region Public API

        /// <summary>
        /// Install a package from Git URL
        /// </summary>
        public void InstallPackageFromGit(string gitUrl, Action<bool, string> callback)
        {
            if (!ErrorHandler.ValidateNotEmpty(gitUrl, "Git URL"))
            {
                callback?.Invoke(false, "Git URL cannot be empty");
                return;
            }

            if (!IsValidGitUrl(gitUrl))
            {
                ErrorHandler.LogErrorWithCode("PKG002", gitUrl);
                callback?.Invoke(false, ErrorHandler.GetErrorMessage("PKG002"));
                return;
            }

            ErrorHandler.Log($"Installing package from Git: {gitUrl}");

            ErrorHandler.TryWithRetry(
                action: () =>
                {
                    addRequest = Client.Add(gitUrl);
                    EditorApplication.update += () => ProgressAddRequest(callback);
                },
                maxRetries: 3,
                delayMs: 2000,
                onFinalError: (ex) =>
                {
                    ErrorHandler.LogErrorWithCode("PKG003", ex.Message);
                    callback?.Invoke(false, "Package installation failed after retries");
                },
                context: $"Install package from Git: {gitUrl}"
            );
        }

        /// <summary>
        /// Install a package by name (from Unity Registry)
        /// </summary>
        public void InstallPackage(string packageName, Action<bool, string> callback)
        {
            if (!ErrorHandler.ValidateNotEmpty(packageName, "Package Name"))
            {
                callback?.Invoke(false, "Package name cannot be empty");
                return;
            }

            ErrorHandler.Log($"Installing package: {packageName}");

            ErrorHandler.Try(
                action: () =>
                {
                    addRequest = Client.Add(packageName);
                    EditorApplication.update += () => ProgressAddRequest(callback);
                },
                onError: (ex) =>
                {
                    ErrorHandler.LogErrorWithCode("PKG003", ex.Message);
                    callback?.Invoke(false, "Package installation failed");
                },
                context: $"Install package: {packageName}"
            );
        }

        /// <summary>
        /// Remove a package from the project
        /// </summary>
        public void RemovePackage(string packageName, Action<bool, string> callback)
        {
            if (!ErrorHandler.ValidateNotEmpty(packageName, "Package Name"))
            {
                callback?.Invoke(false, "Package name cannot be empty");
                return;
            }

            ErrorHandler.Log($"Removing package: {packageName}");

            ErrorHandler.Try(
                action: () =>
                {
                    removeRequest = Client.Remove(packageName);
                    EditorApplication.update += () => ProgressRemoveRequest(callback);
                },
                onError: (ex) =>
                {
                    ErrorHandler.LogError($"Failed to remove package: {ex.Message}");
                    callback?.Invoke(false, "Package removal failed");
                },
                context: $"Remove package: {packageName}"
            );
        }

        /// <summary>
        /// Get list of all installed packages
        /// </summary>
        public void GetInstalledPackages(Action<List<UnityEditor.PackageManager.PackageInfo>> callback)
        {
            ErrorHandler.Try(
                action: () =>
                {
                    listRequest = Client.List(true);
                    EditorApplication.update += () => ProgressListRequest(callback);
                },
                onError: (ex) =>
                {
                    ErrorHandler.LogError($"Failed to get package list: {ex.Message}");
                    callback?.Invoke(new List<UnityEditor.PackageManager.PackageInfo>());
                },
                context: "Get installed packages"
            );
        }

        /// <summary>
        /// Search for packages in the registry
        /// </summary>
        public void SearchPackages(string searchQuery, Action<List<UnityEditor.PackageManager.PackageInfo>> callback)
        {
            if (!ErrorHandler.ValidateNotEmpty(searchQuery, "Search Query"))
            {
                callback?.Invoke(new List<UnityEditor.PackageManager.PackageInfo>());
                return;
            }

            ErrorHandler.Try(
                action: () =>
                {
                    searchRequest = Client.Search(searchQuery);
                    EditorApplication.update += () => ProgressSearchRequest(callback);
                },
                onError: (ex) =>
                {
                    ErrorHandler.LogError($"Failed to search packages: {ex.Message}");
                    callback?.Invoke(new List<UnityEditor.PackageManager.PackageInfo>());
                },
                context: $"Search packages: {searchQuery}"
            );
        }

        /// <summary>
        /// Parse Asset Store URL and extract package information
        /// </summary>
        public AssetStorePackageInfo ParseAssetStoreUrl(string url)
        {
            if (!ErrorHandler.ValidateNotEmpty(url, "Asset Store URL"))
            {
                return null;
            }

            return ErrorHandler.Try(
                func: () =>
                {
                    // Extract package ID from Asset Store URL
                    // Format: https://assetstore.unity.com/packages/.../{packageId}
                    var uri = new Uri(url);
                    
                    if (!uri.Host.Contains("assetstore.unity.com"))
                    {
                        ErrorHandler.LogWarning("URL is not from Unity Asset Store");
                        return null;
                    }

                    var segments = uri.Segments;
                    var lastSegment = segments[segments.Length - 1];
                    
                    if (int.TryParse(lastSegment, out int packageId))
                    {
                        return new AssetStorePackageInfo
                        {
                            PackageId = packageId,
                            Url = url
                        };
                    }

                    ErrorHandler.LogWarning("Could not extract package ID from URL");
                    return null;
                },
                defaultValue: null,
                onError: (ex) => ErrorHandler.LogError($"Failed to parse Asset Store URL: {ex.Message}"),
                context: "Parse Asset Store URL"
            );
        }

        #endregion

        #region Private Methods

        private void ProgressAddRequest(Action<bool, string> callback)
        {
            if (addRequest == null) return;

            if (addRequest.IsCompleted)
            {
                EditorApplication.update -= () => ProgressAddRequest(callback);

                if (addRequest.Status == StatusCode.Success)
                {
                    ErrorHandler.Log($"Package installed successfully: {addRequest.Result.displayName}");
                    callback?.Invoke(true, $"Successfully installed {addRequest.Result.displayName}");
                }
                else if (addRequest.Status >= StatusCode.Failure)
                {
                    ErrorHandler.LogError($"Package installation failed: {addRequest.Error.message}");
                    callback?.Invoke(false, addRequest.Error.message);
                }

                addRequest = null;
            }
        }

        private void ProgressRemoveRequest(Action<bool, string> callback)
        {
            if (removeRequest == null) return;

            if (removeRequest.IsCompleted)
            {
                EditorApplication.update -= () => ProgressRemoveRequest(callback);

                if (removeRequest.Status == StatusCode.Success)
                {
                    ErrorHandler.Log($"Package removed successfully");
                    callback?.Invoke(true, "Package removed successfully");
                }
                else if (removeRequest.Status >= StatusCode.Failure)
                {
                    ErrorHandler.LogError($"Package removal failed: {removeRequest.Error.message}");
                    callback?.Invoke(false, removeRequest.Error.message);
                }

                removeRequest = null;
            }
        }

        private void ProgressListRequest(Action<List<UnityEditor.PackageManager.PackageInfo>> callback)
        {
            if (listRequest == null) return;

            if (listRequest.IsCompleted)
            {
                EditorApplication.update -= () => ProgressListRequest(callback);

                if (listRequest.Status == StatusCode.Success)
                {
                    var packages = new List<UnityEditor.PackageManager.PackageInfo>(listRequest.Result);
                    callback?.Invoke(packages);
                }
                else
                {
                    ErrorHandler.LogError($"Failed to list packages: {listRequest.Error.message}");
                    callback?.Invoke(new List<UnityEditor.PackageManager.PackageInfo>());
                }

                listRequest = null;
            }
        }

        private void ProgressSearchRequest(Action<List<UnityEditor.PackageManager.PackageInfo>> callback)
        {
            if (searchRequest == null) return;

            if (searchRequest.IsCompleted)
            {
                EditorApplication.update -= () => ProgressSearchRequest(callback);

                if (searchRequest.Status == StatusCode.Success)
                {
                    var packages = new List<UnityEditor.PackageManager.PackageInfo>(searchRequest.Result);
                    callback?.Invoke(packages);
                }
                else
                {
                    ErrorHandler.LogError($"Package search failed: {searchRequest.Error.message}");
                    callback?.Invoke(new List<UnityEditor.PackageManager.PackageInfo>());
                }

                searchRequest = null;
            }
        }

        private bool IsValidGitUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            return url.StartsWith("https://") || url.StartsWith("git://") || url.StartsWith("ssh://") ||
                   url.StartsWith("git@") || url.EndsWith(".git");
        }

        #endregion
    }

    /// <summary>
    /// Data class for Asset Store package information
    /// </summary>
    public class AssetStorePackageInfo
    {
        public int PackageId { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
    }
}

