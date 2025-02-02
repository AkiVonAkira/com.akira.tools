#if UNITY_EDITOR
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace akira
{
    public static class Packages
    {
        public static async Task ReplacePackageFromGist(string id, string user = "AkiVonAkira")
        {
            var url = GetGistUrl(id, user);
            var contents = await GetContents(url);
            ReplacePackageFile(contents);
        }

        private static string GetGistUrl(string id, string user = "AkiVonAkira") =>
            $"https://gist.githubusercontent.com/{user}/{id}/raw";

        private static async Task<string> GetContents(string url)
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            return content;
        }

        private static void ReplacePackageFile(string contents)
        {
            var existing = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            File.WriteAllText(existing, contents);
            UnityEditor.PackageManager.Client.Resolve();
        }

        public static Task<bool> InstallUnityPackage(string packageName)
        {
            var tcs = new TaskCompletionSource<bool>();
            UnityEditor.PackageManager.Requests.AddRequest request =
                UnityEditor.PackageManager.Client.Add(packageName);

            EditorApplication.update += CheckProgress;

            void CheckProgress()
            {
                if (request.IsCompleted)
                {
                    if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                    {
                        Debug.Log($"Package {packageName} installed successfully.");
                        tcs.SetResult(true);
                    }
                    else
                    {
                        Debug.LogError(
                            $"Failed to install package {packageName}. Error: {request.Error.message}"
                        );
                        tcs.SetResult(false);
                    }
                    EditorApplication.update -= CheckProgress;
                }
            }

            return tcs.Task;
        }
    }
}
#endif
