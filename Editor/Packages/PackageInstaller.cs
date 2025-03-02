#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace akira.Packages
{
    public static class PackageInstaller
    {
        public static async Task<bool> InstallUnityPackage(string packageName)
        {
            var request = Client.Add(packageName);
            var tcs = new TaskCompletionSource<bool>();

            EditorApplication.update += CheckProgress;

            void CheckProgress()
            {
                if (!request.IsCompleted) return;

                var success = request.Status == StatusCode.Success;
                if (success)
                    Debug.Log($"Package {packageName} installed successfully.");
                else
                    Debug.LogError($"Failed to install package {packageName}. Error: {request.Error.message}");

                EditorApplication.update -= CheckProgress;
                tcs.SetResult(success);
            }

            return await tcs.Task;
        }
    }
}
#endif