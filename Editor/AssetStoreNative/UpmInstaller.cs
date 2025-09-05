#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace akira.AssetStoreNative
{
    public static class UpmInstaller
    {
        private static AddRequest _add;
        private static bool _inProgress;

        public static void Install(string idOrUrl)
        {
            if (string.IsNullOrWhiteSpace(idOrUrl)) return;
            if (_inProgress)
            {
                Debug.LogWarning("Another install is in progress.");
                return;
            }
            try
            {
                _add = Client.Add(idOrUrl.Trim());
                _inProgress = true;
                EditorApplication.update += Poll;
            }
            catch (Exception ex)
            {
                Debug.LogError($"UPM install failed to start: {ex.Message}");
            }
        }

        private static void Poll()
        {
            if (_add == null) { Stop(); return; }
            if (!_add.IsCompleted) return;
            if (_add.Status == StatusCode.Success)
            {
                Debug.Log($"Installed package: {_add.Result?.name} {_add.Result?.version}");
            }
            else if (_add.Status >= StatusCode.Failure)
            {
                Debug.LogError($"UPM install error: {_add.Error?.message}");
            }
            Stop();
        }

        private static void Stop()
        {
            EditorApplication.update -= Poll;
            _inProgress = false;
            _add = null;
        }
    }
}
#endif

