#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace akira.Packages
{
    /// <summary>
    /// Utilities for working with Unity Package IDs and Git-like identifiers.
    /// Centralizes normalization and matching logic for reuse across UI and manager code.
    /// </summary>
    public static class PackageIdUtils
    {
        // Determine if a string looks like a Git/URL style package identifier
        public static bool IsGitLike(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            return packageId.StartsWith("git+") ||
                   packageId.StartsWith("https://") ||
                   packageId.StartsWith("http://") ||
                   packageId.StartsWith("ssh://") ||
                   packageId.StartsWith("git://") ||
                   packageId.Contains(":");
        }

        // Normalize for Client.Add by stripping npm-style git+ prefix
        public static string NormalizeForAdd(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return packageId;
            return packageId.StartsWith("git+") ? packageId.Substring(4) : packageId;
        }

        // Extract a human-friendly repo/package token (e.g., last segment of a URL)
        public static string NormalizePackageId(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return packageId;

            if (IsGitLike(packageId))
            {
                var url = NormalizeUrlForComparison(packageId);
                var parts = url.Split('/')
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();
                if (parts.Length > 0)
                {
                    return parts[^1];
                }
            }

            return packageId;
        }

        // Normalize URL/id for robust comparison (lowercase, strip protocol, .git, trailing slashes, hash, query)
        public static string NormalizeUrlForComparison(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var s = raw.Trim().ToLowerInvariant();
            if (s.StartsWith("git+")) s = s.Substring(4);

            // Convert scp-like syntax git@host:owner/repo(.git) to host/owner/repo
            if (s.StartsWith("git@"))
            {
                var atIdx = s.IndexOf('@');
                var colonIdx = s.IndexOf(':');
                if (atIdx >= 0 && colonIdx > atIdx)
                {
                    var host = s.Substring(atIdx + 1, colonIdx - atIdx - 1);
                    var path = s.Substring(colonIdx + 1);
                    s = host + "/" + path;
                }
            }

            // Strip protocol
            if (s.StartsWith("https://")) s = s.Substring(8);
            else if (s.StartsWith("http://")) s = s.Substring(7);
            else if (s.StartsWith("ssh://")) s = s.Substring(6);
            else if (s.StartsWith("git://")) s = s.Substring(6);

            // Remove hash/query fragments
            var hashIdx = s.IndexOf('#');
            if (hashIdx >= 0) s = s.Substring(0, hashIdx);
            var qIdx = s.IndexOf('?');
            if (qIdx >= 0) s = s.Substring(0, qIdx);

            // Strip trailing .git and slashes
            if (s.EndsWith(".git")) s = s.Substring(0, s.Length - 4);
            while (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);

            return s;
        }

        // Heuristic matching between an input id/URL and an installed package info
        public static bool IsPackageIdMatch(string packageId, PackageInfo installedPackage)
        {
            if (string.IsNullOrEmpty(packageId) || installedPackage == null)
                return false;

            // Direct match by canonical name
            if (packageId == installedPackage.name)
                return true;

            if (IsGitLike(packageId))
            {
                var normalizedId = NormalizePackageId(packageId); // repo name
                var inputUrlNorm = NormalizeUrlForComparison(packageId);

                // 1) Match by repo name against end of package name (e.g., com.company.repo)
                if (!string.IsNullOrEmpty(installedPackage.name))
                {
                    string Canon(string x) => string.IsNullOrEmpty(x) ? x : x.Replace("_", "-");
                    if (Canon(installedPackage.name).EndsWith(Canon(normalizedId), StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // 2) Match against installed packageId which often contains the URL and commit
                if (!string.IsNullOrEmpty(installedPackage.packageId))
                {
                    var pkgId = installedPackage.packageId.ToLowerInvariant();
                    var atIdx = pkgId.IndexOf('@');
                    var urlPart = atIdx >= 0 && atIdx + 1 < pkgId.Length ? pkgId.Substring(atIdx + 1) : pkgId;
                    var pkgUrlNorm = NormalizeUrlForComparison(urlPart);

                    if (!string.IsNullOrEmpty(pkgUrlNorm))
                    {
                        if (pkgUrlNorm.Equals(inputUrlNorm, StringComparison.OrdinalIgnoreCase)) return true;
                        if (pkgUrlNorm.Contains(inputUrlNorm, StringComparison.OrdinalIgnoreCase)) return true;
                        if (inputUrlNorm.Contains(pkgUrlNorm, StringComparison.OrdinalIgnoreCase)) return true;
                    }

                    // Fallback: simple contains of repo name
                    if (pkgId.Contains(normalizedId, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }
    }
}
#endif

