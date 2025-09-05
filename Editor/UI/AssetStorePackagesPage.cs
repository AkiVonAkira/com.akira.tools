#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using akira.EditorServices;
using akira.Packages;
using akira.ToolsHub;
using UnityEditor;
using UnityEngine;
using ActionButton = akira.UI.PackageUIUtils.ActionButton;
using akira.AssetStoreNative;

namespace akira.UI
{
    public class AssetStorePackagesPageImpl : IToolsHubPage
    {
        // UI colors
        private static readonly Color InstallButtonColor = new(0.3f, 0.5f, 0.9f, 1f);
        private static readonly Color SecondaryButtonColor = new(0.45f, 0.45f, 0.45f, 1f);

        // UI state
        private string _search = "";
        private bool _addingNew;
        private bool _hideOwned; 

        // New entry fields
        private string _url = "";
        private string _displayName = "";
        private string _description = "";

        // Category filters
        private string _categoryFilter = "All";
        private string _subcategoryFilter = "All";

        // Sorting
        private enum SortField { Name = 0, Author = 1 }
        private SortField _sortField = SortField.Name;
        private bool _sortAscending = true;

        // Group foldouts
        private bool _foldOwned = true;
        private bool _foldFreeUnowned = true;
        private bool _foldPaidUnowned = true;
        private bool _foldUnknown = false;
        // New foldouts
        private bool _foldUpdate = true;
        private bool _foldInstalled = true;

        // Collapse state per card and last visible keys for bulk toggle
        private readonly HashSet<string> _collapsedKeys = new(StringComparer.Ordinal);
        private HashSet<string> _lastVisibleKeys = new(StringComparer.Ordinal);
        private bool _initialCollapseApplied; // once-per-editor flag

        // Cached per-product overview info (avoids changing PackageEntry)
        private class ProductOverview
        {
            public string Description;
            public string Version;
            public float? RatingAverage; // 0-5
            public int? RatingCount;
            public DateTime? PurchasedDate;
            public long? DownloadSizeBytes;
            public int? AssetCount;
            public List<string> Labels;
        }
        private readonly Dictionary<long, ProductOverview> _overviewByPid = new();
        private readonly HashSet<long> _overviewRequested = new();

        public AssetStorePackagesPageImpl()
        {
            // Set page-level refresh: only re-fetch items older than TTL
            ToolsHubManager.SetPageRefreshHandler(RefreshStaleEntries);
            // Load persisted UI prefs
            _hideOwned = EditorPrefs.GetBool("akira.assetStore.hideOwned", false);
            
            // New: proactively fetch ownership on first load when available
            EditorApplication.delayCall += () =>
            {
                try { RefreshStaleEntries(); } catch (Exception) { }
            };

            // Load sort prefs
            _sortField = (SortField)EditorPrefs.GetInt("akira.assetStore.sortField", (int)SortField.Name);
            _sortAscending = EditorPrefs.GetBool("akira.assetStore.sortAsc", true);
            _foldOwned = EditorPrefs.GetBool("akira.assetStore.fold.owned", true);
            _foldFreeUnowned = EditorPrefs.GetBool("akira.assetStore.fold.free", true);
            _foldPaidUnowned = EditorPrefs.GetBool("akira.assetStore.fold.paid", true);
            _foldUnknown = EditorPrefs.GetBool("akira.assetStore.fold.unknown", false);
            _foldUpdate = EditorPrefs.GetBool("akira.assetStore.fold.update", true);
            _foldInstalled = EditorPrefs.GetBool("akira.assetStore.fold.installed", true);
            _initialCollapseApplied = EditorPrefs.GetBool("akira.assetStore.initialCollapseApplied", false);
        }

        // Allow static wrapper to re-bind the header Refresh button when page becomes active again
        public void BindRefreshHook()
        {
            ToolsHubManager.SetPageRefreshHandler(RefreshStaleEntries);
        }

        public string Title => "Manage Asset Store Packages";
        public string Description => "Add and track Asset Store Packages with recommended assets to get you started.";

        private void RefreshStaleEntries()
        {
            var all = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
            var assets = all.Where(p => p.IsAssetStore == true || !string.IsNullOrEmpty(p.AssetStoreUrl)).ToList();
            var staleCount = 0;

            foreach (var e in assets)
            {
                if (AssetStoreInfoService.IsStale(e))
                {
                    staleCount++;
                    AssetStoreInfoService.QueueFetchIfStale(e);
                }
            }

            if (staleCount > 0)
                ToolsHubManager.ShowNotification($"Refreshing {staleCount} asset(s) …");
            else
                ToolsHubManager.ShowNotification("All asset info is up to date.");

            // New: preload native ownership when available and signed in
            if (AssetStoreService.IsAvailable && AssetStoreService.IsSignedIn)
            {
                AssetStoreService.GetAllOwnedProducts(ids =>
                {
                    try
                    {
                        var owned = new HashSet<long>(ids ?? new List<long>());
                        var list = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
                        var touched = 0;
                        foreach (var e in list)
                        {
                            if (!string.IsNullOrEmpty(e.AssetStoreId) && long.TryParse(e.AssetStoreId, out var pid))
                            {
                                var val = owned.Contains(pid);
                                if (e.IsOwned != val)
                                {
                                    e.IsOwned = val;
                                    touched++;
                                }
                            }
                        }
                        if (touched > 0)
                        {
                            ToolsHubSettings.Save();
                            // repaint
                            EditorApplication.delayCall += () =>
                            {
                                if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                                    EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                            };
                        }
                    }
                    catch (Exception) { }
                }, _ => { /* best-effort; ignore */ });
            }
        }

        public void DrawContentHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(42));
            _search = EditorGUILayout.TextField(_search ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) _search = "";
            EditorGUILayout.EndHorizontal();

            // Category/subcategory dropdowns + filters
            DrawCategoryFilters();

