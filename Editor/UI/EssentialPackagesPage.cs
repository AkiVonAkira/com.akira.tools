#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using akira.Packages;
using akira.ToolsHub;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEditor.PackageManager; // added for PackageSource

namespace akira.UI
{
    public class EssentialPackagesPageImpl : IToolsHubPage
    {
        private const string TEXTMESHPRO_ID = "TextMeshPro";
        private const string TEXTMESHPRO_PACKAGE_ID = "com.unity.textmeshpro";

        // UI constants
        private static readonly Color EnabledButtonColor = new(0.2f, 0.7f, 0.2f, 1f);
        private static readonly Color DisabledButtonColor = new(0.7f, 0.2f, 0.2f, 1f);
        private static readonly Color InstallButtonColor = new(0.3f, 0.5f, 0.9f, 1f);
        private static readonly Color HeaderColor = new(0.25f, 0.25f, 0.25f, 1f);
        private readonly Dictionary<string, bool> _hasPackageUpdate = new();

        // Package status tracking
        private readonly Dictionary<string, bool> _isPackageInstalled = new();
        private readonly Dictionary<string, bool> _packageInstallSuccess = new();
        private readonly Dictionary<string, float> _packageProgress = new();
        private readonly Dictionary<string, string> _packageVersions = new();
        private readonly Dictionary<string, string> _packageLatestVersions = new(); // latest (registry) version when available
        private bool _addingNewPackage;

        // Installation state
        private bool _isInstalling;
        private bool _isRefreshingStatus;

        // Check if TextMeshPro resources are already installed
        private DateTime _lastTMPCheck = DateTime.MinValue;
        private string _newPackageDescription = "";
        private string _newPackageDisplayName = "";

        // UI state
        private string _newPackageId = "";
        private bool _newPackageIsEssential;
        private float _overallProgress;
        private Vector2 _scrollPosition;

        // Filter/search
        private string _search = "";
        private bool _showOnlyEnabled;
        private bool _showOnlyEssential;
        private bool _showOnlyInstalled;
        private bool _tmpResourcesInstalled;

        public EssentialPackagesPageImpl()
        {
            // Register for package installation events
            PackageManager.OnPackageInstallProgress += HandlePackageInstallProgress;
            PackageManager.OnPackageInstallComplete += HandlePackageInstallComplete;
            PackageManager.OnPackageInstallError += HandlePackageInstallError;
            PackageManager.OnAllPackagesInstallComplete += HandleAllPackagesComplete;

            // IMPORTANT: Don't access Package Manager or ToolsHubSettings in the constructor
            // Instead, use delayCall to schedule all initialization after construction
            EditorApplication.delayCall += InitializePackagePage;
        }

        // Provide a way to rebind the header refresh action when the page regains focus
        public void BindRefreshHook()
        {
            ToolsHubManager.SetPageRefreshHandler(() => RefreshPackageStatus());
        }

        // Page implementation
        public string Title => "Manage Essential & Recommended Packages";
        public string Description => "Install and manage essential and recommended packages for your project";

        // Helper: detect if there are any non-TMP packages configured
        private bool HasNonTMPPackages()
        {
            var packages = ToolsHubSettings.GetAllPackages();
            if (packages == null || packages.Count == 0) return false;
            return packages.Any(p =>
                !string.IsNullOrEmpty(p?.Id) &&
                !p.Id.Equals(TEXTMESHPRO_ID, StringComparison.OrdinalIgnoreCase) &&
                !p.Id.Equals(TEXTMESHPRO_PACKAGE_ID, StringComparison.OrdinalIgnoreCase) &&
                p.Id.IndexOf("textmeshpro", StringComparison.OrdinalIgnoreCase) < 0
            );
        }

        // Helper: restore default packages into settings
        private void RestoreDefaultPackages()
        {
            AddDefaultPackages();
            ToolsHubSettings.Save();
            ToolsHubManager.ShowNotification("Default package list restored.", "success");
            RefreshPackageStatus();
        }

        public void DrawContentHeader()
        {
            // Search bar (full width) + Clear button inline
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(42));
            _search = EditorGUILayout.TextField(_search ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) _search = "";
            EditorGUILayout.EndHorizontal();

            // Wrapping toggles + actions row
            DrawWrappingFilterControls();

            // Show a hint if the list only contains TMP
            if (!HasNonTMPPackages())
                EditorGUILayout.HelpBox(
                    "Only TextMesh Pro is present. Click 'Restore Defaults' to re-add recommended packages.",
                    MessageType.Info);

