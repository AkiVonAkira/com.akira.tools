#if UNITY_EDITOR
using System.Collections.Generic;
using akira.AssetStoreNative;
using akira.ToolsHub;
using UnityEditor;
using UnityEngine;

namespace akira.UI
{
    // ToolsHub page that replaces the standalone AssetStoreWindow
    public class AssetStoreNativePageImpl : IToolsHubPage
    {
        private Vector2 _scroll;
        private string _productId = "";
        private string _status = "Idle";
        private List<OwnedAssetInfo> _owned = new List<OwnedAssetInfo>();
        private string _details = "";
        private bool _debugDownloads;
        private GUIStyle _wrap;

        public string Title => "Asset Store (Native)";
        public string Description => "Debug and use Unity Asset Store via UPM internals (ownership, details, download/import).";

        public void BindRefreshHook()
        {
            // no-op for now; page does not auto-refresh
        }

        public void DrawContentHeader()
        {
            _wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true };

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Service Available", AssetStoreService.IsAvailable);
                EditorGUILayout.Toggle("Signed In", AssetStoreService.IsSignedIn);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Diagnose", GUILayout.Width(90)))
                _status = AssetStoreBridge.Diagnose();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Debug logging toggle
            EditorGUILayout.BeginHorizontal();
            var newDbg = EditorGUILayout.ToggleLeft("Log Download Events (debug)", _debugDownloads, GUILayout.Width(220));
            if (newDbg != _debugDownloads)
            {
                _debugDownloads = newDbg;
                if (!AssetStoreBridge.EnableDownloadDebugLogging(_debugDownloads))
                    _status = "Failed to toggle debug logging (service unavailable)";
                else
                    _status = _debugDownloads ? "Download debug logging enabled" : "Download debug logging disabled";
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        public void DrawScrollContent()
        {
            EditorGUILayout.BeginHorizontal();
            _productId = EditorGUILayout.TextField("Product ID", _productId);
            if (GUILayout.Button("Details", GUILayout.Width(80))) FetchDetails();
            if (GUILayout.Button("Download", GUILayout.Width(90))) Download();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_details))
            {
                EditorGUILayout.LabelField("Details:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_details, _wrap);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("List Owned Assets")) ListOwned();

            EditorGUILayout.LabelField("Owned Product IDs:", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_owned.Count == 0) EditorGUILayout.LabelField("(none)");
            else
            {
                for (int i = 0; i < _owned.Count; i++)
                {
                    var info = _owned[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(info.DisplayName) ? info.ProductId.ToString() : $"{info.ProductId} - {info.DisplayName}", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Download", GUILayout.Width(90))) { _productId = info.ProductId.ToString(); Download(); }
                    if (GUILayout.Button("Details", GUILayout.Width(80))) { _productId = info.ProductId.ToString(); FetchDetails(); }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        public void DrawContentFooter()
        {
            // none
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

            // Pass null explicitly for right side
            PageLayout.DrawFooterSplit(left, null);
        }

        public void OnPageResult(PageOperationResult result) { }

        private void ListOwned()
        {
            _status = "Fetching owned assets...";
            ToolsHubManager.ShowNotification(_status);
            AssetStoreService.GetAllOwnedPurchases(items =>
            {
                _owned = items ?? new List<OwnedAssetInfo>();
                _status = $"Owned count: {_owned.Count}";
                RequestRepaintToolsHub();
            }, err =>
            {
                _status = string.IsNullOrEmpty(err) ? "Failed to get owned assets." : err;
                RequestRepaintToolsHub();
            });
        }

        private void FetchDetails()
        {
            if (!long.TryParse(_productId, out var pid))
            {
                _status = "Enter a numeric Product ID.";
                return;
            }
            _status = $"Fetching details for {pid}...";
            _details = string.Empty;
            RequestRepaintToolsHub();
            AssetStoreService.GetProductDetail(pid, map =>
            {
                string GetStr(string k) => map != null && map.TryGetValue(k, out var v) ? v?.ToString() : null;
                var disp = GetStr("displayName") ?? GetStr("name") ?? GetStr("packageName");
                var pub = GetStr("publisherName") ?? GetStr("publisher") ?? GetStr("publisherLabel");

                AssetStoreService.IsOwned(pid, owned =>
                {
                    _details = $"Name: {disp}\nPublisher: {pub}\nOwned: {(owned ? "Yes" : "No")}";
                    _status = "OK";
                    RequestRepaintToolsHub();
                }, err =>
                {
                    _details = $"Name: {disp}\nPublisher: {pub}\nOwned: (unknown)";
                    _status = string.IsNullOrEmpty(err) ? "OK" : err;
                    RequestRepaintToolsHub();
                });
            }, err =>
            {
                _status = string.IsNullOrEmpty(err) ? "Failed to fetch product details." : err;
                RequestRepaintToolsHub();
            });
        }

        private void Download()
        {
            if (!long.TryParse(_productId, out var pid))
            {
                _status = "Enter a numeric Product ID.";
                return;
            }
            _status = $"Requesting download for {pid}...";
            RequestRepaintToolsHub();
            AssetStoreService.DownloadAndImport(pid);
        }

        private static void RequestRepaintToolsHub()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                    EditorWindow.GetWindow<ToolsHubManager>().Repaint();
            };
        }
    }

    public static class AssetStoreNativePage
    {
        private static AssetStoreNativePageImpl _impl;

        //[MenuButtonItem("Setup/Packages", "Native Asset Store Testing", "tools to test Native Asset Store functionality", false)]
        public static void ShowPage()
        {
            // Redirect to the consolidated page
            ToolsHubManager.ShowNotification("Native Asset Store page has moved. Redirecting to Asset Store Packages…");
            AssetStorePackagesPage.ShowPage();
        }

        // Called by ToolsHub to render the page content
        public static void ShowPage_Page()
        {
            // Redirect drawing to the new page as well
            AssetStorePackagesPage.ShowPage_Page();
        }
    }
}
#endif
