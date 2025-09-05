#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using akira.Packages;
using akira.ToolsHub;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Globalization;
using UnityEditor.PackageManager;

namespace akira.EditorServices
{
    /// <summary>
    /// Fetches Asset Store page metadata (price/free) using UnityWebRequest and updates PackageEntry.
    /// Uses a tiny polling loop to process requests on the editor update thread.
    /// </summary>
    public static class AssetStoreInfoService
    {
        private const int TTL_SECONDS = 300; // 5 minutes

        private class FetchOp
        {
            public string Id;               // PackageEntry.Id
            public string Url;              // Asset store page URL
            public UnityWebRequest Req;     // Active request
        }

        private static readonly HashSet<string> Enqueued = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<FetchOp> Active = new();
        private static bool _listening;

        // Fetch only when IsFree/Price unknown (used on add/first view)
        public static void QueueFetch(PackageEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.AssetStoreUrl) || string.IsNullOrWhiteSpace(entry.Id))
                return;
            if (entry.IsFree.HasValue || !string.IsNullOrEmpty(entry.Price))
                return; // already known
            InternalQueue(entry);
        }

        // Fetch if stale compared to TTL (used on page refresh)
        public static void QueueFetchIfStale(PackageEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.AssetStoreUrl) || string.IsNullOrWhiteSpace(entry.Id))
                return;
            if (!IsStale(entry)) return;
            InternalQueue(entry);
        }

        private static void InternalQueue(PackageEntry entry)
        {
            if (Enqueued.Contains(entry.Id)) return;
            Enqueued.Add(entry.Id);

            // Parse AssetStoreId from URL once if available
            var parsedId = TryParseProductId(entry.AssetStoreUrl);
            if (!string.IsNullOrEmpty(parsedId))
            {
                var pe = ToolsHubSettings.GetPackage(entry.Id) ?? entry;
                pe.AssetStoreId = pe.AssetStoreId ?? parsedId;
                ToolsHubSettings.AddOrUpdatePackage(pe);
                ToolsHubSettings.Save();
            }

            try
            {
                var req = UnityWebRequest.Get(entry.AssetStoreUrl);
                req.timeout = 10; // seconds
                req.SendWebRequest();
                Active.Add(new FetchOp { Id = entry.Id, Url = entry.AssetStoreUrl, Req = req });
                EnsureUpdateHook();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AssetStoreInfoService: failed to start request for {entry.AssetStoreUrl}: {ex.Message}");
            }
        }

        public static bool IsStale(PackageEntry entry)
        {
            if (entry == null) return false;
            var last = entry.LastAssetFetchUnixSeconds;
            if (last <= 0) return true;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return (now - last) >= TTL_SECONDS;
        }

        private static void EnsureUpdateHook()
        {
            if (_listening) return;
            _listening = true;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var f = Active[i];
                var req = f.Req;
                if (!req.isDone) continue;

                try
                {
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var html = req.downloadHandler.text;
                        var (priceLabel, isFree) = ParsePrice(html);

                        var pe = ToolsHubSettings.GetPackage(f.Id);
                        if (pe != null)
                        {
                            // Interpret 0/0.00 as free even if label not FREE
                            var isZero = IsZeroPrice(priceLabel);
                            if (isZero) isFree = true;

                            if (isFree.HasValue) pe.IsFree = isFree.Value;
                            if (!string.IsNullOrWhiteSpace(priceLabel))
                                pe.Price = isZero ? "FREE" : NormalizePrice(priceLabel);

                            // Parse extended metadata
                            // Prefer og:title from the page; override any early heuristic title
                            var ogTitle = ExtractMeta(html, "og:title");
                            if (!string.IsNullOrWhiteSpace(ogTitle))
                                pe.AssetTitle = NormalizeTitle(ogTitle);

                            // Prefer a richer Overview from the page body; fallback to og:description
                            var overview = ExtractOverviewFromHtml(html);
                            if (!string.IsNullOrWhiteSpace(overview))
                            {
                                pe.Description = TruncateWords(overview, 200);
                            }
                            else
                            {
                                var ogDesc = ExtractMeta(html, "og:description");
                                if (string.IsNullOrWhiteSpace(pe.Description) && !string.IsNullOrWhiteSpace(ogDesc))
                                    pe.Description = ogDesc;
                            }

                            pe.AssetImageUrl ??= ExtractMeta(html, "og:image");
                            pe.AssetAuthor ??= ExtractAuthor(html);

                            pe.UpmPackageId ??= ExtractUpmId(html);

                            // Category from URL (e.g., /packages/tools/utilities/...)
                            pe.AssetCategory ??= ExtractCategoryFromUrl(f.Url);
                            if (!string.IsNullOrEmpty(pe.AssetCategory))
                            {
                                // Also add category tokens into ExtraTags for chips (top-level and subcategory)
                                var parts = pe.AssetCategory.Split('/');
                                pe.ExtraTags ??= new List<string>();
                                foreach (var token in parts)
                                {
                                    var label = token.Trim();
                                    if (!string.IsNullOrEmpty(label) && !pe.ExtraTags.Contains(label))
                                        pe.ExtraTags.Add(label);
                                }
                            }

                            // Stamp last fetched time
                            pe.LastAssetFetchUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                            ToolsHubSettings.AddOrUpdatePackage(pe);
                            ToolsHubSettings.Save();
                        }
                    }
                    else
                    {
                        // Leave existing fields as-is; this is best-effort
                        if (Debug.isDebugBuild)
                            Debug.LogWarning($"AssetStoreInfoService: request failed for {f.Url}: {req.error}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"AssetStoreInfoService: error processing response for {f.Url}: {ex.Message}");
                }
                finally
                {
                    req.Dispose();
                    Active.RemoveAt(i);
                }
            }

            if (Active.Count == 0)
            {
                EditorApplication.update -= Update;
                _listening = false;
                // repaint ToolsHub if open
                EditorApplication.delayCall += () =>
                {
                    if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                        EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                };
                Enqueued.Clear();
            }
        }

        // Try reading a price from common patterns in Asset Store pages
        // Returns (priceLabel, isFree) where priceLabel could be like "$15" or "FREE".
        private static (string priceLabel, bool? isFree) ParsePrice(string html)
        {
            if (string.IsNullOrEmpty(html)) return (null, null);

            try
            {
                // JSON-LD price field
                var m1 = Regex.Match(html, @"""price""\s*:\s*""(?<p>[^""]+)""", RegexOptions.IgnoreCase);
                if (m1.Success)
                {
                    var p = m1.Groups["p"].Value.Trim();
                    var norm = NormalizePrice(p);
                    if (!string.IsNullOrEmpty(norm))
                        return (norm, IsFreeLabel(norm) || IsZeroPrice(norm));
                }

                // Inline label with FREE or currency + amount
                var m2 = Regex.Match(html, @">(?<p>FREE|Free|[$€£]\s?\d+[\d\.,]*)<", RegexOptions.IgnoreCase);
                if (m2.Success)
                {
                    var p = m2.Groups["p"].Value.Trim();
                    var norm = NormalizePrice(p);
                    return (norm, IsFreeLabel(norm) || IsZeroPrice(norm));
                }

                // Nearby "price" token followed by a value
                var m3 = Regex.Match(html, @"price[^<>{}]{0,60}([$€£]\s?\d+[\d\.,]*|FREE|Free)", RegexOptions.IgnoreCase);
                if (m3.Success)
                {
                    var p = Regex.Match(m3.Value, @"([$€£]\s?\d+[\d\.,]*|FREE|Free)", RegexOptions.IgnoreCase).Value;
                    var norm = NormalizePrice(p);
                    return (norm, IsFreeLabel(norm) || IsZeroPrice(norm));
                }
            }
            catch { /* ignore and fall through */ }

            return (null, null);
        }

        private static string NormalizePrice(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Trim();
            if (IsFreeLabel(raw) || IsZeroPrice(raw)) return "FREE";
            // Keep leading currency and digits
            var m = Regex.Match(raw, @"^([$€£])\s?(?<n>[0-9][0-9\.,]*)");
            if (m.Success)
            {
                var symbol = m.Groups[1].Value;
                var num = m.Groups["n"].Value;
                return $"{symbol}{num}";
            }
            return raw;
        }

        private static bool IsZeroPrice(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var str = s.Trim();
            // Strip currency symbols
            str = Regex.Replace(str, @"^[$€£]\s?", "");
            // Normalize separators
            str = str.Replace(",", ".");
            // Keep digits and decimal point
            if (decimal.TryParse(str, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var val))
            {
                return val == 0m;
            }
            return false;
        }

        private static bool IsFreeLabel(string s)
        {
            return !string.IsNullOrEmpty(s) && s.Trim().Equals("FREE", StringComparison.OrdinalIgnoreCase);
        }

        private static string TryParseProductId(string url)
        {
            try
            {
                // Typical URL: https://assetstore.unity.com/packages/slug-name-123456
                var uri = new Uri(url);
                var segs = uri.AbsolutePath.Trim('/').Split('/');
                if (segs.Length >= 2)
                {
                    var last = segs[^1];
                    var m = Regex.Match(last, @"-(?<id>\d+)$");
                    if (m.Success) return m.Groups["id"].Value;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        private static string ExtractMeta(string html, string property)
        {
            try
            {
                // <meta property="og:title" content="...">
                var rx = new Regex($@"<meta[^>]+property=""{Regex.Escape(property)}""[^>]*content=""(?<c>[^""]+)""", RegexOptions.IgnoreCase);
                var m = rx.Match(html);
                if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups["c"].Value);

                // Some pages use name=
                rx = new Regex($@"<meta[^>]+name=""{Regex.Escape(property)}""[^>]*content=""(?<c>[^""]+)""", RegexOptions.IgnoreCase);
                m = rx.Match(html);
                if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups["c"].Value);
            }
            catch { }
            return null;
        }

        private static string ExtractAuthor(string html)
        {
            try
            {
                // JSON-LD brand name: "brand":{"@type":"Brand","name":"Author"}
                var m = Regex.Match(html, @"""brand""\s*:\s*\{[^}]*""name""\s*:\s*""(?<a>[^""\\]+)""", RegexOptions.IgnoreCase);
                if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups["a"].Value);

                // meta name="author"
                var rx = new Regex(@"<meta[^>]+name=""author""[^>]*content=""(?<c>[^""]+)""", RegexOptions.IgnoreCase);
                var m2 = rx.Match(html);
                if (m2.Success) return System.Net.WebUtility.HtmlDecode(m2.Groups["c"].Value);

                // data-test publisher name
                var m3 = Regex.Match(html, @"data-test=""publisher-name""[^>]*>\s*<[^>]*>\s*(?<n>[^<]+)<", RegexOptions.IgnoreCase);
                if (m3.Success) return System.Net.WebUtility.HtmlDecode(m3.Groups["n"].Value.Trim());
            }
            catch { }
            return null;
        }

        private static string ExtractUpmId(string html)
        {
            try
            {
                // Look for com.company.packagename style tokens. Prefer those around "upm" or "package" context.
                var ctx = Regex.Match(html, @"(?<ctx>(upm|package)[^<>{}]{0,200})com\.[a-z0-9\._-]+", RegexOptions.IgnoreCase);
                if (ctx.Success)
                {
                    var id = Regex.Match(ctx.Value, @"com\.[a-z0-9\._-]+", RegexOptions.IgnoreCase).Value;
                    if (!string.IsNullOrEmpty(id)) return id;
                }
                // Fallback: first com.* looking id
                var m = Regex.Match(html, @"com\.[a-z0-9\._-]+", RegexOptions.IgnoreCase);
                if (m.Success) return m.Value;
            }
            catch { }
            return null;
        }

        private static string ExtractCategoryFromUrl(string url)
        {
            // Expecting: https://assetstore.unity.com/packages/<cat>/<subcat>/slug-id
            try
            {
                var uri = new Uri(url);
                var segs = uri.AbsolutePath.Trim('/').Split('/');
                var idx = Array.FindIndex(segs, s => string.Equals(s, "packages", StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    var cat = idx + 1 < segs.Length ? segs[idx + 1] : null;
                    var sub = idx + 2 < segs.Length ? segs[idx + 2] : null;
                    string Title(string s)
                    {
                        if (string.IsNullOrEmpty(s)) return null;
                        s = s.Replace('-', ' ');
                        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s);
                    }
                    var c1 = Title(cat);
                    var c2 = Title(sub);
                    if (!string.IsNullOrEmpty(c1) && !string.IsNullOrEmpty(c2)) return $"{c1}/{c2}";
                    if (!string.IsNullOrEmpty(c1)) return c1;
                }
            }
            catch { }
            return null;
        }

        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return title;
            var t = title.Trim();
            var idx = t.IndexOf('|');
            if (idx > 0)
            {
                t = t.Substring(0, idx).Trim();
            }
            return t;
        }

        // Try to parse a richer Overview/Description from the page markup
        private static string ExtractOverviewFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            try
            {
                // 0) Explicit Overview container by id="description-panel" (author-written description)
                var descPanel = Regex.Match(html, @"id=""description-panel""[^>]*>(?<c>[\s\S]*?)</div>", RegexOptions.IgnoreCase);
                if (descPanel.Success)
                {
                    var raw = descPanel.Groups["c"].Value;
                    var text = HtmlToPlainText(raw);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Heuristic: drop marketing phrases like "Get 10% off" that sometimes appear adjacent
                        text = Regex.Replace(text, @"^Get\s+\d+%\s+off[\s\S]*?\.", string.Empty, RegexOptions.IgnoreCase).Trim();
                        if (!string.IsNullOrWhiteSpace(text)) return text;
                    }
                }

                // 1) Look for a long description container commonly used on store pages
                var longDesc = Regex.Match(html, @"data-test=""(long-description|description)""[^>]*>(?<c>[\s\S]*?)</div>", RegexOptions.IgnoreCase);
                if (longDesc.Success)
                {
                    var raw = longDesc.Groups["c"].Value;
                    var text = HtmlToPlainText(raw);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }

                // 2) Section following an Overview heading
                var ovh = Regex.Match(html, @"<h[12][^>]*>\s*Overview\s*</h[12]>(?<c>[\s\S]{0,5000}?)<(h[12]|section|div)\b", RegexOptions.IgnoreCase);
                if (ovh.Success)
                {
                    var raw = ovh.Groups["c"].Value;
                    var text = HtmlToPlainText(raw);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }

                // 3) Fallback: first substantial paragraph-like block
                var p = Regex.Match(html, @"<p[^>]*>(?<c>[\s\S]*?)</p>", RegexOptions.IgnoreCase);
                if (p.Success)
                {
                    var raw = p.Groups["c"].Value;
                    var text = HtmlToPlainText(raw);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
            catch { }
            return null;
        }

        private static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            try
            {
                // Remove scripts/styles
                html = Regex.Replace(html, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<style[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
                // Replace <br> and <li> with newlines/markers
                html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"</li>", "\n", RegexOptions.IgnoreCase);
                // Strip remaining tags
                var text = Regex.Replace(html, @"<[^>]+>", string.Empty);
                // Decode HTML entities
                text = System.Net.WebUtility.HtmlDecode(text);
                // Collapse whitespace
                text = Regex.Replace(text, @"\s+", " ").Trim();
                return text;
            }
            catch { return html; }
        }

        private static string TruncateWords(string text, int maxWords)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var parts = Regex.Split(text.Trim(), @"\s+");
            if (parts.Length <= maxWords) return text;
            return string.Join(" ", parts, 0, Math.Max(0, maxWords)) + " …";
        }
    }
}
#endif
