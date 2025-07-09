using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR

namespace akira.Scene
{
    public static class SceneHierarchySetup
    {
        private static readonly string[] DefaultHierarchy =
        {
            "[SETUP]/Default",
            "[SETUP]/Character",
            "------------------------",
            "[MANAGERS]/Game Manager",
            "[MANAGERS]/Audio Manager",
            "[MANAGERS]/Input Manager",
            "[MANAGERS]/UI Manager",
            "------------------------",
            "[LEVEL]/Environment",
            "[LEVEL]/Lighting",
            "[LEVEL]/Terrain",
            "[LEVEL]/Navigation",
            "------------------------",
            "[DYNAMIC]/Characters",
            "[DYNAMIC]/Enemies",
            "[DYNAMIC]/Pickups",
            "[DYNAMIC]/Props",
            "------------------------",
            "[SYSTEMS]/AI",
            "[SYSTEMS]/Physics",
            "[SYSTEMS]/Pooling",
            "[SYSTEMS]/Services",
            "[SYSTEMS]/Spawners",
            "[SYSTEMS]/States",
            "[SYSTEMS]/Utilities",
            "------------------------",
            "[UI]/Canvases",
            "[UI]/EventSystem",
            "------------------------"
        };

        private static readonly Dictionary<string, GameObject> HeaderObjects = new();

        public static void CreateBasicHierarchy()
        {
            var currentScene = SceneManager.GetActiveScene();
            HeaderObjects.Clear();

            foreach (var objectName in DefaultHierarchy)
            {
                if (objectName == "------------------------")
                {
                    CreateGameObject(objectName);

                    continue;
                }

                if (objectName.Contains("/"))
                {
                    var split = objectName.Split('/');
                    var parentName = split[0];
                    var childName = split[1];

                    // Ensure parent exists
                    if (!HeaderObjects.ContainsKey(parentName))
                    {
                        var parent = CreateGameObject(parentName);
                        HeaderObjects.Add(parentName, parent);
                    }

                    // Create child under parent
                    var child = CreateGameObject(childName);
                    child.transform.SetParent(HeaderObjects[parentName].transform);
                }
                else
                {
                    // Create header object
                    var go = CreateGameObject(objectName);
                    if (objectName.StartsWith("[") && objectName.EndsWith("]")) HeaderObjects[objectName] = go;
                }
            }

            EditorSceneManager.MarkSceneDirty(currentScene);
        }

        private static GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            go.transform.SetAsLastSibling();

            return go;
        }
    }
}
#endif