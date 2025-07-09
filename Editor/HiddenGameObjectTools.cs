#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace akira
{
    public class HiddenGameObjectTools : EditorWindow
    {
        // Static state for ToolsHub page
        private static readonly List<GameObject> HiddenObjects = new();
        private static Vector2 _scroll;

        private void OnGUI()
        {
            ShowInToolsHub_Page();
        }

        [MenuButtonItem("Others", "Hidden GameObject Tools", "Show hidden GameObjects", true)]
        public static void ShowInToolsHub()
        {
            /* No-op, page logic handled by DrawPage */
        }

        // Draws the page inside ToolsHub
        public static void ShowInToolsHub_Page()
        {
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(35))) GatherHiddenObjects();

            if (GUILayout.Button("Test", GUILayout.Height(35), GUILayout.Width(80)))
            {
                var go = new GameObject("HiddenTestObject");
                go.hideFlags = HideFlags.HideInHierarchy;
                GatherHiddenObjects();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            EditorGUILayout.LabelField(
                $"Hidden Objects ({HiddenObjects.Count})",
                EditorStyles.boldLabel
            );

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));

            for (var i = 0; i < HiddenObjects.Count; i++)
            {
                var hiddenObject = HiddenObjects[i];
                GUILayout.BeginHorizontal();
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

                    if (GUILayout.Button("Delete", GUILayout.Width(80)))
                    {
                        var scene = hiddenObject.scene;
                        DestroyImmediate(hiddenObject);
                        EditorSceneManager.MarkSceneDirty(scene);
                        GatherHiddenObjects();

                        break;
                    }
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // Used by both window and ToolsHub page
        private static void GatherHiddenObjects()
        {
            HiddenObjects.Clear();
            var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var go in allObjects)
                if ((go.hideFlags & HideFlags.HideInHierarchy) != 0)
                    HiddenObjects.Add(go);
        }

        private static bool IsHidden(GameObject go)
        {
            return (go.hideFlags & HideFlags.HideInHierarchy) != 0;
        }

        // Legacy window entry (optional, can be removed if only using ToolsHub)
        [MenuItem("Tools/Hidden GameObject Tools")]
        public static void ShowWindow()
        {
            var window = GetWindow<HiddenGameObjectTools>();
            window.titleContent = new GUIContent("Hidden GOs");
            GatherHiddenObjects();
        }
    }
}
#endif