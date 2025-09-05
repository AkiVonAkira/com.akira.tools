#if UNITY_EDITOR
using System.Threading.Tasks;
using akira.ToolsHub;
using UnityEditor;

namespace akira.Packages
{
    public static class PackageInstaller
    {
        // Delegates to the central PackageManager to avoid duplicated logic
        public static Task<bool> InstallUnityPackage(string packageName)
        {
            var tcs = new TaskCompletionSource<bool>();

            void OnComplete(string id, bool success)
            {
                if (id != packageName) return;
                PackageManager.OnPackageInstallComplete -= OnComplete;
                PackageManager.OnPackageInstallError -= OnError;
                tcs.TrySetResult(success);
            }

            void OnError(string id)
            {
                if (id != packageName) return;
                PackageManager.OnPackageInstallComplete -= OnComplete;
                PackageManager.OnPackageInstallError -= OnError;
                tcs.TrySetResult(false);
            }

            PackageManager.OnPackageInstallComplete += OnComplete;
            PackageManager.OnPackageInstallError += OnError;

            // Kick off install on main thread
            EditorApplication.delayCall += () => PackageManager.InstallPackage(packageName);

            return tcs.Task;
        }
    }
}
#endif