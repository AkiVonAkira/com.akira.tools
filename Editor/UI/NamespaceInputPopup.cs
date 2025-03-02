#if UNITY_EDITOR
using System.IO;
using Editor.Files;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace akira.UI
{
    public class NamespaceInputPopup : EditorWindow
    {
        private string namespaceInput = "akira";

        private void CreateGUI()
        {
            var label = new Label("Enter the namespace for the Singleton script:");
            rootVisualElement.Add(label);

            var textField = new TextField();
            textField.value = namespaceInput;
            textField.RegisterValueChangedCallback(evt => namespaceInput = evt.newValue);
            rootVisualElement.Add(textField);

            var button = new Button();
            button.text = "OK";
            button.clicked += () =>
            {
                ImportSingletonScript(namespaceInput);
                Close();
            };
            rootVisualElement.Add(button);
        }

        [MenuItem("Tools/Setup/Import Singleton")]
        private static void ImportSingleton()
        {
            var window = CreateInstance<NamespaceInputPopup>();
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
            window.ShowPopup();
        }

        private void ImportSingletonScript(string nameSpace)
        {
            var packageName = "com.akira.tools";
            var txtPath = Path.Combine(
                Application.dataPath,
                "../Packages",
                packageName,
                "Scripts/Singleton.txt"
            );
            var outputPath = Path.Combine(
                Application.dataPath,
                "_Project",
                "_Scripts",
                "Utilities",
                "Singleton.cs"
            );

            ImportFile.ImportTextAsScript(txtPath, outputPath, nameSpace);
            Debug.Log("Singleton imported successfully!");
        }
    }
}
#endif