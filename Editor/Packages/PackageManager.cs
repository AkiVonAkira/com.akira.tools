#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace akira.Packages
{
    public static class PackageManager
    {
        private static readonly Queue<string> packagesToInstall = new();
        private static AddRequest currentRequest;

        public static async Task InstallPackages(string[] packages)
        {
            foreach (var package in packages)
                packagesToInstall.Enqueue(package);

            if (packagesToInstall.Count > 0)
                await InstallNextPackage();
        }

        private static async Task InstallNextPackage()
        {
            while (packagesToInstall.Count > 0)
            {
                var packageId = packagesToInstall.Dequeue();
                var success = await InstallPackage(packageId);

                if (!success)
                {
                    Debug.LogError($"Failed to install package: {packageId}");

                    continue;
                }

                await Task.Delay(1000); // Prevent overwhelming Unity's package manager
            }
        }

        private static async Task<bool> InstallPackage(string packageId)
        {
            var tcs = new TaskCompletionSource<bool>();
            currentRequest = Client.Add(packageId);

            EditorApplication.update += CheckProgress;

            void CheckProgress()
            {
                if (!currentRequest.IsCompleted) return;

                var success = currentRequest.Status == StatusCode.Success;

                if (success)
                    Debug.Log($"Successfully installed package: {packageId}");
                else
                    Debug.LogError($"Package installation failed: {currentRequest.Error.message}");

                EditorApplication.update -= CheckProgress;
                tcs.SetResult(success);
            }

            return await tcs.Task;
        }
    }
}
#endif