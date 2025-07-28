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
        private static readonly Color AddButtonColor = new(0.3f, 0.5f, 0.9f, 1f);
        private bool _addingNewPackage;
        private readonly Dictionary<string, bool> _hasPackageUpdate = new();

        // Installation state
        private bool _isInstalling;

        // Package status tracking
        private readonly Dictionary<string, bool> _isPackageInstalled = new();
        private bool _isRefreshingStatus;

        // Check if TextMeshPro resources are already installed
        private DateTime _lastTMPCheck = DateTime.MinValue;
        private string _newPackageDescription = "";
        private string _newPackageDisplayName = "";

        // UI state
        private string _newPackageId = "";
        private bool _newPackageIsEssential;
        private float _overallProgress;
        private readonly Dictionary<string, bool> _packageInstallSuccess = new();
        private readonly Dictionary<string, float> _packageProgress = new();
        private readonly Dictionary<string, string> _packageVersions = new();
        private Vector2 _scrollPosition;

        // Filter/search
        private string _searchFilter = "";
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

        // Page implementation
        public string Title => "Manage Essential & Recommended Packages";
        public string Description => "Install and manage essential and recommended packages for your project";

        public void DrawContentHeader()
        {
            // Search bar
            EditorGUILayout.BeginHorizontal();

            _searchFilter = EditorGUILayout.TextField("Filter:", _searchFilter);

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
                _searchFilter = "";

            EditorGUILayout.EndHorizontal();

            // Filter toggles
            EditorGUILayout.BeginHorizontal();

            _showOnlyEnabled = EditorGUILayout.ToggleLeft("Enabled Only", _showOnlyEnabled, GUILayout.Width(110));
            _showOnlyEssential = EditorGUILayout.ToggleLeft("Essential Only", _showOnlyEssential, GUILayout.Width(110));
            _showOnlyInstalled = EditorGUILayout.ToggleLeft("Installed Only", _showOnlyInstalled, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            GUI.enabled = !_isRefreshingStatus && !_isInstalling;
            if (GUILayout.Button("Refresh Status", GUILayout.Width(110))) RefreshPackageStatus();
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Overall progress when installing
            if (_isInstalling)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Installing Packages... {_overallProgress:P0}", EditorStyles.boldLabel);
                var progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, _overallProgress, "");
                EditorGUILayout.Space(5);
            }
        }

        public void DrawScrollContent()
        {
            var packages = ToolsHubSettings.GetAllPackages();

            // Default packages if none exist yet
            if (packages == null || packages.Count == 0)
            {
                AddDefaultPackages();
                packages = ToolsHubSettings.GetAllPackages();
            }

            // Apply filters
            packages = FilterPackages(packages);

            // Group packages by category (sort essentials first)
            var groupedPackages = packages
                .OrderByDescending(p => p.IsEssential)
                .ThenBy(p => p.DisplayName)
                .ToList();

            // Draw packages
            foreach (var package in groupedPackages) DrawPackageEntry(package);
        }

        // Page implementation
        public void DrawContentFooter()
        {
            // Empty - moved buttons to the main footer
        }

        public void DrawFooter()
        {
            // Add New Package section
            if (_addingNewPackage)
            {
                DrawAddNewPackageForm();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = AddButtonColor;

                if (GUILayout.Button("Add New Package", GUILayout.Height(30), GUILayout.Width(150)))
                {
                    _addingNewPackage = true;
                    _newPackageId = "";
                    _newPackageDisplayName = "";
                    _newPackageDescription = "";
                    _newPackageIsEssential = false;
                }

                GUI.backgroundColor = originalColor;

                EditorGUILayout.EndHorizontal();
            }

            // Install selected button
            if (GetEnabledPackageCount() > 0)
            {
                EditorGUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUI.enabled = !_isInstalling && !_isRefreshingStatus;
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = InstallButtonColor;

                if (GUILayout.Button($"Install Selected ({GetEnabledPackageCount()} packages)",
                        GUILayout.Height(30), GUILayout.Width(250)))
                    StartPackageInstallation();
                GUI.backgroundColor = originalColor;
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.FlexibleSpace();

            if (PageLayout.DrawCancelButton(100))
            {
                // Unregister events
                PackageManager.OnPackageInstallProgress -= HandlePackageInstallProgress;
                PackageManager.OnPackageInstallComplete -= HandlePackageInstallComplete;
                PackageManager.OnPackageInstallError -= HandlePackageInstallError;
                PackageManager.OnAllPackagesInstallComplete -= HandleAllPackagesComplete;
            }
        }

        public void OnPageResult(PageOperationResult result)
        {
            // Do nothing
        }

        // New method to initialize everything after constructor completes
        private void InitializePackagePage()
        {
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
            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManger>().Repaint(); };
        }

        private void HandlePackageInstallComplete(string packageId, bool success)
        {
            _packageInstallSuccess[packageId] = success;

            RefreshPackageStatus(packageId);
            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManger>().Repaint(); };
        }

        private void HandlePackageInstallError(string packageId)
        {
            _packageInstallSuccess[packageId] = false;
            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManger>().Repaint(); };
        }

        private void HandleAllPackagesComplete()
        {
            _isInstalling = false;
            RefreshPackageStatus();
            EditorApplication.delayCall += () => { EditorWindow.GetWindow<ToolsHubManger>().Repaint(); };
        }

        private List<PackageEntry> FilterPackages(List<PackageEntry> packages)
        {
            if (string.IsNullOrWhiteSpace(_searchFilter) && !_showOnlyEnabled &&
                !_showOnlyInstalled && !_showOnlyEssential)
                return packages;

            return packages.Where(p =>
                (string.IsNullOrWhiteSpace(_searchFilter) ||
                 p.DisplayName.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 p.Id.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 p.Description.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0) &&
                (!_showOnlyEnabled || p.IsEnabled) &&
                (!_showOnlyEssential || p.IsEssential) &&
                (!_showOnlyInstalled || (_isPackageInstalled.ContainsKey(p.Id) && _isPackageInstalled[p.Id]))
            ).ToList();
        }

        private void InstallSinglePackage(string packageId)
        {
            _isInstalling = true;
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

        private void DrawPackageEntry(PackageEntry package)
        {
            // Entry container
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Special handling for TextMeshPro which is built-in but needs resources imported
            var isTMPPackage = package.DisplayName == TEXTMESHPRO_ID;

            // Status flags (different for TMP)
            var isInstalled = isTMPPackage
                ? _tmpResourcesInstalled
                : _isPackageInstalled.ContainsKey(package.Id) && _isPackageInstalled[package.Id];

            var hasUpdate = !isTMPPackage && _hasPackageUpdate.ContainsKey(package.Id) && _hasPackageUpdate[package.Id];
            var version = !isTMPPackage && _packageVersions.ContainsKey(package.Id) ? _packageVersions[package.Id] : "";

            // Is this a Git package?
            var isGitPackage = package.Id.StartsWith("git+");

            // Header row with name, status, and toggle
            EditorGUILayout.BeginHorizontal();

            // Status icon and name
            var titleStyle = new GUIStyle(EditorStyles.boldLabel);

            if (package.IsEssential)
                titleStyle.normal.textColor = new Color(0.9f, 0.7f, 0.1f);

            // Toggle for enabled
            var wasEnabled = package.IsEnabled;
            package.IsEnabled = EditorGUILayout.Toggle(package.IsEnabled, GUILayout.Width(16));
            if (wasEnabled != package.IsEnabled) ToolsHubSettings.SetPackageEnabled(package.Id, package.IsEnabled);

            // Display name with icon
            var displayText = package.DisplayName;

            // Add Git tag for Git packages
            if (isGitPackage) displayText += " [Git]";

            // Add essential/recommended tag
            displayText += package.IsEssential ? " (Essential)" : " (Recommended)";

            EditorGUILayout.LabelField(new GUIContent(
                    displayText,
                    isInstalled ? EditorGUIUtility.IconContent("Installed").image : null),
                titleStyle);

            // Version info
            if (!string.IsNullOrEmpty(version))
            {
                var versionStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

                EditorGUILayout.LabelField(hasUpdate ? $"v{version} (Update Available)" : $"v{version}",
                    versionStyle, GUILayout.Width(150));
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndHorizontal();

            // Description
            var descriptionStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(package.Description, descriptionStyle);

            // Progress bar during installation
            if (_isInstalling && _packageProgress.ContainsKey(package.Id))
            {
                var progress = _packageProgress[package.Id];
                var progressRect = EditorGUILayout.GetControlRect(false, 10);
                EditorGUI.ProgressBar(progressRect, progress, "");

                if (_packageInstallSuccess.ContainsKey(package.Id))
                {
                    var success = _packageInstallSuccess[package.Id];

                    EditorGUILayout.LabelField(success ? "Installation completed" : "Installation failed",
                        success
                            ? EditorStyles.miniBoldLabel
                            : new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.red } });
                }
            }

            // Action buttons
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            var originalColor = GUI.backgroundColor;

            // Install button - special handling for TextMeshPro
            GUI.enabled = !_isInstalling && !_isRefreshingStatus && package.IsEnabled;

            if (!isInstalled)
            {
                GUI.backgroundColor = InstallButtonColor;

                // Different button text/action for TMP
                if (isTMPPackage)
                {
                    if (GUILayout.Button("Import TMP Resources", GUILayout.Width(150))) ImportTMPResources();
                }
                else
                {
                    // Show install for Git packages too
                    if (GUILayout.Button("Install", GUILayout.Width(80))) InstallSinglePackage(package.Id);
                }
            }
            else if (hasUpdate)
            {
                GUI.backgroundColor = InstallButtonColor;
                if (GUILayout.Button("Update", GUILayout.Width(80))) InstallSinglePackage(package.Id);
            }
            else
            {
                // Don't show remove button for TextMeshPro - it can't be removed
                if (!isTMPPackage)
                {
                    GUI.backgroundColor = DisabledButtonColor;
                    if (GUILayout.Button("Remove", GUILayout.Width(80))) RemovePackage(package.Id);
                }
                else if (isInstalled)
                {
                    // For TextMeshPro, provide option to reimport resources
                    GUI.backgroundColor = InstallButtonColor;
                    if (GUILayout.Button("Reimport TMP Resources", GUILayout.Width(180))) ImportTMPResources();

                    // Add the remove TMP resources button
                    GUI.backgroundColor = DisabledButtonColor;
                    if (GUILayout.Button("Remove TMP Resources", GUILayout.Width(180))) RemoveTMPResources();
                }
            }

            GUI.backgroundColor = originalColor;
            GUI.enabled = true;

            // Remove from list button (only for non-essential packages)
            GUI.enabled = !_isInstalling && !_isRefreshingStatus && !package.IsEssential;

            if (GUILayout.Button("✕", GUILayout.Width(25)))
                if (EditorUtility.DisplayDialog("Remove Package",
                        $"Remove {package.DisplayName} from the package list?", "Yes", "Cancel"))
                    ToolsHubSettings.RemovePackage(package.Id);

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
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

                        EditorWindow.GetWindow<ToolsHubManger>().Repaint();
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
                    ToolsHubManger.ShowNotification("Deleted TMP Essentials.", "success");
                }

                if (Directory.Exists(examplesPath))
                {
                    FileUtil.DeleteFileOrDirectory(examplesPath);
                    FileUtil.DeleteFileOrDirectory(examplesPath + ".meta");
                    changed = true;
                    ToolsHubManger.ShowNotification("Deleted TMP Examples & Extras.", "success");
                }

                if (changed)
                    AssetDatabase.Refresh();
                else
                    ToolsHubManger.ShowNotification("No TMP resources found to delete.");
            }
            catch (Exception ex)
            {
                ToolsHubManger.ShowNotification("Error removing TMP resources", "error");
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
                                    "• Git: git+https://github.com/username/repo.git\n" +
                                    "• Local: file:/path/to/package",
                MessageType.Info);

            EditorGUILayout.Space(5);

            // Buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cancel")) _addingNewPackage = false;

            var canSave = !string.IsNullOrWhiteSpace(_newPackageId) &&
                          !string.IsNullOrWhiteSpace(_newPackageDisplayName);

            GUI.enabled = canSave;
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = AddButtonColor;

            if (GUILayout.Button("Add Package"))
            {
                // Add the package to settings
                var packageEntry = new PackageEntry
                {
                    Id = _newPackageId.Trim(),
                    DisplayName = _newPackageDisplayName.Trim(),
                    Description = _newPackageDescription.Trim(),
                    IsEssential = _newPackageIsEssential,
                    IsEnabled = true
                };

                ToolsHubSettings.AddOrUpdatePackage(packageEntry);

                // Reset form
                _addingNewPackage = false;

                // Refresh status
                RefreshPackageStatus(packageEntry.Id);
            }

            GUI.backgroundColor = originalColor;
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

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
                        !package.Id.StartsWith("git+"))
                        await RefreshSinglePackageStatus(package);

                // Then check Git packages which are more prone to timeouts
                foreach (var package in packages)
                    if (package.DisplayName != TEXTMESHPRO_ID &&
                        package.Id != TEXTMESHPRO_PACKAGE_ID &&
                        package.Id.StartsWith("git+"))
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
                    if (EditorWindow.HasOpenInstances<ToolsHubManger>())
                        EditorWindow.GetWindow<ToolsHubManger>().Repaint();
                };
            }
        }

        private async Task RefreshSinglePackageStatus(PackageEntry package)
        {
            try
            {
                // Limit debug logging for Git packages to reduce console spam
                var isGitPackage = package.Id.StartsWith("git+");
                if (isGitPackage && Debug.isDebugBuild) Debug.Log($"Checking status for Git package: {package.Id}");

                var isInstalled = await PackageManager.IsPackageInstalled(package.Id);
                var info = await PackageManager.GetPackageInfo(package.Id);
                var hasUpdate = false;
                var version = "";

                if (isInstalled && info != null)
                {
                    // If we found a package, log its details only in debug builds
                    if (isGitPackage && Debug.isDebugBuild)
                        Debug.Log(
                            $"Found Git package: {package.Id} as {info.name} version {info.version}, source: {info.source}");

                    version = info.version;
                    hasUpdate = await PackageManager.HasPackageUpdate(package.Id);
                }
                else if (isGitPackage && Debug.isDebugBuild)
                {
                    // If Git package not found, log that fact only in debug builds
                    Debug.Log($"Git package not found: {package.Id}");
                }

                _isPackageInstalled[package.Id] = isInstalled;
                _hasPackageUpdate[package.Id] = hasUpdate;
                _packageVersions[package.Id] = version;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error refreshing status for package {package.Id}: {ex.Message}");

                // Set as not installed if there's an error
                _isPackageInstalled[package.Id] = false;
                _hasPackageUpdate[package.Id] = false;
                _packageVersions[package.Id] = "";
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
                    if (package.Id.StartsWith("git+"))
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
                    IsEnabled = true
                },
                // Add Git packages
                new PackageEntry
                {
                    Id = "git+https://github.com/adammyhre/Unity-Utils.git",
                    DisplayName = "Adam's Unity Utils",
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
            var packages = ToolsHubSettings.GetAllPackages();
            var hadConflictingEntries = false;

            foreach (var package in packages.ToArray())
                // Remove any package with the TextMeshPro package ID or any variations
                if (package.Id == TEXTMESHPRO_PACKAGE_ID ||
                    package.Id.Contains("textmeshpro") ||
                    (package.DisplayName != TEXTMESHPRO_ID &&
                     package.DisplayName.Contains("Text Mesh Pro")))
                {
                    ToolsHubManger.ShowNotification($"Removing conflicting TextMeshPro entry: {package.DisplayName}");
                    Debug.Log($"Removing conflicting TextMeshPro package entry: {package.Id} / {package.DisplayName}");
                    ToolsHubSettings.RemovePackage(package.Id);
                    hadConflictingEntries = true;
                }

            // Make sure the TMP entry exists with the correct ID format
            var tmpEntry = packages.FirstOrDefault(p => p.DisplayName == TEXTMESHPRO_ID && p.Id == TEXTMESHPRO_ID);

            if (tmpEntry == null)
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

            // If we found conflicts, save settings explicitly
            if (hadConflictingEntries)
            {
                ToolsHubManger.ShowNotification("Saved settings after removing conflicting TextMeshPro entries",
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

            var settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

            // Check if directory exists first (faster than checking file)
            var dirExists = Directory.Exists("Assets/TextMesh Pro/Resources");
            _tmpResourcesInstalled = dirExists && File.Exists(settingsPath);

            // Only log if requested and in debug builds
            if (logResult && Debug.isDebugBuild)
                Debug.Log($"TMP Resources check: {_tmpResourcesInstalled} - path exists: {dirExists}");
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
                var packages = ToolsHubSettings.GetAllPackages();
                var hadConflictingEntries = false;

                // Remove any problematic TextMeshPro entries
                foreach (var package in packages.ToArray())
                    // Check for problematic entries
                    if (package.Id == "com.unity.textmeshpro" ||
                        (package.Id != "TextMeshPro" && package.Id.Contains("textmeshpro")) ||
                        (package.DisplayName != "TextMeshPro" && package.DisplayName.Contains("Text Mesh Pro")))
                    {
                        ToolsHubSettings.RemovePackage(package.Id);
                        hadConflictingEntries = true;
                    }

                // Save if we made changes
                if (hadConflictingEntries)
                {
                    ToolsHubManger.ShowNotification("Cleaned up package settings during startup", "success");
                    ToolsHubSettings.Save();
                }
            }
            catch (Exception ex)
            {
                ToolsHubManger.ShowNotification("Error cleaning up package settings", "error");
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

            _currentPageImpl.DrawPage();
        }
    }
}
#endif