            // Overall progress when installing
            if (_isInstalling)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Working... {_overallProgress:P0}", EditorStyles.boldLabel);
                var progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, _overallProgress, "");
                EditorGUILayout.Space(5);
            }
        }

        // Render the toggle chips and actions with wrapping
        private void DrawWrappingFilterControls()
        {
            float available = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 24f);
            float gap = 6f;
            float lineUsed = 0f;

            void NewLine()
            {
                if (lineUsed > 0) EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                lineUsed = 0f;
            }

            float MeasureToggle(string label)
            {
                var size = EditorStyles.label.CalcSize(new GUIContent(label));
                return size.x + 26f; // checkbox + padding
            }

            float MeasureButton(string label)
            {
                var size = GUI.skin.button.CalcSize(new GUIContent(label));
                return size.x + 8f;
            }

            void SpaceIfNeeded(float need)
            {
                if (lineUsed > 0) { GUILayout.Space(gap); lineUsed += gap; }
                if (lineUsed + need > available)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    lineUsed = 0f;
                }
            }

            NewLine();

            // Toggles only (Refresh moved to toolbar)
            var t1w = MeasureToggle("Enabled Only");
            SpaceIfNeeded(t1w);
            _showOnlyEnabled = EditorGUILayout.ToggleLeft("Enabled Only", _showOnlyEnabled, GUILayout.Width(t1w));
            lineUsed += t1w;

            var t2w = MeasureToggle("Essential Only");
            SpaceIfNeeded(t2w);
            _showOnlyEssential = EditorGUILayout.ToggleLeft("Essential Only", _showOnlyEssential, GUILayout.Width(t2w));
            lineUsed += t2w;

            var t3w = MeasureToggle("Installed Only");
            SpaceIfNeeded(t3w);
            _showOnlyInstalled = EditorGUILayout.ToggleLeft("Installed Only", _showOnlyInstalled, GUILayout.Width(t3w));
            lineUsed += t3w;

            if (!HasNonTMPPackages())
            {
                var restoreW = MeasureButton("Restore Defaults");
                SpaceIfNeeded(restoreW);
                if (GUILayout.Button("Restore Defaults", GUILayout.Width(restoreW))) RestoreDefaultPackages();
                lineUsed += restoreW;
            }

            // End the last line
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        public void DrawScrollContent()
        {
            var packages = ToolsHubSettings.GetAllPackages();

            // If there are no packages at all OR only TMP exists, seed defaults
            var needDefaults = packages == null || packages.Count == 0 || !HasNonTMPPackages();
            if (needDefaults)
            {
                AddDefaultPackages();
                // Ensure persistence so they're visible immediately
                ToolsHubSettings.Save();
                packages = ToolsHubSettings.GetAllPackages();
            }

            // Exclude Asset Store entries (handled by dedicated page)
            packages = packages
                .Where(p => p.IsAssetStore != true && string.IsNullOrEmpty(p.AssetStoreUrl))
                .ToList();

            // Apply filters
            packages = FilterPackages(packages);

            // Group packages by category (sort essentials first)
            var groupedPackages = packages
                .OrderByDescending(p => p.IsEssential)
                .ThenBy(p => p.DisplayName)
                .ToList();

            // Draw packages
            foreach (var package in groupedPackages)
                DrawPackageEntry(package);
        }

        // Page implementation
        public void DrawContentFooter()
        {
            // Empty - moved buttons to the main footer
        }

        public void DrawFooter()
        {
            // When adding a package, render the form only (form has its own footer buttons)
            if (_addingNewPackage)
            {
                DrawAddNewPackageForm();
                return;
            }

            // Build split footer: Cancel on the left, other actions on the right
            var left = new List<PageLayout.FooterButton>();
            var right = new List<PageLayout.FooterButton>();

            void CancelAction()
            {
                PackageManager.OnPackageInstallProgress -= HandlePackageInstallProgress;
                PackageManager.OnPackageInstallComplete -= HandlePackageInstallComplete;
                PackageManager.OnPackageInstallError -= HandlePackageInstallError;
                PackageManager.OnAllPackagesInstallComplete -= HandleAllPackagesComplete;
                ToolsHubManager.ClosePage(PageOperationResult.Cancelled);
            }

            // Left: Close only
            left.Add(new PageLayout.FooterButton
            {
                Label = "Close",
                Style = PageLayout.FooterButtonStyle.Secondary,
                Enabled = true,
                OnClick = CancelAction,
                MinWidth = 100
            });

            // Right: primary actions (right-aligned)
            right.Add(new PageLayout.FooterButton
            {
                Label = "Add New",
                Style = PageLayout.FooterButtonStyle.Secondary,
                Enabled = !_isInstalling && !_isRefreshingStatus,
                OnClick = () =>
                {
                    _addingNewPackage = true;
                    _newPackageId = "";
                    _newPackageDisplayName = "";
                    _newPackageDescription = "";
                    _newPackageIsEssential = false;
                }
            });

            var removeCount = GetRemovableCount();
            right.Add(new PageLayout.FooterButton
            {
                Label = $"Remove ({removeCount})",
                Style = PageLayout.FooterButtonStyle.Danger,
                Enabled = removeCount > 0 && !_isInstalling && !_isRefreshingStatus,
                OnClick = StartPackageRemoval,
                MinWidth = 110
            });

            var installCount = GetInstallableCount();
            right.Add(new PageLayout.FooterButton
            {
                Label = $"Install ({installCount})",
                Style = PageLayout.FooterButtonStyle.Primary,
                Enabled = installCount > 0 && !_isInstalling && !_isRefreshingStatus,
                OnClick = StartPackageInstallation,
                MinWidth = 110
            });

            PageLayout.DrawFooterSplit(left, right);
        }

        public void OnPageResult(PageOperationResult result)
        {
            // Do nothing
        }

        // New method to initialize everything after constructor completes
        private void InitializePackagePage()
        {
            // Provide toolbar Refresh hook for this page
            ToolsHubManager.SetPageRefreshHandler(() => RefreshPackageStatus());

            // First, clean up any existing TMP entries to prevent installation attempts
            RemoveConflictingTMPEntries();

            // Check if TMP resources are already installed
            CheckTMPResourcesInstalled();

            // Don't initialize package status immediately to avoid freezing
            // Schedule a delayed initialization to allow the UI to render first
            EditorApplication.delayCall += () =>
            {
                if (this != null) InitializePackageStatus();
            };
        }

        private void InitializePackageStatus()
        {
            // Only run this once when the page is first shown
            if (!_isRefreshingStatus)
            {
                _isRefreshingStatus = true;
                RefreshPackageStatus();
            }
        }

        ~EssentialPackagesPageImpl()
        {
            // Unregister events
            PackageManager.OnPackageInstallProgress -= HandlePackageInstallProgress;
            PackageManager.OnPackageInstallComplete -= HandlePackageInstallComplete;
            PackageManager.OnPackageInstallError -= HandlePackageInstallError;
            PackageManager.OnAllPackagesInstallComplete -= HandleAllPackagesComplete;
        }

        // Event handlers
        private void HandlePackageInstallProgress(string packageId, float progress)
        {
            _packageProgress[packageId] = progress;
            // Recompute overall progress as average of active package progresses
            if (_packageProgress.Count > 0)
                _overallProgress = Mathf.Clamp01(_packageProgress.Values.Average());
            else
                _overallProgress = 0f;

            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManager>().Repaint(); };
        }

        private void HandlePackageInstallComplete(string packageId, bool success)
        {
            _packageInstallSuccess[packageId] = success;
            // Ensure this package shows as complete in progress tracking
            _packageProgress[packageId] = 1f;
            // Update overall progress
            if (_packageProgress.Count > 0)
                _overallProgress = Mathf.Clamp01(_packageProgress.Values.Average());

            // Recommend restart for Burst installs
            if (success && string.Equals(packageId, "com.unity.burst", StringComparison.OrdinalIgnoreCase))
                ToolsHubManager.ShowNotification("Burst installed. Editor restart is recommended.", "warning");

            RefreshPackageStatus(packageId);
            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManager>().Repaint(); };
        }

        private void HandlePackageInstallError(string packageId)
        {
            _packageInstallSuccess[packageId] = false;
            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManager>().Repaint(); };
        }

        private void HandleAllPackagesComplete()
        {
            _isInstalling = false;
            _overallProgress = 1f;
            // Clear per-package progress after completion to remove lingering bars
            _packageProgress.Clear();
            _packageInstallSuccess.Clear();
            RefreshPackageStatus();
            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManager>().Repaint(); };
        }

        private List<PackageEntry> FilterPackages(List<PackageEntry> packages)
        {
            if (string.IsNullOrWhiteSpace(_search) && !_showOnlyEnabled &&
                !_showOnlyInstalled && !_showOnlyEssential)
                return packages;

            return packages.Where(p =>
                (string.IsNullOrWhiteSpace(_search) ||
                 (p.DisplayName ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (p.Id ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (p.Description ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) &&
                (!_showOnlyEnabled || p.IsEnabled) &&
                (!_showOnlyEssential || p.IsEssential) &&
                (!_showOnlyInstalled || (_isPackageInstalled.ContainsKey(p.Id) && _isPackageInstalled[p.Id]))
            ).ToList();
        }

        private void InstallSinglePackage(string packageId)
        {
            _isInstalling = true;
            _overallProgress = 0f;
            _packageProgress[packageId] = 0;

            // Call install on main thread
            PackageManager.InstallPackage(packageId,
                progress => _packageProgress[packageId] = progress);
        }

        private void RemovePackage(string packageId)
        {
            if (EditorUtility.DisplayDialog("Remove Package",
                    "Remove this package from your project?", "Yes", "Cancel"))
            {
                _isInstalling = true;

                // Call remove on main thread
                PackageManager.RemovePackage(packageId, success =>
                {
                    _isInstalling = false;
                    RefreshPackageStatus();
                });
            }
        }

        private void ImportTMPResources()
        {
            try
            {
                // Show a progress indicator
                _isInstalling = true;
                var packageId = TEXTMESHPRO_ID;
                _packageProgress[packageId] = 0.5f;

                // Import TMP resources using the built-in importer
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        // Use the TMP package resource importer directly
                        TMP_PackageResourceImporter.ImportResources(true, false, false);

                        _packageProgress[packageId] = 1.0f;
                        _packageInstallSuccess[packageId] = true;

                        // Update the TMP resources installed flag
                        CheckTMPResourcesInstalled();

                        EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error importing TMP resources: {ex.Message}");
                        _packageProgress[packageId] = 1.0f;
                        _packageInstallSuccess[packageId] = false;
                    }
                    finally
                    {
                        _isInstalling = false;
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting up TMP resources import: {ex.Message}");
                _isInstalling = false;
            }
        }

        private void RemoveTMPResources()
        {
            // update implementation to remove TMP resources
            try
            {
                var essentialsPath = "Assets/TextMesh Pro/Resources";
                var examplesPath = "Assets/TextMesh Pro/Examples & Extras";

                var changed = false;

                if (Directory.Exists(essentialsPath))
                {
                    FileUtil.DeleteFileOrDirectory(essentialsPath);
                    FileUtil.DeleteFileOrDirectory(essentialsPath + ".meta");
                    changed = true;
                    ToolsHubManager.ShowNotification("Deleted TMP Essentials.", "success");
                }

                if (Directory.Exists(examplesPath))
                {
                    FileUtil.DeleteFileOrDirectory(examplesPath);
                    FileUtil.DeleteFileOrDirectory(examplesPath + ".meta");
                    changed = true;
                    ToolsHubManager.ShowNotification("Deleted TMP Examples & Extras.", "success");
                }

                if (changed)
                    AssetDatabase.Refresh();
                else
                    ToolsHubManager.ShowNotification("No TMP resources found to delete.");
            }
            catch (Exception ex)
            {
                ToolsHubManager.ShowNotification("Error removing TMP resources", "error");
                Debug.LogError($"Error setting up TMP resources removal: {ex.Message}");
                _isInstalling = false;
            }
        }

        private void DrawAddNewPackageForm()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Add New Package", EditorStyles.boldLabel);

            _newPackageId = EditorGUILayout.TextField("Package ID:", _newPackageId);
            _newPackageDisplayName = EditorGUILayout.TextField("Display Name:", _newPackageDisplayName);
            _newPackageDescription = EditorGUILayout.TextField("Description:", _newPackageDescription);
            _newPackageIsEssential = EditorGUILayout.Toggle("Is Essential:", _newPackageIsEssential);

            // Show Git package template button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Use Git Package Template", GUILayout.Width(180)))
            {
                _newPackageId = "git+https://github.com/username/repo.git";

                if (string.IsNullOrEmpty(_newPackageDisplayName))
                    _newPackageDisplayName = "Git Package";
            }

            EditorGUILayout.EndHorizontal();

            // Example helper
            EditorGUILayout.HelpBox("Package ID examples:\n" +
                                    "• Registry: com.unity.cinemachine\n" +
                                    "• Git: git+https://github.com/username/repo.git or https://github.com/username/repo.git\n" +
                                    "• Local: file:/path/to/package",
                MessageType.Info);

            EditorGUILayout.Space(5);

            // Buttons
            var canSave = !string.IsNullOrWhiteSpace(_newPackageId) &&
                          !string.IsNullOrWhiteSpace(_newPackageDisplayName);

            var formButtons = new List<PageLayout.FooterButton>
            {
                new PageLayout.FooterButton
                {
                    Label = "Cancel",
                    Style = PageLayout.FooterButtonStyle.Secondary,
                    Enabled = true,
                    OnClick = () => _addingNewPackage = false
                },
                new PageLayout.FooterButton
                {
                    Label = "Add Package",
                    Style = PageLayout.FooterButtonStyle.Primary,
                    Enabled = canSave,
                    OnClick = () =>
                    {
                        var packageEntry = new PackageEntry
                        {
                            Id = _newPackageId.Trim(),
                            DisplayName = _newPackageDisplayName.Trim(),
                            Description = _newPackageDescription.Trim(),
                            IsEssential = _newPackageIsEssential,
                            IsEnabled = true
                        };
                        ToolsHubSettings.AddOrUpdatePackage(packageEntry);
                        _addingNewPackage = false;
                        RefreshPackageStatus(packageEntry.Id);
                    }
                }
            };

            PageLayout.DrawFooterButtons(formButtons, alignRight: true, maxPerRow: 3);

            EditorGUILayout.EndVertical();
        }

        // Package status methods
        internal async void RefreshPackageStatus(string specificPackageId = null)
        {
            if (_isRefreshingStatus) return; // Prevent multiple concurrent refreshes

            _isRefreshingStatus = true;

            try
            {
                // Check TMP resources status - only check once per refresh
                CheckTMPResourcesInstalled();

                var packages = ToolsHubSettings.GetAllPackages();

                if (packages == null || packages.Count == 0)
                {
                    _isRefreshingStatus = false;

                    return;
                }

                // If a specific package is specified, only refresh that one
                if (!string.IsNullOrEmpty(specificPackageId))
                {
                    var package = packages.Find(p => p.Id == specificPackageId);

                    if (package != null && package.DisplayName != TEXTMESHPRO_ID &&
                        package.Id != TEXTMESHPRO_PACKAGE_ID) await RefreshSinglePackageStatus(package);
                    _isRefreshingStatus = false;

                    return;
                }

                // First check regular packages (non-Git) to improve responsiveness
                foreach (var package in packages)
                    if (package.DisplayName != TEXTMESHPRO_ID &&
                        package.Id != TEXTMESHPRO_PACKAGE_ID &&
                        !PackageIdUtils.IsGitLike(package.Id))
                        await RefreshSinglePackageStatus(package);

                // Then check Git packages which are more prone to timeouts
                foreach (var package in packages)
                    if (package.DisplayName != TEXTMESHPRO_ID &&
                        package.Id != TEXTMESHPRO_PACKAGE_ID &&
                        PackageIdUtils.IsGitLike(package.Id))
                        await RefreshSinglePackageStatus(package);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error refreshing package status: {ex.Message}");
            }
            finally
            {
                _isRefreshingStatus = false;

                // Force UI to update
                EditorApplication.delayCall += () =>
                {
                    if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                        EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                };
            }
        }

        private async Task RefreshSinglePackageStatus(PackageEntry package)
        {
            try
            {
                // Limit debug logging for Git packages to reduce console spam
                var isGitPackage = PackageIdUtils.IsGitLike(package.Id);
                if (isGitPackage && Debug.isDebugBuild) Debug.Log($"Checking status for Git package: {package.Id}");

                var isInstalled = await PackageManager.IsPackageInstalled(package.Id);
                var info = await PackageManager.GetPackageInfo(package.Id);
                var hasUpdate = false;
                var version = "";
                var latest = "";

                if (isInstalled && info != null)
                {
                    // If we found a package, log its details only in debug builds
                    if (isGitPackage && Debug.isDebugBuild)
                        Debug.Log(
                            $"Found Git package: {package.Id} as {info.name} version {info.version}, source: {info.source}");

                    version = info.version;

                    // For registry packages, surface the latest version for display (and update state)
                    if (info.source == PackageSource.Registry)
                    {
                        latest = info.versions != null ? info.versions.latest : "";
                        if (!string.IsNullOrEmpty(latest) && latest != version)
                            hasUpdate = true;
                    }
                }

                _isPackageInstalled[package.Id] = isInstalled;
                _hasPackageUpdate[package.Id] = hasUpdate;
                _packageVersions[package.Id] = version;
                _packageLatestVersions[package.Id] = latest;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error refreshing status for package {package.Id}: {ex.Message}");

                // Set as not installed if there's an error
                _isPackageInstalled[package.Id] = false;
                _hasPackageUpdate[package.Id] = false;
                _packageVersions[package.Id] = "";
                _packageLatestVersions[package.Id] = "";
            }
        }

        // Installation methods
        private void StartPackageInstallation()
        {
            // First, ensure no TMP entries are in the list
            RemoveConflictingTMPEntries();

            var packagesToInstall = new List<string>();

            // Get all enabled packages except TextMeshPro
            foreach (var package in ToolsHubSettings.GetAllPackages())
            {
                // Strict filtering to ensure no TMP package gets installed
                var isTMPPackage = package.DisplayName == TEXTMESHPRO_ID ||
                                   package.Id == TEXTMESHPRO_PACKAGE_ID ||
                                   package.Id.Contains("textmeshpro");

                if (package.IsEnabled &&
                    !isTMPPackage &&
                    (!_isPackageInstalled.ContainsKey(package.Id) || !_isPackageInstalled[package.Id] ||
                     (_hasPackageUpdate.ContainsKey(package.Id) && _hasPackageUpdate[package.Id])))
                {
                    // Add debug log for Git packages
                    if (PackageIdUtils.IsGitLike(package.Id))
                        Debug.Log($"Adding Git package to installation queue: {package.Id}");

                    packagesToInstall.Add(package.Id);
                }
            }

            // Check if TextMeshPro resources need to be imported
            var needTMPImport = false;

            var tmpPackage = ToolsHubSettings.GetAllPackages().FirstOrDefault(p =>
                p.DisplayName == TEXTMESHPRO_ID);

            if (tmpPackage != null && tmpPackage.IsEnabled && !_tmpResourcesInstalled) needTMPImport = true;

            if (packagesToInstall.Count == 0 && !needTMPImport)
            {
                EditorUtility.DisplayDialog("No Packages to Install",
                    "No packages selected for installation or all selected packages are already installed.", "OK");

                return;
            }

            // Reset progress tracking
            _isInstalling = true;
            _packageProgress.Clear();
            _packageInstallSuccess.Clear();
            _overallProgress = 0;

            var totalCount = packagesToInstall.Count + (needTMPImport ? 1 : 0);
            var completedCount = 0;

            foreach (var packageId in packagesToInstall) _packageProgress[packageId] = 0;

            if (needTMPImport) _packageProgress[TEXTMESHPRO_ID] = 0;

            // Create progress tracker for packages
            var progress = new Progress<float>(p => _overallProgress = p);

            // Start installation of normal packages
            if (packagesToInstall.Count > 0)
            {
                // Double-check no TMP packages are in the list
                packagesToInstall = packagesToInstall
                    .Where(id => id != TEXTMESHPRO_PACKAGE_ID && !id.Contains("textmeshpro"))
                    .ToList();

                PackageManager.InstallPackages(packagesToInstall, progress);
            }

            // Import TMP resources if needed
            if (needTMPImport)
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        TMP_PackageResourceImporter.ImportResources(true, false, false);

                        _packageProgress[TEXTMESHPRO_ID] = 1.0f;
                        _packageInstallSuccess[TEXTMESHPRO_ID] = true;
                        CheckTMPResourcesInstalled();

                        completedCount++;
                        _overallProgress = (float)completedCount / totalCount;

                        if (packagesToInstall.Count == 0)
                        {
                            _isInstalling = false;
                            HandleAllPackagesComplete();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error importing TMP resources: {ex.Message}");
                        _packageProgress[TEXTMESHPRO_ID] = 1.0f;
                        _packageInstallSuccess[TEXTMESHPRO_ID] = false;
                    }
                };
        }

        private void AddDefaultPackages()
        {
            // First, ensure no TMP entries are in the list
            RemoveConflictingTMPEntries();

            // Add default essential packages
            var defaultPackages = new[]
            {
                new PackageEntry
                {
                    Id = "com.unity.cinemachine",
                    DisplayName = "Cinemachine",
                    Description = "Virtual camera system for dynamic and procedural camera control.",
                    IsEssential = true,
                    IsEnabled = true
                },
                new PackageEntry
                {
                    Id = "com.unity.inputsystem",
                    DisplayName = "Input System",
                    Description = "New input system for Unity with support for modern controllers and devices.",
                    IsEssential = true,
                    IsEnabled = true
                },
                // Don't add TextMeshPro here - we already handle it in RemoveConflictingTMPEntries
                new PackageEntry
                {
                    Id = "com.unity.addressables",
                    DisplayName = "Addressables",
                    Description = "Asset management system for optimizing memory and asset loading.",
                    IsEssential = false,
                    IsEnabled = true
                },
                new PackageEntry
                {
                    Id = "com.unity.burst",
                    DisplayName = "Burst Compiler",
                    Description = "Optimizing compiler for high-performance computation in Unity.",
                    IsEssential = false,
                    IsEnabled = true,
                    RequiresRestart = true
                },
                // Add Git packages
                new PackageEntry
                {
                    Id = "git+https://github.com/adammyhre/Unity-Utils.git",
                    DisplayName = "Unity Utility Library",
                    Description = "Collection of utility functions for Unity development.",
                    IsEssential = false,
                    IsEnabled = true
                },
                new PackageEntry
                {
                    Id = "git+https://github.com/adammyhre/Unity-Improved-Timers.git",
                    DisplayName = "Improved Timers",
                    Description = "Enhanced timer system for Unity projects.",
                    IsEssential = false,
                    IsEnabled = true
                }
            };

            foreach (var package in defaultPackages) ToolsHubSettings.AddOrUpdatePackage(package);
        }

        // Add a helper method to remove any conflicting TextMeshPro entries from settings
        private void RemoveConflictingTMPEntries()
        {
            var packages = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
            var hadConflictingEntries = false;

            foreach (var package in packages.ToArray())
            {
                var id = package?.Id ?? string.Empty;
                var name = package?.DisplayName ?? string.Empty;

                // Remove any package with the TextMeshPro package ID or any variations
                if (id.Equals(TEXTMESHPRO_PACKAGE_ID, StringComparison.OrdinalIgnoreCase) ||
                    (id.IndexOf("textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!name.Equals(TEXTMESHPRO_ID, StringComparison.OrdinalIgnoreCase) &&
                     name.IndexOf("Text Mesh Pro", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    ToolsHubManager.ShowNotification($"Removing conflicting TextMeshPro entry: {name}");
                    Debug.Log($"Removing conflicting TextMeshPro package entry: {id} / {name}");
                    if (!string.IsNullOrEmpty(id))
                        ToolsHubSettings.RemovePackage(id);
                    hadConflictingEntries = true;
                }
            }

            // Make sure the TMP entry exists with the correct ID format
            packages = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
            var tmpEntry = packages.FirstOrDefault(p =>
                (p?.DisplayName ?? string.Empty).Equals(TEXTMESHPRO_ID, StringComparison.OrdinalIgnoreCase) &&
                (p?.Id ?? string.Empty).Equals(TEXTMESHPRO_ID, StringComparison.OrdinalIgnoreCase));

            if (tmpEntry == null)
            {
                // Add or update the proper TextMeshPro entry
                ToolsHubSettings.AddOrUpdatePackage(new PackageEntry
                {
                    Id = TEXTMESHPRO_ID,
                    DisplayName = TEXTMESHPRO_ID,
                    Description =
                        "Advanced text rendering system with improved visual quality and performance. Resources need to be imported.",
                    IsEssential = true,
                    IsEnabled = true
                });
                hadConflictingEntries = true;
            }

            // If we found conflicts, save settings explicitly
            if (hadConflictingEntries)
            {
                ToolsHubManager.ShowNotification("Saved settings after removing conflicting TextMeshPro entries",
                    "success");
                ToolsHubSettings.Save();
            }
        }

        private int GetEnabledPackageCount()
        {
            var packages = ToolsHubSettings.GetAllPackages();

            if (packages == null)
                return 0;

            return packages.Count(p => p.IsEnabled);
        }

        private void CheckTMPResourcesInstalled(bool logResult = false) // Changed default to false
        {
            // Only check once per second at most
            if ((DateTime.Now - _lastTMPCheck).TotalSeconds < 1)
                return;

            _lastTMPCheck = DateTime.Now;

            // C#
            var dirA = "Assets/TextMesh Pro/Resources";
            var dirB = "Assets/Text Mesh Pro/Resources";

            var settingsA = Path.Combine(dirA, "TMP Settings.asset").Replace("\\", "/");
            var settingsB = Path.Combine(dirB, "TMP Settings.asset").Replace("\\", "/");

            // Check either directory and either settings asset
            var dirExists = Directory.Exists(dirA) || Directory.Exists(dirB);
            var settingsExists = File.Exists(settingsA) || File.Exists(settingsB);

            _tmpResourcesInstalled = dirExists && settingsExists;

            // Only log if requested and in debug builds
            if (logResult && Debug.isDebugBuild)
                Debug.Log($"TMP Resources check: {_tmpResourcesInstalled} - path exists: {dirExists}");
        }

        // ========== New helpers and batch removal ==========
        private int GetInstallableCount()
        {
            var list = ToolsHubSettings.GetAllPackages();
            if (list == null || list.Count == 0) return 0;

            var count = 0;
            foreach (var package in list)
            {
                var isTMP = package.DisplayName == TEXTMESHPRO_ID ||
                            package.Id == TEXTMESHPRO_PACKAGE_ID ||
                            package.Id.IndexOf("textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!package.IsEnabled || isTMP) continue;

                var notInstalled = !_isPackageInstalled.ContainsKey(package.Id) || !_isPackageInstalled[package.Id];
                var needsUpdate = _hasPackageUpdate.ContainsKey(package.Id) && _hasPackageUpdate[package.Id];
                if (notInstalled || needsUpdate) count++;
            }

            // Account for TMP resource import if enabled and missing
            var tmp = list.FirstOrDefault(p => p.DisplayName == TEXTMESHPRO_ID);
            if (tmp != null && tmp.IsEnabled && !_tmpResourcesInstalled) count++;

            return count;
        }

        private int GetRemovableCount()
        {
            var list = ToolsHubSettings.GetAllPackages();
            if (list == null || list.Count == 0) return 0;

            var count = 0;
            foreach (var package in list)
            {
                var isTMP = package.DisplayName == TEXTMESHPRO_ID ||
                            package.Id == TEXTMESHPRO_PACKAGE_ID ||
                            package.Id.IndexOf("textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!package.IsEnabled || isTMP) continue;
                if (_isPackageInstalled.ContainsKey(package.Id) && _isPackageInstalled[package.Id]) count++;
            }
            return count;
        }

        private void StartPackageRemoval()
        {
            var list = ToolsHubSettings.GetAllPackages();
            if (list == null || list.Count == 0) return;

            var toRemove = new List<string>();
            foreach (var package in list)
            {
                var isTMP = package.DisplayName == TEXTMESHPRO_ID ||
                            package.Id == TEXTMESHPRO_PACKAGE_ID ||
                            package.Id.IndexOf("textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!package.IsEnabled || isTMP) continue;
                if (_isPackageInstalled.ContainsKey(package.Id) && _isPackageInstalled[package.Id])
                    toRemove.Add(package.Id);
            }

            if (toRemove.Count == 0)
            {
                EditorUtility.DisplayDialog("No Packages to Remove",
                    "No enabled, installed packages were found to remove.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Packages",
                    $"Remove {toRemove.Count} selected package(s)?", "Remove", "Cancel"))
                return;

            _isInstalling = true;
            _overallProgress = 0f;

            int total = toRemove.Count;
            int completed = 0;

            void OneDone()
            {
                completed++;
                _overallProgress = Mathf.Clamp01((float)completed / total);
                if (completed >= total)
                {
                    _isInstalling = false;
                    RefreshPackageStatus();
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                            EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                    };
                }
            }

            foreach (var id in toRemove)
            {
                var capture = id;
                PackageManager.RemovePackage(capture, success => { OneDone(); });
            }
        }

        // ========= New UI helpers =========
        private void DrawPackageEntry(PackageEntry package)
        {
            // Determine special cases and state
            var isTMP = package.DisplayName == TEXTMESHPRO_ID || package.Id == TEXTMESHPRO_PACKAGE_ID ||
                        package.Id.IndexOf("textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0;
            var isGit = PackageIdUtils.IsGitLike(package.Id);
            var installed = _isPackageInstalled.ContainsKey(package.Id) && _isPackageInstalled[package.Id];
            var hasUpdate = _hasPackageUpdate.ContainsKey(package.Id) && _hasPackageUpdate[package.Id];
            var version = _packageVersions.ContainsKey(package.Id) ? _packageVersions[package.Id] : string.Empty;
            var latest = _packageLatestVersions.ContainsKey(package.Id) ? _packageLatestVersions[package.Id] : string.Empty;
            var inProgress = _packageProgress.ContainsKey(package.Id);

            // Card container
            PackageUIUtils.BeginCard();

            // Title row with optional enable checkbox on the left
            var title = string.IsNullOrEmpty(package.DisplayName) ? package.Id : package.DisplayName;
            PackageUIUtils.DrawTitleRow(title, !isTMP, package.IsEnabled, out var newEnabled);
            if (!isTMP && newEnabled != package.IsEnabled)
            {
                package.IsEnabled = newEnabled;
                ToolsHubSettings.AddOrUpdatePackage(package);
            }
            
            // Build all chips and draw with wrapping flow
            var std = PackageUIUtils.BuildStandardChips(
                package.IsEssential,
                isTMP ? _tmpResourcesInstalled : installed,
                hasUpdate,
                isGit,
                isTMP,
                version,
                latest
            );
            var meta = PackageUIUtils.BuildMetaChips(package);
            var allChips = new List<PackageUIUtils.TagChip>();
            allChips.AddRange(std);
            allChips.AddRange(meta);
            PackageUIUtils.DrawChipFlow(allChips);

            // Description
            PackageUIUtils.DrawDescription(package.Description);

            GUILayout.Space(4);

            // Build action buttons list
            var actions = new List<PackageUIUtils.ActionButton>();

            if (isTMP)
            {
                var importLabel = _tmpResourcesInstalled ? "Reimport TMP" : "Import TMP";
                actions.Add(new PackageUIUtils.ActionButton
                {
                    Label = importLabel,
                    Enabled = true,
                    Bg = InstallButtonColor,
                    OnClick = ImportTMPResources
                });
                actions.Add(new PackageUIUtils.ActionButton
                {
                    Label = "Remove",
                    Enabled = true,
                    Bg = DisabledButtonColor,
                    OnClick = RemoveTMPResources
                });
            }
            else
            {
                if (!installed)
                {
                    actions.Add(new PackageUIUtils.ActionButton
                    {
                        Label = "Install",
                        Enabled = package.IsEnabled && !_isInstalling && !_isRefreshingStatus,
                        Bg = InstallButtonColor,
                        OnClick = () => InstallSinglePackage(package.Id)
                    });
                }
                else
                {
                    if (hasUpdate)
                    {
                        actions.Add(new PackageUIUtils.ActionButton
                        {
                            Label = "Update",
                            Enabled = package.IsEnabled && !_isInstalling && !_isRefreshingStatus,
                            Bg = InstallButtonColor,
                            OnClick = () => InstallSinglePackage(package.Id)
                        });
                    }

                    actions.Add(new PackageUIUtils.ActionButton
                    {
                        Label = "Remove",
                        Enabled = !_isInstalling && !_isRefreshingStatus,
                        Bg = DisabledButtonColor,
                        OnClick = () => RemovePackage(package.Id)
                    });
                }
            }

            // Draw actions; for TMP include status on the same row
            if (isTMP)
            {
                var tmpStatus = _tmpResourcesInstalled ? "TMP Resources Imported" : "TMP Resources Missing";
                PackageUIUtils.DrawActionButtonsFlowWithLeftPrefix(tmpStatus, EditorStyles.miniBoldLabel, actions);
            }
            else
            {
                PackageUIUtils.DrawActionButtonsFlow(actions);
            }

            // Progress bar for this package
            if (inProgress)
            {
                var p = Mathf.Clamp01(_packageProgress[package.Id]);
                var rect = EditorGUILayout.GetControlRect(false, 16);
                EditorGUI.ProgressBar(rect, p, p > 0f && p < 1f ? "Working…" : (p >= 1f ? "Done" : string.Empty));
            }

            PackageUIUtils.EndCard();
        }
    }

    public static class EssentialPackagesPage
    {
        private static EssentialPackagesPageImpl _currentPageImpl;
        private static bool _refreshRequested;

        // Static constructor might also be problematic - move to delayCall
        static EssentialPackagesPage()
        {
            // Clean up package settings when the class is first loaded - defer to avoid constructor issues
            EditorApplication.delayCall += CleanupPackageSettings;
        }

        // Helper to clean up package settings
        private static void CleanupPackageSettings()
        {
            try
            {
                var packages = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
                if (packages.Count == 0) return;
                var hadConflictingEntries = false;

                // Remove any problematic TextMeshPro entries
                foreach (var package in packages.ToArray())
                {
                    var id = package?.Id ?? string.Empty;
                    var name = package?.DisplayName ?? string.Empty;

                    // Check for problematic entries
                    if (id.Equals("com.unity.textmeshpro", StringComparison.OrdinalIgnoreCase) ||
                        (!id.Equals("TextMeshPro", StringComparison.OrdinalIgnoreCase) &&
                         id.IndexOf("textmeshpro", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!name.Equals("TextMeshPro", StringComparison.OrdinalIgnoreCase) &&
                         name.IndexOf("Text Mesh Pro", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        if (!string.IsNullOrEmpty(id))
                            ToolsHubSettings.RemovePackage(id);
                        hadConflictingEntries = true;
                    }
                }

                // Save if we made changes
                if (hadConflictingEntries)
                {
                    ToolsHubManager.ShowNotification("Cleaned up package settings during startup", "success");
                    ToolsHubSettings.Save();
                }
            }
            catch (Exception ex)
            {
                ToolsHubManager.ShowNotification("Error cleaning up package settings", "error");
                Debug.LogError($"Error cleaning up package settings: {ex.Message}");
            }
        }

        [MenuButtonItem("Setup/Packages", "Manage Packages", "Install and manage essential & recommended packages",
            true)]
        public static void ShowPackagesPage()
        {
            _currentPageImpl = new EssentialPackagesPageImpl();
            _currentPageImpl.ShowInToolsHub();
        }

        // This method is needed for the ToolsHub menu system to find the Draw method
        public static void ShowPackagesPage_Page()
        {
            if (_currentPageImpl == null)
            {
                _currentPageImpl = new EssentialPackagesPageImpl();
                // The initial refresh is now scheduled by InitializePackagePage
                // No need to explicitly request a refresh here
            }
            else if (!_refreshRequested)
            {
                // Only schedule a refresh if needed and not already pending
                _refreshRequested = true;

                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        if (_currentPageImpl != null) _currentPageImpl.RefreshPackageStatus();
                    }
                    finally
                    {
                        _refreshRequested = false;
                    }
                };
            }

            // Ensure the page binds the header refresh action any time it's drawn
            _currentPageImpl.BindRefreshHook();

            _currentPageImpl.DrawPage();
        }
    }
}
#endif