            // Sorting row
            DrawSortControls();
        }

        private void DrawSortControls()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sort By:", GUILayout.Width(60));
            var options = new[] { "Name", "Author" };
            var newSortIndex = EditorGUILayout.Popup((int)_sortField, options, GUILayout.MaxWidth(160));
            if (newSortIndex != (int)_sortField)
            {
                _sortField = (SortField)newSortIndex;
                EditorPrefs.SetInt("akira.assetStore.sortField", (int)_sortField);
            }

            GUILayout.Space(8);
            var ascLabel = _sortAscending ? "Ascending" : "Descending";
            if (GUILayout.Button(ascLabel, GUILayout.Width(100)))
            {
                _sortAscending = !_sortAscending;
                EditorPrefs.SetBool("akira.assetStore.sortAsc", _sortAscending);
            }

            GUILayout.Space(12);
            using (new EditorGUI.DisabledScope(_lastVisibleKeys == null || _lastVisibleKeys.Count == 0))
            {
                if (GUILayout.Button("Collapse All", GUILayout.Width(100)))
                {
                    _collapsedKeys.UnionWith(_lastVisibleKeys);
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                            EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                    };
                }
                if (GUILayout.Button("Expand All", GUILayout.Width(100)))
                {
                    _collapsedKeys.ExceptWith(_lastVisibleKeys);
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                            EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                    };
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryFilters()
        {
            var all = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
            var assets = all.Where(p => p.IsAssetStore == true || !string.IsNullOrEmpty(p.AssetStoreUrl)).ToList();

            // Build categories from AssetCategory; fallback to URL parsing (top-level after /packages/)
            static string TopCategory(PackageEntry e)
            {
                if (!string.IsNullOrEmpty(e.AssetCategory)) return e.AssetCategory.Split('/')[0];
                var url = e.AssetStoreUrl ?? string.Empty;
                try
                {
                    var uri = new Uri(url);
                    var segs = uri.AbsolutePath.Trim('/').Split('/');
                    var idx = Array.FindIndex(segs, s => string.Equals(s, "packages", StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0 && idx + 1 < segs.Length)
                    {
                        var cat = segs[idx + 1].Replace('-', ' ');
                        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cat);
                    }
                }
                catch { }
                return "Uncategorized";
            }

            static string SubCategory(PackageEntry e)
            {
                if (!string.IsNullOrEmpty(e.AssetCategory))
                {
                    var parts = e.AssetCategory.Split('/');
                    if (parts.Length > 1) return parts[1];
                }
                return "(None)";
            }

            var categories = new List<string> { "All" };
            categories.AddRange(assets.Select(TopCategory).Distinct().OrderBy(s => s));

            EditorGUILayout.BeginHorizontal();

            // Only show Category dropdown if there are actual choices beyond 'All'
            if (categories.Count > 1)
            {
                EditorGUILayout.LabelField("Category:", GUILayout.Width(70));
                var catIndex = Mathf.Max(0, categories.IndexOf(_categoryFilter));
                var newCatIndex = EditorGUILayout.Popup(catIndex, categories.ToArray(), GUILayout.MaxWidth(220));
                if (newCatIndex != catIndex)
                {
                    _categoryFilter = categories[newCatIndex];
                    _subcategoryFilter = "All"; // reset
                }
            }

            // Subcategories based on selected category
            IEnumerable<PackageEntry> baseSet = assets;
            if (_categoryFilter != "All") baseSet = baseSet.Where(e => TopCategory(e) == _categoryFilter);
            var subcats = new List<string> { "All" };
            subcats.AddRange(baseSet.Select(SubCategory).Distinct().OrderBy(s => s));

            // Only show Sub dropdown if there are actual choices beyond 'All'
            if (subcats.Count > 1)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Sub:", GUILayout.Width(30));
                var subIndex = Mathf.Max(0, subcats.IndexOf(_subcategoryFilter));
                var newSubIndex = EditorGUILayout.Popup(subIndex, subcats.ToArray(), GUILayout.MaxWidth(200));
                if (newSubIndex != subIndex) _subcategoryFilter = subcats[newSubIndex];
            }

            // Hide Owned toggle and Reset are always shown
            GUILayout.Space(8);
            var newHideOwned = EditorGUILayout.ToggleLeft("Hide Owned", _hideOwned, GUILayout.Width(110));
            if (newHideOwned != _hideOwned)
            {
                _hideOwned = newHideOwned;
                EditorPrefs.SetBool("akira.assetStore.hideOwned", _hideOwned);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset Filters", GUILayout.Width(110)))
            {
                _categoryFilter = "All";
                _subcategoryFilter = "All";
                _search = string.Empty;
                _hideOwned = false;
                EditorPrefs.SetBool("akira.assetStore.hideOwned", _hideOwned);
            }
            EditorGUILayout.EndHorizontal();
        }

        public void DrawScrollContent()
        {
            // Temporarily hide ownership/price chips for this page
            var prevOwn = PackageUIUtils.ShowOwnershipChip;
            var prevPrice = PackageUIUtils.ShowPriceChip;
            try
            {
                PackageUIUtils.ShowOwnershipChip = false;
                PackageUIUtils.ShowPriceChip = false;

                var all = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
                var list = all
                    .Where(p => p.IsAssetStore == true || !string.IsNullOrEmpty(p.AssetStoreUrl))
                    .ToList();

                // Apply text search first
                if (!string.IsNullOrWhiteSpace(_search))
                {
                    list = list.Where(p =>
                        (p.DisplayName ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (p.Description ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (p.AssetStoreUrl ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                    ).ToList();
                }

                // Apply category filters
                static string TopCategory(PackageEntry e)
                {
                    if (!string.IsNullOrEmpty(e.AssetCategory)) return e.AssetCategory.Split('/')[0];
                    return "Uncategorized";
                }
                static string SubCategory(PackageEntry e)
                {
                    if (!string.IsNullOrEmpty(e.AssetCategory))
                    {
                        var parts = e.AssetCategory.Split('/');
                        if (parts.Length > 1) return parts[1];
                    }
                    return "(None)";
                }

                if (_categoryFilter != "All")
                    list = list.Where(e => TopCategory(e) == _categoryFilter).ToList();
                if (_subcategoryFilter != "All")
                {
                    if (_subcategoryFilter == "(None)")
                        list = list.Where(e => SubCategory(e) == "(None)").ToList();
                    else
                        list = list.Where(e => SubCategory(e) == _subcategoryFilter).ToList();
                }

                // Hide entries marked as Owned when toggle is active
                if (_hideOwned)
                    list = list.Where(e => e.IsOwned != true).ToList();

                // Track visible keys for bulk collapse/expand (for next frame buttons)
                _lastVisibleKeys = new HashSet<string>(list.Select(e => e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty)
                    .Where(k => !string.IsNullOrEmpty(k)), StringComparer.Ordinal);

                // Apply initial collapse once per page usage
                if (!_initialCollapseApplied && _lastVisibleKeys.Count > 0)
                {
                    _collapsedKeys.UnionWith(_lastVisibleKeys);
                    _initialCollapseApplied = true;
                    EditorPrefs.SetBool("akira.assetStore.initialCollapseApplied", true);
                }

                if (list.Count == 0)
                {
                    EditorGUILayout.HelpBox("No Asset Store packages found. Use 'Add New' to create entries.", MessageType.Info);
                    return;
                }

                // Ensure details fetch if missing and prefetch overview (for versions)
                foreach (var entry in list)
                {
                    if (string.IsNullOrEmpty(entry.Price) && !entry.IsFree.HasValue && !string.IsNullOrEmpty(entry.AssetStoreUrl))
                        AssetStoreInfoService.QueueFetch(entry);
                    if (!string.IsNullOrEmpty(entry.AssetStoreId) && long.TryParse(entry.AssetStoreId, out var pid))
                        TryRequestOverview(pid);
                }

                // ---- Version-aware grouping helpers ----
                bool HasAnyTrackedVersion(string k)
                {
                    return !string.IsNullOrEmpty(GetInstalledVersion(k)) || !string.IsNullOrEmpty(GetDownloadedVersion(k));
                }

                bool IsImportedPid(PackageEntry e)
                {
                    if (string.IsNullOrEmpty(e.AssetStoreId) || !long.TryParse(e.AssetStoreId, out var pid)) return false;
                    try { return AssetStoreBridge.IsImported(pid); } catch { return false; }
                }

                bool IsUpdate(PackageEntry e)
                {
                    var k = e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty;
                    if (string.IsNullOrEmpty(e.AssetStoreId) || !long.TryParse(e.AssetStoreId, out var pid)) return false;
                    if (!_overviewByPid.TryGetValue(pid, out var ov) || string.IsNullOrWhiteSpace(ov?.Version)) return false;
                    var avail = ov.Version;
                    var baseVer = MaxVersion(GetInstalledVersion(k), GetDownloadedVersion(k));
                    if (string.IsNullOrWhiteSpace(baseVer)) return false; // no prior install/download => not an update
                    return CompareVersions(avail, baseVer) > 0;
                }

                bool IsInstalled(PackageEntry e)
                {
                    // Prefer authoritative import status from Unity's AssetStoreCache
                    if (IsImportedPid(e)) return true;
                    var k = e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty;
                    return HasAnyTrackedVersion(k);
                }

                // Build groups with priority buckets
                var updateAvail = list.Where(IsUpdate).ToList();
                var installed = list.Where(IsInstalled).ToList();
                // Remove overlaps: update items shouldn't appear in installed; also exclude both from remaining buckets
                var excludeKeys = new HashSet<string>(updateAvail.Select(e => e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty)
                    .Concat(installed.Select(e => e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty)), StringComparer.Ordinal);

                // Grouping
                bool IsOwned(PackageEntry e) => e.IsOwned == true;
                bool IsFreeUnowned(PackageEntry e) => (e.IsOwned != true) && (e.IsFree == true);
                bool IsPaidUnowned(PackageEntry e) => (e.IsOwned != true) && (e.IsFree == false);
                bool IsUnknown(PackageEntry e) => (e.IsOwned != true) && (!e.IsFree.HasValue);

                var owned = list.Where(e => !excludeKeys.Contains(e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty)).Where(IsOwned).ToList();
                var freeUnowned = list.Where(e => !excludeKeys.Contains(e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty)).Where(IsFreeUnowned).ToList();
                var paidUnowned = list.Where(e => !excludeKeys.Contains(e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty)).Where(IsPaidUnowned).ToList();
                var unknown = list.Where(e => !excludeKeys.Contains(e.AssetStoreId ?? e.Id ?? e.AssetStoreUrl ?? string.Empty)).Where(IsUnknown).ToList();

                // Sorting helper
                string SortName(PackageEntry e)
                {
                    var name = e.DisplayName;
                    if (string.IsNullOrWhiteSpace(name)) name = e.AssetTitle;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        // derive from URL slug
                        try
                        {
                            var u = new Uri(e.AssetStoreUrl ?? string.Empty);
                            var seg = u.Segments?.LastOrDefault()?.Trim('/') ?? string.Empty;
                            name = string.IsNullOrEmpty(seg) ? (e.Id ?? string.Empty) : seg.Replace('-', ' ');
                        }
                        catch { name = e.Id ?? string.Empty; }
                    }
                    return name ?? string.Empty;
                }
                string SortAuthor(PackageEntry e) => e.AssetAuthor ?? string.Empty;

                IEnumerable<PackageEntry> ApplySort(IEnumerable<PackageEntry> src)
                {
                    IOrderedEnumerable<PackageEntry> ordered = _sortField switch
                    {
                        SortField.Author => src.OrderBy(SortAuthor, StringComparer.OrdinalIgnoreCase),
                        _ => src.OrderBy(SortName, StringComparer.OrdinalIgnoreCase)
                    };
                    if (_sortAscending) return ordered;
                    return ordered.Reverse();
                }

                updateAvail = ApplySort(updateAvail).ToList();
                installed = ApplySort(installed).ToList();
                owned = ApplySort(owned).ToList();
                freeUnowned = ApplySort(freeUnowned).ToList();
                paidUnowned = ApplySort(paidUnowned).ToList();
                unknown = ApplySort(unknown).ToList();

                // Draw groups with desired priority:
                // 1) Update Available (highest)
                if (updateAvail.Count > 0)
                {
                    DrawGroupHeader(ref _foldUpdate, $"Update Available ({updateAvail.Count})", "akira.assetStore.fold.update",
                        () => { /* Enable All not applicable here; no-op */ }, () => { /* Disable All no-op */ });
                    if (_foldUpdate) foreach (var e in updateAvail) DrawAssetCard(e);
                }

                // 2) Owned / Unowned buckets (not installed)
                if (owned.Count > 0)
                {
                    DrawGroupHeader(ref _foldOwned, $"Owned (Not Installed) ({owned.Count})", "akira.assetStore.fold.owned", () => ToggleGroupEnable(owned, true), () => ToggleGroupEnable(owned, false));
                    if (_foldOwned) foreach (var e in owned) DrawAssetCard(e);
                }

                if (freeUnowned.Count > 0)
                {
                    DrawGroupHeader(ref _foldFreeUnowned, $"Free (Unowned) ({freeUnowned.Count})", "akira.assetStore.fold.free", () => ToggleGroupEnable(freeUnowned, true), () => ToggleGroupEnable(freeUnowned, false));
                    if (_foldFreeUnowned) foreach (var e in freeUnowned) DrawAssetCard(e);
                }

                if (paidUnowned.Count > 0)
                {
                    DrawGroupHeader(ref _foldPaidUnowned, $"Paid (Unowned) ({paidUnowned.Count})", "akira.assetStore.fold.paid", () => ToggleGroupEnable(paidUnowned, true), () => ToggleGroupEnable(paidUnowned, false));
                    if (_foldPaidUnowned) foreach (var e in paidUnowned) DrawAssetCard(e);
                }

                if (unknown.Count > 0)
                {
                    DrawGroupHeader(ref _foldUnknown, $"Unknown ({unknown.Count})", "akira.assetStore.fold.unknown", () => ToggleGroupEnable(unknown, true), () => ToggleGroupEnable(unknown, false));
                    if (_foldUnknown) foreach (var e in unknown) DrawAssetCard(e);
                }

                // 3) Installed (lowest priority among groups requested)
                if (installed.Count > 0)
                {
                    DrawGroupHeader(ref _foldInstalled, $"Installed ({installed.Count})", "akira.assetStore.fold.installed",
                        () => ToggleGroupEnable(installed, true), () => ToggleGroupEnable(installed, false));
                    if (_foldInstalled) foreach (var e in installed) DrawAssetCard(e);
                }
            }
            finally
            {
                PackageUIUtils.ShowOwnershipChip = prevOwn;
                PackageUIUtils.ShowPriceChip = prevPrice;
            }
        }

        private void ToggleGroupEnable(List<PackageEntry> items, bool enable)
        {
            if (items == null || items.Count == 0) return;
            foreach (var e in items)
            {
                if (e.IsEnabled != enable)
                {
                    e.IsEnabled = enable;
                    ToolsHubSettings.AddOrUpdatePackage(e);
                }
            }
            ToolsHubSettings.Save();
            EditorApplication.delayCall += () =>
            {
                if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                    EditorWindow.GetWindow<ToolsHubManager>().Repaint();
            };
        }

        private void DrawGroupHeader(ref bool foldout, string label, string prefKey, Action enableAll, Action disableAll)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var newFold = EditorGUILayout.Foldout(foldout, label, true);
            if (newFold != foldout)
            {
                foldout = newFold;
                if (!string.IsNullOrEmpty(prefKey)) EditorPrefs.SetBool(prefKey, foldout);
            }
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!foldout))
            {
                if (GUILayout.Button("Enable All", GUILayout.Width(90))) enableAll?.Invoke();
                if (GUILayout.Button("Disable All", GUILayout.Width(90))) disableAll?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        public void DrawContentFooter()
        {
            // When adding new, show a form in footer area
            if (_addingNew)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Add Asset Store Package", EditorStyles.boldLabel);

                _url = EditorGUILayout.TextField("Asset URL:", _url);
                _displayName = EditorGUILayout.TextField("Display Name: (Optional)", _displayName);
                _description = EditorGUILayout.TextField("Description: (Optional)", _description);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                {
                    _addingNew = false;
                    GUI.FocusControl(null);
                }

                var canAdd = !string.IsNullOrWhiteSpace(_url);
                // Stronger asset url validation:
                if (canAdd)
                {
                    try
                    {
                        var u = new Uri(_url.Trim());
                        if (!(u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps) ||
                            string.IsNullOrWhiteSpace(u.Host) ||
                            !u.AbsolutePath.Contains("/packages/"))
                        {
                            canAdd = false;
                        }
                    }
                    catch
                    {
                        canAdd = false;
                    }
                }
                GUI.enabled = canAdd;
                if (GUILayout.Button("Add", GUILayout.Width(120)))
                {
                    var id = BuildAssetId(_url, _displayName);
                    var entry = new PackageEntry
                    {
                        Id = id,
                        DisplayName = _displayName.Trim(),
                        Description = _description.Trim(),
                        IsEssential = false,
                        IsEnabled = true,
                        IsAssetStore = true,
                        IsFree = null, // unknown until fetched
                        AssetStoreUrl = _url.Trim(),
                        Price = null,
                        LastAssetFetchUnixSeconds = 0
                    };
                    ToolsHubSettings.AddOrUpdatePackage(entry);
                    ToolsHubSettings.Save();
                    // Queue a background fetch for price/free
                    AssetStoreInfoService.QueueFetch(entry);
                    _addingNew = false;
                    ToolsHubManager.ShowNotification("Asset added. Fetching details…", "success");
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        public void DrawFooter()
        {
            var left = new List<PageLayout.FooterButton>
            {
                new PageLayout.FooterButton
                {
                    Label = "Close",
                    Style = PageLayout.FooterButtonStyle.Secondary,
                    Enabled = true,
                    OnClick = () => ToolsHubManager.ClosePage(PageOperationResult.Cancelled),
                    MinWidth = 100
                }
            };

            var right = new List<PageLayout.FooterButton>
            {
                new PageLayout.FooterButton
                {
                    Label = _addingNew ? "Adding..." : "Add New",
                    Style = PageLayout.FooterButtonStyle.Primary,
                    Enabled = !_addingNew,
                    OnClick = () => { _addingNew = true; _url = _displayName = _description = string.Empty; }
                }
            };

            // New: batch download button (owned + enabled)
            var canBatch = AssetStoreService.IsAvailable && AssetStoreService.IsSignedIn;
            var countDownloadable = GetDownloadableSelectedCount();
            right.Add(new PageLayout.FooterButton
            {
                Label = countDownloadable > 0 ? $"Download Selected ({countDownloadable})" : "Download Selected",
                Style = PageLayout.FooterButtonStyle.Primary,
                Enabled = canBatch && countDownloadable > 0,
                OnClick = StartDownloadSelected,
                MinWidth = 160
            });

            PageLayout.DrawFooterSplit(left, right);
        }

        public void OnPageResult(PageOperationResult result) { }

        // ===== Version persistence helpers =====
        private static string VerKey(string kind, string key) => $"akira.assetStore.{kind}Version.{key}";
        private static string GetDownloadedVersion(string key) => EditorPrefs.GetString(VerKey("downloaded", key), string.Empty);
        private static string GetInstalledVersion(string key) => EditorPrefs.GetString(VerKey("installed", key), string.Empty);
        private static void SetDownloadedAndInstalledVersion(string key, string version)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(version)) return;
            EditorPrefs.SetString(VerKey("downloaded", key), version);
            EditorPrefs.SetString(VerKey("installed", key), version);
        }
        private static int CompareVersions(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)) return 0;
            if (string.IsNullOrWhiteSpace(a)) return -1;
            if (string.IsNullOrWhiteSpace(b)) return 1;
            var pa = ParseVersion(a);
            var pb = ParseVersion(b);
            var len = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < len; i++)
            {
                int va = i < pa.Length ? pa[i] : 0;
                int vb = i < pb.Length ? pb[i] : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }
        private static int[] ParseVersion(string v)
        {
            // Keep digits and dots only; split and parse
            var clean = new string(v.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
            if (string.IsNullOrWhiteSpace(clean)) return Array.Empty<int>();
            return clean.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var n) ? n : 0)
                        .ToArray();
        }
        private static string MaxVersion(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a)) return b;
            if (string.IsNullOrWhiteSpace(b)) return a;
            return CompareVersions(a, b) >= 0 ? a : b;
        }

        private void DrawAssetCard(PackageEntry entry)
        {
            var signedIn = AssetStoreService.IsSignedIn;

            PackageUIUtils.BeginCard();
            // Compute a nicer title early from URL slug if AssetTitle is still empty
            string ComputeNiceTitleFromUrl(string url)
            {
                if (string.IsNullOrWhiteSpace(url)) return null;
                try
                {
                    var u = new Uri(url);
                    var seg = u.Segments?.LastOrDefault()?.Trim('/') ?? string.Empty;
                    if (string.IsNullOrEmpty(seg)) return null;
                    // Remove trailing -digits (asset id)
                    var dashIdx = seg.LastIndexOf('-');
                    if (dashIdx > 0)
                    {
                        var tail = seg.Substring(dashIdx + 1);
                        if (int.TryParse(tail, out _)) seg = seg.Substring(0, dashIdx);
                    }
                    // Hyphens to spaces, URL-decode, title case
                    seg = seg.Replace('-', ' ');
                    seg = Uri.UnescapeDataString(seg);
                    if (string.IsNullOrWhiteSpace(seg)) return null;
                    var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                    seg = textInfo.ToTitleCase(seg.Trim());
                    return seg;
                }
                catch (Exception) { return null; }
            }

            // Prefer parsed title; if missing, derive from URL; fallback to DisplayName or Id
            var linkTitle = entry.AssetTitle;
            if (string.IsNullOrWhiteSpace(linkTitle) && !string.IsNullOrWhiteSpace(entry.AssetStoreUrl))
            {
                var guess = ComputeNiceTitleFromUrl(entry.AssetStoreUrl);
                if (!string.IsNullOrWhiteSpace(guess))
                {
                    linkTitle = guess;
                    // Persist heuristic so we don't flash the raw URL on first paint
                    entry.AssetTitle = guess;
                    ToolsHubSettings.AddOrUpdatePackage(entry);
                    ToolsHubSettings.Save();
                }
            }
            if (string.IsNullOrWhiteSpace(linkTitle))
                linkTitle = !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : (!string.IsNullOrEmpty(entry.AssetStoreUrl) ? entry.AssetStoreUrl : entry.Id);

            // Per-card collapse state
            var key = entry.AssetStoreId ?? entry.Id ?? entry.AssetStoreUrl ?? string.Empty;
            bool collapsed = !string.IsNullOrEmpty(key) && _collapsedKeys.Contains(key);

            // Resolve product id early for actions
            long pid = 0;
            var hasPid = !string.IsNullOrEmpty(entry.AssetStoreId) && long.TryParse(entry.AssetStoreId, out pid);

            // Fetch available version if known
            string availableVersion = null;
            if (hasPid && _overviewByPid.TryGetValue(pid, out var ov0)) availableVersion = ov0?.Version;
            var entryKey = entry.AssetStoreId ?? entry.Id ?? entry.AssetStoreUrl ?? string.Empty;
            bool isUpdate = false;
            if (!string.IsNullOrWhiteSpace(availableVersion))
            {
                var baseVer = MaxVersion(GetInstalledVersion(entryKey), GetDownloadedVersion(entryKey));
                isUpdate = !string.IsNullOrWhiteSpace(baseVer) && CompareVersions(availableVersion, baseVer) > 0;
            }
            // New: consider entry “in project” if we have any tracked version
            bool isInProject = !string.IsNullOrEmpty(GetInstalledVersion(entryKey)) || !string.IsNullOrEmpty(GetDownloadedVersion(entryKey));
            // New: cached .unitypackage path if available
            string cachedPkgPath = (hasPid ? AssetStoreBridge.TryGetDownloadedPackagePath(pid) : null);

            // Build collapsed action buttons
            var collapsedActions = new List<ActionButton>();
            if (collapsed)
            {
                if (hasPid)
                {
                    if (isUpdate && signedIn && entry.IsOwned == true)
                    {
                        collapsedActions.Add(new ActionButton
                        {
                            Label = "Update",
                            Enabled = true,
                            Bg = InstallButtonColor,
                            OnClick = () =>
                            {
                                AssetStoreService.DownloadAndImport(pid);
                                if (!string.IsNullOrWhiteSpace(availableVersion))
                                    SetDownloadedAndInstalledVersion(entryKey, availableVersion);
                            }
                        });
                    }
                    else if (isInProject)
                    {
                        // Re-import when already in project; prefer cached path/installers
                        collapsedActions.Add(new ActionButton
                        {
                            Label = "Re-import",
                            Enabled = true,
                            Bg = InstallButtonColor,
                            OnClick = () =>
                            {
                                try
                                {
                                    // Try internal installer first
                                    if (!AssetStoreBridge.InstallDownloadedPackage(pid, true))
                                    {
                                        if (!string.IsNullOrEmpty(cachedPkgPath) && System.IO.File.Exists(cachedPkgPath))
                                            AssetDatabase.ImportPackage(cachedPkgPath, true);
                                        else if (signedIn && entry.IsOwned == true)
                                            AssetStoreService.DownloadAndImport(pid); // fallback: fetch again then import
                                        else
                                            EditorUtility.DisplayDialog("Re-import", "No cached package found. Sign in to download again.", "OK");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"Re-import failed: {ex.Message}");
                                }
                            }
                        });
                    }
                    else if (!string.IsNullOrEmpty(cachedPkgPath))
                    {
                        // Allow re-import directly from cache even if not marked as installed
                        collapsedActions.Add(new ActionButton
                        {
                            Label = "Re-import",
                            Enabled = true,
                            Bg = InstallButtonColor,
                            OnClick = () =>
                            {
                                try
                                {
                                    if (!AssetStoreBridge.InstallDownloadedPackage(pid, true))
                                    {
                                        if (System.IO.File.Exists(cachedPkgPath))
                                            AssetDatabase.ImportPackage(cachedPkgPath, true);
                                        else if (signedIn && entry.IsOwned == true)
                                            AssetStoreService.DownloadAndImport(pid);
                                        else
                                            EditorUtility.DisplayDialog("Re-import", "No cached package found. Sign in to download again.", "OK");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"Re-import failed: {ex.Message}");
                                }
                            }
                        });
                    }
                    else if (signedIn && entry.IsOwned == true)
                    {
                        collapsedActions.Add(new ActionButton
                        {
                            Label = "Download",
                            Enabled = true,
                            Bg = InstallButtonColor,
                            OnClick = () =>
                            {
                                AssetStoreService.DownloadAndImport(pid);
                                if (!string.IsNullOrWhiteSpace(availableVersion))
                                    SetDownloadedAndInstalledVersion(entryKey, availableVersion);
                            }
                        });
                    }
                    else if (!string.IsNullOrEmpty(entry.AssetStoreUrl))
                    {
                        if (entry.IsFree == true)
                        {
                            collapsedActions.Add(new ActionButton
                            {
                                Label = "View Page",
                                Enabled = true,
                                Bg = SecondaryButtonColor,
                                OnClick = () => Application.OpenURL(entry.AssetStoreUrl)
                            });
                        }
                        else
                        {
                            var label = !string.IsNullOrWhiteSpace(entry.Price) ? $"Buy {entry.Price}" : "Buy";
                            collapsedActions.Add(new ActionButton
                            {
                                Label = label,
                                Enabled = true,
                                Bg = SecondaryButtonColor,
                                OnClick = () => Application.OpenURL(entry.AssetStoreUrl)
                            });
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(entry.AssetStoreUrl))
                {
                    if (entry.IsFree == true)
                    {
                        collapsedActions.Add(new ActionButton
                        {
                            Label = "View Page",
                            Enabled = true,
                            Bg = SecondaryButtonColor,
                            OnClick = () => Application.OpenURL(entry.AssetStoreUrl)
                        });
                    }
                    else
                    {
                        var label = !string.IsNullOrWhiteSpace(entry.Price) ? $"Buy {entry.Price}" : "Buy";
                        collapsedActions.Add(new ActionButton
                        {
                            Label = label,
                            Enabled = true,
                            Bg = SecondaryButtonColor,
                            OnClick = () => Application.OpenURL(entry.AssetStoreUrl)
                        });
                    }
                }
            }

            // Title row: [arrow] [toggle] Hyperlinked Title .......... [action buttons when collapsed]
            EditorGUILayout.BeginHorizontal();
            
            // Left arrow button
            var arrow = collapsed ? "▶" : "▼";
            if (GUILayout.Button(arrow, EditorStyles.miniButton, GUILayout.Width(24)))
            {
                if (!string.IsNullOrEmpty(key))
                {
                    if (collapsed) _collapsedKeys.Remove(key); else _collapsedKeys.Add(key);
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                            EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                    };
                }
            }

            // Enable toggle
            var newEnabled = EditorGUILayout.Toggle(entry.IsEnabled, GUILayout.Width(18));

            // Word-wrapped clickable label
            var link = !string.IsNullOrEmpty(entry.AssetStoreUrl);
            var baseStyle = EditorStyles.boldLabel;
            var titleStyle = new GUIStyle(baseStyle)
            {
                wordWrap = true,
                richText = false,
                alignment = TextAnchor.UpperLeft,
                fontSize = Mathf.Max(10, baseStyle.fontSize + 2),
                normal = { textColor = link ? new Color(0.5f, 0.8f, 1f) : baseStyle.normal.textColor }
            };

            var titleContent = new GUIContent(linkTitle ?? string.Empty, entry.AssetStoreUrl);
            EditorGUILayout.LabelField(titleContent, titleStyle, GUILayout.ExpandWidth(true));
            var titleRect = GUILayoutUtility.GetLastRect();
            if (link)
            {
                EditorGUIUtility.AddCursorRect(titleRect, MouseCursor.Link);
                var e = Event.current;
                if (e.type == EventType.MouseUp && titleRect.Contains(e.mousePosition))
                {
                    Application.OpenURL(entry.AssetStoreUrl);
                }
            }

            // Action buttons on the right (when collapsed)
            if (collapsed && collapsedActions.Count > 0)
            {
                GUILayout.FlexibleSpace();
                foreach (var btn in collapsedActions)
                {
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = btn.Bg;
                    EditorGUI.BeginDisabledGroup(!btn.Enabled);
                    if (GUILayout.Button(btn.Label)) btn.OnClick?.Invoke();
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = prev;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (newEnabled != entry.IsEnabled)
            {
                entry.IsEnabled = newEnabled;
                ToolsHubSettings.AddOrUpdatePackage(entry);
                ToolsHubSettings.Save();
            }

            if (!collapsed)
            {
                // Expanded: draw full header and all actions/overview
                string headerDesc = null;
                if (hasPid)
                {
                    _overviewByPid.TryGetValue(pid, out var ovTmp);
                    headerDesc = ovTmp?.Description;
                }
                var expandKey = entry.AssetStoreId ?? entry.Id ?? entry.AssetStoreUrl;
                PackageUIUtils.DrawAssetHeader(entry, headerDesc, expandKey);

                // Actions
                var actions = new List<ActionButton>();

                // New: show an "In Project" indicator and a Re-import button when installed
                if (isInProject)
                {
                    actions.Add(new ActionButton
                    {
                        Label = "In Project",
                        Enabled = false,
                        Bg = SecondaryButtonColor,
                        OnClick = null
                    });

                    actions.Add(new ActionButton
                    {
                        Label = "Re-import",
                        Enabled = true,
                        Bg = InstallButtonColor,
                        OnClick = () =>
                        {
                            try
                            {
                                if (hasPid && AssetStoreBridge.InstallDownloadedPackage(pid, true)) return;
                                if (!string.IsNullOrEmpty(cachedPkgPath) && System.IO.File.Exists(cachedPkgPath))
                                {
                                    AssetDatabase.ImportPackage(cachedPkgPath, true);
                                    return;
                                }
                                // Fallback: download again if allowed
                                if (hasPid && entry.IsOwned == true && signedIn)
                                {
                                    AssetStoreService.DownloadAndImport(pid);
                                }
                                else
                                {
                                    EditorUtility.DisplayDialog("Re-import", "No cached package found. Sign in to download again.", "OK");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Re-import failed: {ex.Message}");
                            }
                        }
                    });
                }
                else if (!string.IsNullOrEmpty(cachedPkgPath))
                {
                    // If we have a cached package locally, offer Re-import even if we can't confirm installed state
                    actions.Add(new ActionButton
                    {
                        Label = "Re-import",
                        Enabled = true,
                        Bg = InstallButtonColor,
                        OnClick = () =>
                        {
                            try
                            {
                                if (hasPid && AssetStoreBridge.InstallDownloadedPackage(pid, true)) return;
                                if (System.IO.File.Exists(cachedPkgPath))
                                {
                                    AssetDatabase.ImportPackage(cachedPkgPath, true);
                                    return;
                                }
                                if (hasPid && entry.IsOwned == true && signedIn)
                                {
                                    AssetStoreService.DownloadAndImport(pid);
                                }
                                else
                                {
                                    EditorUtility.DisplayDialog("Re-import", "No cached package found. Sign in to download again.", "OK");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Re-import failed: {ex.Message}");
                            }
                        }
                    });
                }

                if (!signedIn)
                {
                    actions.Add(new ActionButton
                    {
                        Label = "Sign In",
                        Enabled = true,
                        Bg = SecondaryButtonColor,
                        OnClick = () => Application.OpenURL("https://id.unity.com")
                    });
                }

                if (!string.IsNullOrEmpty(entry.UpmPackageId))
                {
                    actions.Add(new ActionButton
                    {
                        Label = "Install Package",
                        Enabled = true,
                        Bg = InstallButtonColor,
                        OnClick = () => UpmInstaller.Install(entry.UpmPackageId)
                    });
                }

                if (hasPid)
                {
                    // Ensure overview requested
                    TryRequestOverview(pid);

                    if (signedIn && entry.IsOwned == true)
                    {
                        var label = isUpdate ? "Update" : (isInProject ? "Download (Reinstall)" : "Download");
                        actions.Add(new ActionButton
                        {
                            Label = label,
                            Enabled = true,
                            Bg = InstallButtonColor,
                            OnClick = () =>
                            {
                                AssetStoreService.DownloadAndImport(pid);
                                if (!string.IsNullOrWhiteSpace(availableVersion))
                                    SetDownloadedAndInstalledVersion(entryKey, availableVersion);
                            }
                        });
                    }
                    else
                    {
                        if (entry.IsFree == true)
                        {
                            actions.Add(new ActionButton
                            {
                                Label = "View Page",
                                Enabled = !string.IsNullOrEmpty(entry.AssetStoreUrl),
                                Bg = SecondaryButtonColor,
                                OnClick = () => { if (!string.IsNullOrEmpty(entry.AssetStoreUrl)) Application.OpenURL(entry.AssetStoreUrl); }
                            });
                        }
                        else
                        {
                            var buyLbl = !string.IsNullOrWhiteSpace(entry.Price) ? $"Buy {entry.Price}" : "Buy";
                            actions.Add(new ActionButton
                            {
                                Label = buyLbl,
                                Enabled = !string.IsNullOrEmpty(entry.AssetStoreUrl),
                                Bg = SecondaryButtonColor,
                                OnClick = () => { if (!string.IsNullOrEmpty(entry.AssetStoreUrl)) Application.OpenURL(entry.AssetStoreUrl); }
                            });
                        }
                    }
                }

                // View Page safety for non-UPM assets
                if (string.IsNullOrEmpty(entry.UpmPackageId) && actions.All(a => a.Label != "View Page"))
                {
                    actions.Add(new ActionButton
                    {
                        Label = "View Page",
                        Enabled = !string.IsNullOrEmpty(entry.AssetStoreUrl),
                        Bg = SecondaryButtonColor,
                        OnClick = () => { if (!string.IsNullOrEmpty(entry.AssetStoreUrl)) Application.OpenURL(entry.AssetStoreUrl); }
                    });
                }

                PackageUIUtils.DrawActionButtonsFlow(actions);

                // Optional: debug cache info button (enable via EditorPrefs key: akira.assetStore.showDebug)
                if (hasPid && EditorPrefs.GetBool("akira.assetStore.showDebug", false))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Debug Cache Info", GUILayout.Width(140)))
                    {
                        try
                        {
                            var map = AssetStoreBridge.TryGetLocalInfoMap(pid);
                            if (map == null || map.Count == 0)
                            {
                                Debug.Log($"[AssetStoreDebug] No local cache info for productId={pid}");
                            }
                            else
                            {
                                Debug.Log($"[AssetStoreDebug] Local cache info for productId={pid}:");
                                foreach (var kv in map)
                                {
                                    Debug.Log($"[AssetStoreDebug]   {kv.Key} = {kv.Value}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AssetStoreDebug] Failed to dump cache info: {ex.Message}");
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // Draw Overview row (ratings/version/purchased/size/labels)
                if (hasPid)
                {
                    DrawOverviewRow(pid);
                }
            }

            PackageUIUtils.EndCard();
        }

        // ===== Overview (ratings, version, purchase date, size, labels) =====
        private void TryRequestOverview(long pid)
        {
            if (_overviewByPid.ContainsKey(pid) || _overviewRequested.Contains(pid)) return;
            _overviewRequested.Add(pid);
            // Requires service and sign-in to access product details
            if (!AssetStoreService.IsAvailable || !AssetStoreService.IsSignedIn) return;

            AssetStoreService.GetProductDetail(pid, map =>
            {
                var ov = new ProductOverview();
                try
                {
                    string GetStr(params string[] keys)
                    {
                        foreach (var k in keys)
                        {
                            if (map != null && map.TryGetValue(k, out var v) && v != null)
                            {
                                var s = v.ToString();
                                if (!string.IsNullOrWhiteSpace(s)) return s;
                            }
                        }
                        return null;
                    }
                    float? GetFloat(params string[] keys)
                    {
                        foreach (var k in keys)
                        {
                            if (map != null && map.TryGetValue(k, out var v) && v != null)
                            {
                                if (v is float f) return f;
                                if (v is double d) return (float)d;
                                if (float.TryParse(v.ToString(), out var pf)) return pf;
                            }
                        }
                        return null;
                    }
                    int? GetInt(params string[] keys)
                    {
                        foreach (var k in keys)
                        {
                            if (map != null && map.TryGetValue(k, out var v) && v != null)
                            {
                                if (v is int i) return i;
                                if (v is long l) return (int)l;
                                if (int.TryParse(v.ToString(), out var pi)) return pi;
                            }
                        }
                        return null;
                    }

                    // Description
                    ov.Description = GetStr("description", "productDescription", "desc");

                    // Version
                    ov.Version = GetStr("version", "packageVersion", "currentVersion");

                    // Ratings
                    ov.RatingAverage = GetFloat("rating", "ratingAverage", "averageRating");
                    ov.RatingCount = GetInt("ratingCount", "votes", "reviewCount");

                    // Purchased date (if provided)
                    var purchased = GetStr("purchasedTime", "purchasedDate");
                    if (DateTime.TryParse(purchased, out var dt)) ov.PurchasedDate = dt;

                    // Size and asset count
                    var sizeStr = GetStr("downloadSize", "size");
                    if (long.TryParse(sizeStr, out var sizeLong)) ov.DownloadSizeBytes = sizeLong;
                    else if (int.TryParse(sizeStr, out var sizeInt)) ov.DownloadSizeBytes = sizeInt;
                    ov.AssetCount = GetInt("assetCount", "files");

                    // Labels (tags)
                    if (map != null && map.TryGetValue("labels", out var labelsObj) && labelsObj is IEnumerable<object> en)
                        ov.Labels = en.Select(x => x?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                }
                catch { }
                _overviewByPid[pid] = ov;

                // Repaint to reflect new info
                EditorApplication.delayCall += () =>
                {
                    if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                        EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                };
            }, err => { /* ignore */ });
        }

        private void DrawOverviewRow(long pid)
        {
            _overviewByPid.TryGetValue(pid, out var ov);
            if (ov == null) return;

            // Build a compact row: Rating | Version | Purchased | Size | Labels
            EditorGUILayout.BeginHorizontal();

            // Rating
            if (ov.RatingAverage.HasValue)
            {
                var avg = Mathf.Clamp01(ov.RatingAverage.Value / 5f) * 5f; // keep 0-5 range
                var stars = BuildStars(avg);
                var label = ov.RatingCount.HasValue ? $"{stars} ({ov.RatingCount.Value})" : stars;
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.MaxWidth(160));
            }

            // Version
            if (!string.IsNullOrWhiteSpace(ov.Version))
            {
                EditorGUILayout.LabelField($"Version {ov.Version}", EditorStyles.miniLabel, GUILayout.MaxWidth(130));
            }

            // Purchased date
            if (ov.PurchasedDate.HasValue)
            {
                EditorGUILayout.LabelField($"Purchased {ov.PurchasedDate.Value:MMMM dd, yyyy}", EditorStyles.miniLabel, GUILayout.MaxWidth(190));
            }

            // Size and files
            if (ov.DownloadSizeBytes.HasValue)
            {
                var sizeText = EditorUtility.FormatBytes((long)ov.DownloadSizeBytes.Value);
                var filesText = ov.AssetCount.HasValue ? $" (Files: {ov.AssetCount.Value})" : string.Empty;
                EditorGUILayout.LabelField($"Size: {sizeText}{filesText}", EditorStyles.miniLabel, GUILayout.MaxWidth(220));
            }

            // Labels
            if (ov.Labels != null && ov.Labels.Count > 0)
            {
                var text = string.Join(", ", ov.Labels.Take(3));
                EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private string BuildStars(float rating0to5)
        {
            // Build simple star string; half stars approximated by rounding to nearest half
            var roundedHalf = Mathf.Round(rating0to5 * 2f) / 2f;
            int full = Mathf.FloorToInt(roundedHalf);
            bool half = (roundedHalf - full) >= 0.5f;
            int empty = 5 - full - (half ? 1 : 0);
            return new string('★', full) + (half ? "☆" : string.Empty) + new string('☆', empty);
        }

        private void DrawOverviewDescription(long pid, PackageEntry entry)
        {
            // Deprecated: description is now rendered inline in header
        }

        private static string BuildAssetId(string url, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                // attempt to create stable id
                var u = url.Trim();
                if (u.EndsWith("/")) u = u.Substring(0, u.Length - 1);
                var last = u.Split('/').LastOrDefault();
                if (!string.IsNullOrEmpty(last)) return $"assetstore:{last}";
                return $"assetstore:{u.GetHashCode()}";
            }
            return $"assetstore:{displayName?.Trim()}";
        }

        // ===== New helpers: batch download selection =====
        private int GetDownloadableSelectedCount()
        {
            try
            {
                var list = ToolsHubSettings.GetAllPackages();
                if (list == null || list.Count == 0) return 0;
                return list.Count(e =>
                    e.IsEnabled &&
                    (e.IsAssetStore == true || !string.IsNullOrEmpty(e.AssetStoreUrl)) &&
                    !string.IsNullOrEmpty(e.AssetStoreId) && long.TryParse(e.AssetStoreId, out var _) &&
                    e.IsOwned == true);
            }
            catch { return 0; }
        }

        private void StartDownloadSelected()
        {
            var list = ToolsHubSettings.GetAllPackages() ?? new List<PackageEntry>();
            var targets = list
                .Where(e => e.IsEnabled && (e.IsAssetStore == true || !string.IsNullOrEmpty(e.AssetStoreUrl)))
                .Where(e => !string.IsNullOrEmpty(e.AssetStoreId) && long.TryParse(e.AssetStoreId, out var _))
                .Where(e => e.IsOwned == true)
                .Select(e => long.Parse(e.AssetStoreId))
                .Distinct()
                .ToList();

            if (targets.Count == 0)
            {
                ToolsHubManager.ShowNotification("No owned, selected Asset Store items to download.");
                return;
            }

            ToolsHubManager.ShowNotification($"Starting downloads for {targets.Count} asset(s)…");

            // Fire off downloads (Unity's manager will handle queuing); space calls across a few frames
            int idx = 0;
            void Kick()
            {
                if (idx >= targets.Count) return;
                var pid = targets[idx++];
                try { AssetStoreService.DownloadAndImport(pid); } catch (Exception) { }
                if (idx < targets.Count)
                    EditorApplication.delayCall += Kick; // schedule next on next editor update
            }
            Kick();
        }
    }

    public static class AssetStorePackagesPage
    {
        private static AssetStorePackagesPageImpl _impl;

        [MenuButtonItem("Setup/Packages", "Asset Store Packages", "Track and install Asset Store items", true)]
        public static void ShowPage()
        {
            _impl = new AssetStorePackagesPageImpl();
            _impl.ShowInToolsHub();
        }

        public static void ShowPage_Page()
        {
            if (_impl == null) _impl = new AssetStorePackagesPageImpl();
            // Rebind header Refresh to this page's handler every time we draw the page
            _impl.BindRefreshHook();
            _impl.DrawPage();
        }
    }
}
#endif
