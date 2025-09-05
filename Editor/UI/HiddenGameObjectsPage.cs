#if UNITY_EDITOR
using System.Collections.Generic;
using akira.ToolsHub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace akira.UI
{
    public class HiddenGameObjectsPageImpl : IToolsHubPage
    {
        // Static state for ToolsHub page
        private readonly List<GameObject> _hiddenObjects = new();
        private int _objectToRemoveIndex = -1;
        private bool _pendingObjectRemoval;

        public HiddenGameObjectsPageImpl()
        {
            // Initialize by gathering hidden objects
            GatherHiddenObjects();
            // Hook the global toolbar refresh to re-gather objects
            ToolsHubManager.SetPageRefreshHandler(GatherHiddenObjects);
        }

        // Ensure refresh is rebound whenever the page becomes active again
        public void BindRefreshHook()
        {
            ToolsHubManager.SetPageRefreshHandler(GatherHiddenObjects);
        }

        public string Title => "Hidden GameObject Tools";
        public string Description => "View and manage objects hidden from the Hierarchy";

        public void DrawContentHeader()
        {
            // Action buttons
            EditorGUILayout.BeginHorizontal();

            // Removed page-local Refresh button; use header Refresh instead
            if (GUILayout.Button("Create Hidden Object", GUILayout.Height(28)))
            {
                var go = new GameObject("HiddenTestObject");
                go.hideFlags = HideFlags.HideInHierarchy;
                GatherHiddenObjects();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.LabelField($"Hidden Objects ({_hiddenObjects.Count})", EditorStyles.boldLabel);

            // Reset pending removal flag
            _pendingObjectRemoval = false;
        }

        public void DrawScrollContent()
        {
            if (_hiddenObjects.Count == 0)
            {
                // Don't create a scroll view at all when empty
                EditorGUILayout.HelpBox("No hidden objects found in the current scene.", MessageType.Info);

                return;
            }

            // Only draw the scroll contents when we have items
            for (var i = 0; i < _hiddenObjects.Count; i++)
            {
                var hiddenObject = _hiddenObjects[i];
                EditorGUILayout.BeginHorizontal();
                var gone = hiddenObject == null;
                GUILayout.Label(gone ? "null" : hiddenObject.name);
                GUILayout.FlexibleSpace();

                if (gone)
                {
                    GUILayout.Box("Select", GUILayout.Width(80));
                    GUILayout.Box("Reveal", GUILayout.Width(80));
                    GUILayout.Box("Delete", GUILayout.Width(80));
                }
                else
                {
                    if (GUILayout.Button("Select", GUILayout.Width(80)))
                        Selection.activeGameObject = hiddenObject;

                    if (GUILayout.Button(IsHidden(hiddenObject) ? "Reveal" : "Hide", GUILayout.Width(80)))
                    {
                        hiddenObject.hideFlags ^= HideFlags.HideInHierarchy;
                        EditorSceneManager.MarkSceneDirty(hiddenObject.scene);
                    }

                    // Queue object for deletion rather than deleting immediately
                    if (GUILayout.Button("Delete", GUILayout.Width(80)))
                    {
                        _pendingObjectRemoval = true;
                        _objectToRemoveIndex = i;
                    }
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            // Handle any pending object removals after drawing is complete
            if (_pendingObjectRemoval && _objectToRemoveIndex >= 0 && _objectToRemoveIndex < _hiddenObjects.Count)
            {
                var hiddenObject = _hiddenObjects[_objectToRemoveIndex];

                if (hiddenObject != null)
                {
                    var scene = hiddenObject.scene;
                    Object.DestroyImmediate(hiddenObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                GatherHiddenObjects();
                _pendingObjectRemoval = false;
                _objectToRemoveIndex = -1;
            }
        }

        public void DrawContentFooter()
        {
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

            // No right-side actions for this page
            var right = new List<PageLayout.FooterButton>();

            PageLayout.DrawFooterSplit(left, right);
        }

        public void OnPageResult(PageOperationResult result)
        {
            // For future notifications if needed
        }

        private void GatherHiddenObjects()
        {
            _hiddenObjects.Clear();
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var go in allObjects)
                if ((go.hideFlags & HideFlags.HideInHierarchy) != 0)
                    _hiddenObjects.Add(go);

            // Ensure UI updates after refresh
            EditorApplication.delayCall += () =>
            {
                if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                    EditorWindow.GetWindow<ToolsHubManager>().Repaint();
            };
        }

        private bool IsHidden(GameObject go)
        {
            return (go.hideFlags & HideFlags.HideInHierarchy) != 0;
        }
    }

    public static class HiddenGameObjectsPage
    {
        private static HiddenGameObjectsPageImpl _currentPageImpl;

        [MenuButtonItem("Others", "Hidden GameObjects Tool", "Show hidden GameObjects", true)]
        public static void ShowHiddenGameObjectsPage()
        {
            // Create a new implementation each time to ensure we refresh the objects list
            _currentPageImpl = new HiddenGameObjectsPageImpl();
            _currentPageImpl.ShowInToolsHub();
        }

        // This is needed for backward compatibility with the menu system
        public static void Draw()
        {
            if (_currentPageImpl != null)
            {
                _currentPageImpl.BindRefreshHook();
                _currentPageImpl.DrawPage();
            }
            else
            {
                _currentPageImpl = new HiddenGameObjectsPageImpl();
                _currentPageImpl.DrawPage();
            }
        }

        // This method is needed for the ToolsHub menu system to find the Draw method
        public static void ShowHiddenGameObjectsPage_Page()
        {
            Draw();
        }
    }
}
#endif