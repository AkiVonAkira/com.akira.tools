#if UNITY_EDITOR
using System.IO;
using akira;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

public class NamespaceInputPopup : EditorWindow
{
    private string namespaceInput = "akira";

    [MenuItem("Tools/Setup/Import Singleton")]
    static void ImportSingleton()
    {
        NamespaceInputPopup window = ScriptableObject.CreateInstance<NamespaceInputPopup>();
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 100);
        window.ShowPopup();
    }

    void CreateGUI()
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
            this.Close();
        };
        rootVisualElement.Add(button);
    }

    private void ImportSingletonScript(string nameSpace)
    {
        string packageName = "com.akira.tools";
        string txtPath = Path.Combine(
            Application.dataPath,
            "../Packages",
            packageName,
            "Scripts/Singleton.txt"
        );
        string outputPath = Path.Combine(
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
#endif
