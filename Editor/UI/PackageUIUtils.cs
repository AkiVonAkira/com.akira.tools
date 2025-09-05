#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using akira.Packages;
using akira.ToolsHub;
using UnityEngine.Networking;
using System.Text.RegularExpressions; // parse URLs in descriptions
using System.Text; // StringBuilder for link coloring

namespace akira.UI
{
    /// <summary>
    /// Shared UI utilities for rendering package cards and standardized tags.
    /// Extracted from EssentialPackagesPage for reuse by future pages (e.g., Asset Store page).
    /// </summary>
    public static class PackageUIUtils
    {
        // Temporary global toggle to control description rendering across pages (no UI; code-only)
        public static bool ShowDescriptions = true;

        // New: allow description text selection and link color customization
        public static bool MakeDescriptionsSelectable = true;
        public static Color LinkColor = new Color(0.5f, 0.8f, 1f, 1f);

        // New: control visibility of specific Asset Store meta chips
        public static bool ShowOwnershipChip = false;
        public static bool ShowPriceChip = false;
        public static bool ShowFreePaidChip = false;

        // Cached card style
        private static GUIStyle _cardStyle;

        // Tag chip data
        public struct TagChip
        {
            public string Text;
            public Color Bg;
            public Color? Fg;
        }

        // Palette
        private static readonly Color TagEssentialBg = new(0.95f, 0.75f, 0.2f, 0.25f);
        private static readonly Color TagRecommendedBg = new(0.6f, 0.6f, 0.6f, 0.25f);
        private static readonly Color TagGitBg = new(0.2f, 0.5f, 0.9f, 0.25f);
        private static readonly Color TagInstalledBg = new(0.2f, 0.7f, 0.2f, 0.25f);
        private static readonly Color TagNotInstalledBg = new(0.7f, 0.2f, 0.2f, 0.25f);
        private static readonly Color TagUpdateBg = new(0.95f, 0.6f, 0.2f, 0.25f);
        private static readonly Color TagMetaBg = new(0.95f, 0.6f, 0.2f, 0.25f);
        private static readonly Color TagAssetStoreBg = new(0.4f, 0.3f, 0.9f, 0.25f);
        private static readonly Color TagFreeBg = new(0.25f, 0.65f, 0.3f, 0.25f);
        private static readonly Color TagPaidBg = new(0.9f, 0.45f, 0.2f, 0.25f);
        private static readonly Color TagDeprecatedBg = new(0.6f, 0.2f, 0.2f, 0.35f);
        private static readonly Color TagPreviewBg = new(0.9f, 0.7f, 0.2f, 0.25f);
        private static readonly Color TagOwnedBg = new(0.2f, 0.7f, 0.2f, 0.25f);

        // Known package metadata (extend as needed)
        private class PackageMeta
        {
            public bool RequiresRestart;
            public bool IsAssetStore;
            public bool? IsFree; // null=unknown
            public string[] ExtraTags; // e.g., "DOTS"
        }

        private static readonly System.Collections.Generic.Dictionary<string, PackageMeta> KnownMeta = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["com.unity.burst"] = new PackageMeta { RequiresRestart = true, IsFree = true },
            ["com.unity.cinemachine"] = new PackageMeta { IsFree = true },
            ["com.unity.inputsystem"] = new PackageMeta { IsFree = true },
            ["com.unity.addressables"] = new PackageMeta { IsFree = true },
        };

        public static GUIStyle GetCardStyle()
        {
            if (_cardStyle != null) return _cardStyle;
            _cardStyle = new GUIStyle(GUI.skin.box)
            {
                margin = new RectOffset(4, 4, 2, 2),
                padding = new RectOffset(8, 8, 6, 6)
            };
            _cardStyle.normal.background = UIEditorUtils.GetSolidTexture(UIEditorUtils.FoldoutBgColor);
            _cardStyle.border = new RectOffset(1, 1, 1, 1);
            return _cardStyle;
        }

        public static void DrawTagChip(TagChip chip)
        {
            var content = new GUIContent(chip.Text);
            var style = EditorStyles.miniLabel;
            var size = style.CalcSize(content);
            var paddingX = 6f;
            var paddingY = 2f;
            var rect = GUILayoutUtility.GetRect(size.x + paddingX * 2, size.y + paddingY * 2, GUILayout.ExpandWidth(false));
            var prevColor = GUI.color;
            GUI.color = chip.Bg;
            EditorGUI.DrawRect(rect, chip.Bg);
            GUI.color = chip.Fg ?? Color.white;
            var labelRect = new Rect(rect.x + paddingX, rect.y + paddingY, size.x, size.y);
            GUI.Label(labelRect, content, style);
            GUI.color = prevColor;
            GUILayout.Space(4);
        }

        // Measure a chip's width including padding and spacing used by DrawTagChip
        public static float MeasureChipWidth(TagChip chip)
        {
            var style = EditorStyles.miniLabel;
            var size = style.CalcSize(new GUIContent(chip.Text));
            var paddingX = 6f;
            var spacing = 4f; // trailing GUILayout.Space in DrawTagChip
            return size.x + paddingX * 2 + spacing;
        }

        // Draw chips in a wrapping flow layout to avoid horizontal overflow
        public static void DrawChipFlow(IEnumerable<TagChip> chips, float gap = 4f)
        {
            if (chips == null) return;
            var list = chips.ToList();
            if (list.Count == 0) return;

            // Available width: subtract more to account for card padding/margins
            var available = Mathf.Max(160f, EditorGUIUtility.currentViewWidth - 48f);
            var used = 0f;
            var open = false;

            void NewRow()
            {
                if (open) EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                open = true;
                used = 0f;
            }

            NewRow();
            for (var i = 0; i < list.Count; i++)
            {
                var w = MeasureChipWidth(list[i]);
                if (used > 0f && used + w > available)
                {
                    NewRow();
                }

                DrawTagChip(list[i]);
                used += w + gap;
            }

            if (open) EditorGUILayout.EndHorizontal();
        }

        public static IEnumerable<TagChip> BuildStandardChips(
            bool isEssential,
            bool isInstalled,
            bool hasUpdate,
            bool isGit,
            bool isTMP,
            string version,
            string latest)
        {
            // Essential/Recommended
            yield return new TagChip
            {
                Text = isEssential ? "Essential" : "Recommended",
                Bg = isEssential ? TagEssentialBg : TagRecommendedBg,
                Fg = isEssential ? new Color(0.95f, 0.85f, 0.2f) : (Color?)Color.white
            };

            // Git
            if (isGit)
                yield return new TagChip { Text = "Git", Bg = TagGitBg, Fg = new Color(0.7f, 0.85f, 1f) };

            // Installed status (TMP handled by caller with custom text)
            if (!isTMP)
            {
                yield return new TagChip
                {
                    Text = isInstalled ? "Installed" : "Not Installed",
                    Bg = isInstalled ? TagInstalledBg : TagNotInstalledBg,
                    Fg = Color.white
                };
            }

            // Version/update tag (right aligned typically; returned here for consistency)
            if (!string.IsNullOrEmpty(version))
            {
                if (hasUpdate && !string.IsNullOrEmpty(latest))
                    yield return new TagChip { Text = $"v{version} → v{latest}", Bg = TagUpdateBg, Fg = new Color(1f, 0.9f, 0.8f) };
                else
                    yield return new TagChip { Text = $"v{version}", Bg = new Color(0.45f, 0.45f, 0.45f, 0.25f), Fg = Color.white };
            }

            // Preview heuristic
            if (!string.IsNullOrEmpty(version) && version.IndexOf("pre", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new TagChip { Text = "Preview", Bg = TagPreviewBg, Fg = Color.white };
        }

        public static IEnumerable<TagChip> BuildMetaChips(PackageEntry entry)
        {
            if (entry == null)
                return Array.Empty<TagChip>();

            // Consolidate known meta with PackageEntry overrides to avoid duplicate chips
            var id = entry.Id ?? string.Empty;
            var displayName = entry.DisplayName ?? string.Empty;
            var description = entry.Description ?? string.Empty;

            KnownMeta.TryGetValue(id, out var known);

            // Combine flags with overrides taking precedence when explicitly set
            var isAssetStore = (entry.IsAssetStore == true) ||
                               (known?.IsAssetStore == true) ||
                               id.StartsWith("assetstore:", StringComparison.OrdinalIgnoreCase) ||
                               id.IndexOf("assetstore", StringComparison.OrdinalIgnoreCase) >= 0;

            var requiresRestart = (entry.RequiresRestart == true) || (known?.RequiresRestart == true);

            bool? isFree = entry.IsFree.HasValue ? entry.IsFree : known?.IsFree;

            // Build with explicit de-duplication so user ExtraTags don't re-add built-in labels
            var chips = new List<TagChip>();
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Asset Store", "Requires Restart", "Free", "Paid", "Deprecated", "Owned"
            };

            void AddOnce(string label, Color bg, Color? fg = null)
            {
                if (string.IsNullOrWhiteSpace(label)) return;
                if (emitted.Contains(label)) return;
                chips.Add(new TagChip { Text = label, Bg = bg, Fg = fg });
                emitted.Add(label);
            }

            if (isAssetStore)
                AddOnce("Asset Store", TagAssetStoreBg, Color.white);

            if (requiresRestart)
                AddOnce("Requires Restart", TagMetaBg, new Color(1f, 0.9f, 0.8f));

            if (isFree.HasValue && ShowFreePaidChip)
                AddOnce(isFree.Value ? "Free" : "Paid", isFree.Value ? TagFreeBg : TagPaidBg, Color.white);

            // Deprecated heuristic from name/description (same as base builder)
            var isDeprecated = (!string.IsNullOrEmpty(displayName) && displayName.IndexOf("deprecated", StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (!string.IsNullOrEmpty(description) && description.IndexOf("deprecated", StringComparison.OrdinalIgnoreCase) >= 0);
            if (isDeprecated)
                AddOnce("Deprecated", TagDeprecatedBg, Color.white);

            // Merge extra tags from known meta and entry, distinct and non-empty, excluding reserved/duplicates
            var extra = new List<string>();
            if (known?.ExtraTags != null) extra.AddRange(known.ExtraTags);
            if (entry.ExtraTags != null) extra.AddRange(entry.ExtraTags);

            foreach (var t in extra.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var label = t.Trim();
                if (reserved.Contains(label) || emitted.Contains(label)) continue;
                AddOnce(label, TagRecommendedBg, Color.white);
            }

            // Ownership chip for Asset Store entries (optional)
            if (ShowOwnershipChip && isAssetStore && entry.IsOwned == true)
            {
                AddOnce("Owned", TagOwnedBg, Color.white);
            }

            // Price embellishment: if Paid and Price specified, append (optional)
            if (ShowPriceChip && !isFree.GetValueOrDefault(true) && !string.IsNullOrWhiteSpace(entry.Price))
            {
                AddOnce($"Price {entry.Price}", TagPaidBg, Color.white);
            }

            return chips;
        }

        // ---- Card helpers (wrappers reused by multiple pages) ----
        public static void BeginCard()
        {
            EditorGUILayout.BeginVertical(GetCardStyle());
        }

        public static void EndCard()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        public static bool DrawTitleRow(string title, bool showToggle, bool currentEnabled, out bool newEnabled,
            string hyperlinkUrl = null, string tooltip = null)
        {
            EditorGUILayout.BeginHorizontal();
            if (showToggle)
            {
                newEnabled = EditorGUILayout.Toggle(currentEnabled, GUILayout.Width(18));
            }
            else
            {
                newEnabled = currentEnabled;
            }

            // Word-wrapped clickable label, slightly larger font size (+2)
            var link = !string.IsNullOrEmpty(hyperlinkUrl);
            var baseStyle = EditorStyles.boldLabel;
            var style = new GUIStyle(baseStyle)
            {
                wordWrap = true,
                richText = false,
                alignment = TextAnchor.UpperLeft,
                fontSize = Mathf.Max(10, baseStyle.fontSize + 2),
                normal = { textColor = link ? new Color(0.5f, 0.8f, 1f) : baseStyle.normal.textColor }
            };

            var content = new GUIContent(title ?? string.Empty, tooltip);
            // Reserve space for dropdown toggle on the right (24px + padding)
            var maxWidth = EditorGUIUtility.currentViewWidth - 50f; // Leave room for toggle
            EditorGUILayout.LabelField(content, style, GUILayout.MaxWidth(maxWidth));
            var rect = GUILayoutUtility.GetLastRect();

            bool clicked = false;
            if (link)
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                var e = Event.current;
                if (e.type == EventType.MouseUp && rect.Contains(e.mousePosition))
                {
                    Application.OpenURL(hyperlinkUrl);
                    clicked = true;
                }
            }

            EditorGUILayout.EndHorizontal();
            return clicked;
        }

        public static void DrawDescription(string description)
        {
            if (!ShowDescriptions) return;
            if (string.IsNullOrWhiteSpace(description)) return;
            // Use the same selectable + colored-link overlay approach
            DrawTextBlockWithClickableLinks(description);
        }

        public static void DrawDescriptionForAsset(PackageEntry entry)
        {
            if (!ShowDescriptions) return;
            if (entry == null)
                return;

            var desc = entry.Description;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                var wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
                EditorGUILayout.LabelField(desc, wrap);
                return;
            }

            // Skeleton when description missing and likely loading (has AssetStoreUrl)
            if (!string.IsNullOrEmpty(entry.AssetStoreUrl))
            {
                var baseColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                void Bar(float height, float widthFrac)
                {
                    var full = EditorGUILayout.GetControlRect(false, height, GUILayout.ExpandWidth(true));
                    var w = Mathf.Max(40f, full.width * Mathf.Clamp01(widthFrac));
                    var rect = new Rect(full.x, full.y, w, height);
                    EditorGUI.DrawRect(rect, baseColor);
                }

                GUILayout.Space(2);
                Bar(10, 1.0f);
                GUILayout.Space(4);
                Bar(10, 0.95f);
                GUILayout.Space(4);
                Bar(10, 0.8f);
                GUILayout.Space(2);
                return;
            }
        }

        // ---- Action buttons (shared layout) ----
        public struct ActionButton
        {
            public string Label;
            public bool Enabled;
            public Color Bg;
            public Action OnClick;
        }

        private static float MeasureButtonWidth(string label)
        {
            var style = GUI.skin.button;
            var size = style.CalcSize(new GUIContent(label));
            // No minimum width; allow compact buttons for better wrapping
            return size.x + 8f;
        }

        public static void DrawActionButtonsFlow(IEnumerable<ActionButton> buttons, float gap = 6f)
        {
            if (buttons == null) return;
            var list = buttons.ToList();
            if (list.Count == 0) return;

            var available = Mathf.Max(160f, EditorGUIUtility.currentViewWidth - 48f);
            var used = 0f;
            var open = false;

            void NewRow()
            {
                if (open) EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // right-align rows
                open = true;
                used = 0f;
            }

            NewRow();
            for (var i = 0; i < list.Count; i++)
            {
                var b = list[i];
                var w = MeasureButtonWidth(b.Label);
                if (used > 0f && used + w > available)
                {
                    NewRow();
                }

                var prev = GUI.backgroundColor;
                GUI.backgroundColor = b.Bg;
                EditorGUI.BeginDisabledGroup(!b.Enabled);
                if (GUILayout.Button(b.Label)) b.OnClick?.Invoke();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = prev;

                used += w + gap;
                if (i < list.Count - 1) GUILayout.Space(gap);
            }

            if (open) EditorGUILayout.EndHorizontal();
        }

        public static void DrawActionButtonsFlowWithLeftPrefix(string leftText, GUIStyle leftStyle,
            IEnumerable<ActionButton> buttons, float gap = 6f)
        {
            if (buttons == null) return;
            var list = buttons.ToList();
            if (list.Count == 0)
            {
                if (!string.IsNullOrEmpty(leftText))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(leftText, leftStyle ?? EditorStyles.label);
                    EditorGUILayout.EndHorizontal();
                }
                return;
            }

            var availableTotal = Mathf.Max(160f, EditorGUIUtility.currentViewWidth - 48f);
            var prefixContent = new GUIContent(leftText ?? string.Empty);
            var prefixWidth = (leftStyle ?? EditorStyles.label).CalcSize(prefixContent).x;

            // First row with prefix left and buttons right
            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(leftText))
                EditorGUILayout.LabelField(prefixContent, leftStyle ?? EditorStyles.label);
            else
                GUILayout.Space(0);

            var used = 0f;
            var firstRowCount = 0;
            var measured = list.Select(b => MeasureButtonWidth(b.Label)).ToArray();
            var available = Mathf.Max(80f, availableTotal - prefixWidth - 12f);

            for (var i = 0; i < list.Count; i++)
            {
                var add = measured[i] + (firstRowCount == 0 ? 0f : gap);
                if (used + add <= available)
                {
                    used += add;
                    firstRowCount++;
                }
                else break;
            }

            GUILayout.FlexibleSpace();
            for (var i = 0; i < firstRowCount; i++)
            {
                if (i > 0) GUILayout.Space(gap);
                var b = list[i];
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = b.Bg;
                EditorGUI.BeginDisabledGroup(!b.Enabled);
                if (GUILayout.Button(b.Label)) b.OnClick?.Invoke();
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();

            // Remaining rows
            if (firstRowCount < list.Count)
            {
                var idx = firstRowCount;
                while (idx < list.Count)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    used = 0f;
                    var startIdx = idx;
                    var countThisRow = 0;

                    while (idx < list.Count)
                    {
                        var add = measured[idx] + (countThisRow == 0 ? 0f : gap);
                        if (used + add <= availableTotal)
                        {
                            used += add;
                            countThisRow++;
                            idx++;
                        }
                        else break;
                    }

                    for (var i = 0; i < countThisRow; i++)
                    {
                        if (i > 0) GUILayout.Space(gap);
                        var b = list[startIdx + i];
                        var prev = GUI.backgroundColor;
                        GUI.backgroundColor = b.Bg;
                        EditorGUI.BeginDisabledGroup(!b.Enabled);
                        if (GUILayout.Button(b.Label)) b.OnClick?.Invoke();
                        EditorGUI.EndDisabledGroup();
                        GUI.backgroundColor = prev;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // Simple editor image cache for thumbnails
        private class ImageOp { public string Url; public UnityWebRequest Req; }
        private static readonly Dictionary<string, Texture2D> _imageCache = new();
        private static readonly HashSet<string> _imageLoading = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<ImageOp> _activeImages = new();
        private static bool _listeningImages;

        private static void EnsureImageUpdate()
        {
            if (_listeningImages) return;
            _listeningImages = true;
            EditorApplication.update += UpdateImages;
        }

        private static void UpdateImages()
        {
            for (var i = _activeImages.Count - 1; i >= 0; i--)
            {
                var op = _activeImages[i];
                if (!op.Req.isDone) continue;

                try
                {
                    if (op.Req.result == UnityWebRequest.Result.Success)
                    {
                        var tex = DownloadHandlerTexture.GetContent(op.Req);
                        if (tex != null) _imageCache[op.Url] = tex;
                    }
                }
                catch { }
                finally
                {
                    op.Req.Dispose();
                    _activeImages.RemoveAt(i);
                }
            }

            if (_activeImages.Count == 0)
            {
                EditorApplication.update -= UpdateImages;
                _listeningImages = false;
                _imageLoading.Clear();
                // Repaint Tools Hub to show newly loaded images
                EditorApplication.delayCall += () =>
                {
                    if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                        EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                };
            }
        }

        private static Texture2D GetOrRequestImage(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (_imageCache.TryGetValue(url, out var tex)) return tex;
            if (!_imageLoading.Contains(url))
            {
                try
                {
                    var req = UnityWebRequestTexture.GetTexture(url);
                    req.timeout = 10;
                    req.SendWebRequest();
                    _activeImages.Add(new ImageOp { Url = url, Req = req });
                    _imageLoading.Add(url);
                    EnsureImageUpdate();
                }
                catch { }
            }
            return null;
        }

        private static Texture2D _authorIcon;

        // Wrapper: keep legacy signature; forward to new overload with entry.Description and a stable key
        public static void DrawAssetHeader(PackageEntry entry)
        {
            var key = entry?.AssetStoreId ?? entry?.Id ?? entry?.AssetStoreUrl;
            DrawAssetHeader(entry, overviewOrDescription: entry?.Description, expandKey: key, truncateChars: 300);
        }

        // Draws a compact Asset Store header (thumbnail, author) with optional overview/description snippet
        public static void DrawAssetHeader(PackageEntry entry, string overviewOrDescription = null, string expandKey = null, int truncateChars = 300)
        {
            if (entry == null) return;
            var thumb = GetOrRequestImage(entry.AssetImageUrl);
            
            if (_authorIcon == null)
            {
                var assetPath = "Packages/com.akira.tools/Assets/author.png";
                try { _authorIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath); } catch { _authorIcon = null; }
            }

            EditorGUILayout.BeginHorizontal();
            // Thumbnail: prefer 16:9 when image is wide; fallback to square
            float maxThumbWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.25f, 96f, 160f);
            bool wide = false;
            if (thumb != null)
            {
                var aspect = thumb.width > 0 && thumb.height > 0 ? (float)thumb.width / thumb.height : 0f;
                wide = aspect > 1.3f; // treat as 16:9-ish when wider than ~4:3
            }

            float drawW = wide ? maxThumbWidth : 64f;
            float drawH = wide ? Mathf.Round(drawW * 9f / 16f) : 64f;

            var thumbRect = GUILayoutUtility.GetRect(drawW, drawH, GUILayout.Width(drawW), GUILayout.Height(drawH));
            if (thumb != null)
            {
                GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleAndCrop);
            }
            else
            {
                // Skeleton block for image
                EditorGUI.DrawRect(thumbRect, new Color(0.25f, 0.25f, 0.25f, 1f));
            }

            GUILayout.Space(8);
            // Right column (chips + author + description)
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // Meta chips (Asset Store, Free/Paid, Owned, tags)
            var chips = BuildMetaChips(entry);
            DrawChipFlow(chips);

            var author = !string.IsNullOrEmpty(entry.AssetAuthor) ? entry.AssetAuthor : null;
            if (!string.IsNullOrEmpty(author))
            {
                EditorGUILayout.BeginHorizontal();
                if (_authorIcon != null)
                {
                    var iconRect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.Height(14f));
                    GUI.DrawTexture(iconRect, _authorIcon, ScaleMode.ScaleToFit, true);
                    GUILayout.Space(4);
                }
                EditorGUILayout.LabelField(author, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
            }
            else if (!string.IsNullOrEmpty(entry.AssetStoreUrl))
            {
                // Author skeleton line with optional icon on the left
                EditorGUILayout.BeginHorizontal();
                if (_authorIcon != null)
                {
                    var iconRect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.Height(14f));
                    GUI.DrawTexture(iconRect, _authorIcon, ScaleMode.ScaleToFit, true);
                    GUILayout.Space(4);
                }
                var w = Mathf.Min(120f, EditorGUIUtility.currentViewWidth * 0.2f);
                var rect = GUILayoutUtility.GetRect(w, 12f, GUILayout.Width(w), GUILayout.Height(12f));
                EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 1f));
                EditorGUILayout.EndHorizontal();
            }

            // Overview/Description snippet under author with expandable toggle
            if (ShowDescriptions)
            {
                var desc = !string.IsNullOrWhiteSpace(overviewOrDescription) ? overviewOrDescription : entry.Description;

                if (!string.IsNullOrWhiteSpace(desc))
                {
                    var key = !string.IsNullOrEmpty(expandKey) ? expandKey : (entry.AssetStoreId ?? entry.Id ?? entry.AssetStoreUrl ?? string.Empty);
                    bool expanded = !string.IsNullOrEmpty(key) && _expandedDescKeys.Contains(key);

                    if (expanded)
                    {
                        // Render full text with clickable links (colored)
                        DrawTextBlockWithClickableLinks(desc);

                        DrawInlineLink("Show less", () =>
                        {
                            if (!string.IsNullOrEmpty(key)) _expandedDescKeys.Remove(key);
                            EditorApplication.delayCall += () =>
                            {
                                if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                                    EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                            };
                        });
                    }
                    else
                    {
                        var trimmed = desc.Trim();
                        // Weighted truncation helpers
                        static int WeightedLength(string s, int newlineWeight)
                        {
                            if (string.IsNullOrEmpty(s)) return 0;
                            var len = 0;
                            for (int i = 0; i < s.Length; i++)
                            {
                                var c = s[i];
                                if (c == '\r') { if (i + 1 < s.Length && s[i + 1] == '\n') { len += newlineWeight; i++; } else len += newlineWeight; }
                                else if (c == '\n') { len += newlineWeight; }
                                else { len += 1; }
                            }
                            return len;
                        }
                        static string WeightedTruncate(string s, int limit, int newlineWeight)
                        {
                            if (string.IsNullOrEmpty(s)) return s;
                            var acc = 0;
                            int i = 0;
                            int lastGoodCutPoint = -1; // Track the last good sentence ending or newline

                            for (; i < s.Length; i++)
                            {
                                var c = s[i];
                                if (c == '\r')
                                {
                                    var add = newlineWeight;
                                    if (i + 1 < s.Length && s[i + 1] == '\n') { i++; }
                                    if (acc + add > limit) break;
                                    acc += add;
                                    lastGoodCutPoint = i + 1; // Newline is always a good cut point
                                }
                                else if (c == '\n')
                                {
                                    var add = newlineWeight;
                                    if (acc + add > limit) break;
                                    acc += add;
                                    lastGoodCutPoint = i + 1; // Newline is always a good cut point
                                }
                                else
                                {
                                    if (acc + 1 > limit) break;
                                    acc += 1;

                                    // Check for sentence endings (period, exclamation, question mark followed by space or end)
                                    if ((c == '.' || c == '!' || c == '?') &&
                                        (i == s.Length - 1 || char.IsWhiteSpace(s[i + 1])))
                                    {
                                        lastGoodCutPoint = i + 1;
                                    }
                                }
                            }

                            // If we stopped due to limit and we're mid-sentence, prefer extending to the next newline
                            if (i < s.Length)
                            {
                                bool justEndedSentence = (i > 0 && (s[i - 1] == '.' || s[i - 1] == '!' || s[i - 1] == '?'));
                                bool justAtNewline = (i > 0 && (s[i - 1] == '\n' || s[i - 1] == '\r'));
                                if (!justEndedSentence && !justAtNewline)
                                {
                                    // Look ahead for the first newline; cap how far we scan (to avoid huge jumps)
                                    const int maxLookahead = 400; // chars
                                    int scanEnd = Math.Min(s.Length, i + maxLookahead);
                                    int nextNewline = -1;
                                    int sentenceEndAhead = -1;
                                    for (int j = i; j < scanEnd; j++)
                                    {
                                        char cj = s[j];
                                        if (cj == '\n' || cj == '\r') { nextNewline = j; break; }
                                        if ((cj == '.' || cj == '!' || cj == '?') && (j == s.Length - 1 || char.IsWhiteSpace(j + 1 < s.Length ? s[j + 1] : ' ')))
                                        {
                                            if (sentenceEndAhead < 0) sentenceEndAhead = j + 1;
                                        }
                                    }

                                    if (nextNewline >= 0)
                                    {
                                        i = nextNewline; // cut exactly at newline (exclude it)
                                    }
                                    else if (sentenceEndAhead > 0)
                                    {
                                        i = sentenceEndAhead; // fall back to the end of this sentence
                                    }
                                }
                            }

                            // If we found a good sentence ending point behind and we're close to it, prefer it
                            if (lastGoodCutPoint > 0 && i > lastGoodCutPoint && (i - lastGoodCutPoint) <= 20)
                            {
                                i = lastGoodCutPoint;
                            }

                            return s.Substring(0, i).TrimEnd();
                        }

                        var newlineWeightChars = 60;
                        var needsTruncate = WeightedLength(trimmed, newlineWeightChars) > truncateChars;
                        if (needsTruncate)
                        {
                            var snippet = WeightedTruncate(trimmed, truncateChars, newlineWeightChars);

                            // Single rich-text label (no selectable overlay) so clicks work and text doesn't overlap
                            var richStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
                            var coloredSnippet = ColorizeLinks(snippet, LinkColor, skipBoundaryEnd: true) + "…";
                            var coloredContent = new GUIContent(coloredSnippet);
                            var rect = GUILayoutUtility.GetRect(coloredContent, richStyle, GUILayout.ExpandWidth(true));
                            GUI.Label(rect, coloredContent, richStyle);

                            // Track click vs drag and make inline URLs clickable (skip tail-partial URL)
                            UpdateLinkClickTracker(rect);
                            MakeInlineUrlsClickableTruncated(snippet, rect, richStyle);

                            // Separate row with a right-aligned clickable link to expand
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            var linkStyle = new GUIStyle(EditorStyles.label)
                            {
                                wordWrap = false,
                                normal = { textColor = new Color(0.5f, 0.8f, 1f) },
                                hover = { textColor = new Color(0.6f, 0.85f, 1f) },
                                active = { textColor = new Color(0.4f, 0.75f, 1f) }
                            };
                            var moreContent = new GUIContent("Read more");
                            var moreRect = GUILayoutUtility.GetRect(moreContent, linkStyle, GUILayout.ExpandWidth(false));
                            EditorGUIUtility.AddCursorRect(moreRect, MouseCursor.Link);
                            if (GUI.Button(moreRect, moreContent, linkStyle))
                            {
                                if (!string.IsNullOrEmpty(key)) _expandedDescKeys.Add(key);
                                EditorApplication.delayCall += () =>
                                {
                                    if (EditorWindow.HasOpenInstances<ToolsHubManager>())
                                        EditorWindow.GetWindow<ToolsHubManager>().Repaint();
                                };
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            DrawTextBlockWithClickableLinks(trimmed);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(entry.AssetStoreUrl))
                {
                    // Skeleton description while loading
                    void Bar(float height, float widthFrac)
                    {
                        var full = EditorGUILayout.GetControlRect(false, height, GUILayout.ExpandWidth(true));
                        var w2 = Mathf.Max(40f, full.width * Mathf.Clamp01(widthFrac));
                        var r2 = new Rect(full.x, full.y, w2, height);
                        EditorGUI.DrawRect(r2, new Color(0.35f, 0.35f, 0.35f, 1f));
                    }
                    GUILayout.Space(2);
                    Bar(10, 1.0f);
                    GUILayout.Space(4);
                    Bar(10, 0.95f);
                    GUILayout.Space(4);
                    Bar(10, 0.8f);
                    GUILayout.Space(2);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // Track which entries have expanded overview/description
        private static readonly HashSet<string> _expandedDescKeys = new(StringComparer.OrdinalIgnoreCase);

        // Draw a small hyperlink-like button used under descriptions
        private static void DrawInlineLink(string text, Action onClick)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.5f, 0.8f, 1f) },
                hover = { textColor = new Color(0.6f, 0.85f, 1f) },
                active = { textColor = new Color(0.4f, 0.75f, 1f) }
            };
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var rect = GUILayoutUtility.GetRect(new GUIContent(text), style, GUILayout.ExpandWidth(false));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            if (GUI.Button(rect, text, style)) onClick?.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        // Lightweight URL extraction
        private static readonly Regex UrlRegex = new Regex(@"https?://[^\s)]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Click tracking to avoid opening links on drag-selection
        private static Vector2 _linkMouseDownPos;
        private static int _linkMouseDownButton;
        private static void UpdateLinkClickTracker(Rect _)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                _linkMouseDownPos = e.mousePosition;
                _linkMouseDownButton = e.button;
            }
        }
        private static bool ShouldOpenLinkOnMouseUp(Rect hitRect)
        {
            var e = Event.current;
            if (e.type != EventType.MouseUp || e.button != 0) return false;
            // Require both mouse down and up inside the same link rect and no drag
            if (!hitRect.Contains(e.mousePosition)) return false;
            if (!hitRect.Contains(_linkMouseDownPos)) return false;
            return Vector2.Distance(_linkMouseDownPos, e.mousePosition) <= 4f;
        }

        // Render a wrapped text block and make any inline URLs clickable within it
        private static void DrawTextBlockWithClickableLinks(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            bool hasLinks = UrlRegex.IsMatch(text);

            if (!hasLinks && MakeDescriptionsSelectable)
            {
                // No links: prefer selectable for easy copy
                var wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
                var content = new GUIContent(text);
                var rect = GUILayoutUtility.GetRect(content, wrapStyle, GUILayout.ExpandWidth(true));
                EditorGUI.SelectableLabel(rect, content.text, wrapStyle);
                return;
            }

            // Single-pass rich text render for colored links; avoids text overlay artifacts
            var richStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = hasLinks };
            var colored = hasLinks ? ColorizeLinks(text, LinkColor) : text;
            var guiContent = new GUIContent(colored);
            var drawRect = GUILayoutUtility.GetRect(guiContent, richStyle, GUILayout.ExpandWidth(true));
            GUI.Label(drawRect, guiContent, richStyle);

            if (hasLinks)
            {
                // Make links clickable
                UpdateLinkClickTracker(drawRect);
                MakeInlineUrlsClickable(text, drawRect, richStyle);
            }
        }

        // Variant used for truncated snippets: skip any URL that ends exactly at snippet end (likely cut mid-URL)
        private static void MakeInlineUrlsClickableTruncated(string snippet, Rect labelRect, GUIStyle style)
        {
            if (string.IsNullOrEmpty(snippet)) return;
            var matches = UrlRegex.Matches(snippet);
            if (matches == null || matches.Count == 0) return;

            foreach (Match m in matches)
            {
                // If the URL terminates at the snippet boundary, skip to avoid broken links pre-expansion
                if (m.Index + m.Length >= snippet.Length) continue;

                var raw = m.Value;
                var trimmed = TrimUrlForDisplay(raw, out var suffixLen);
                var clickableLen = Mathf.Max(0, m.Length - suffixLen);
                if (clickableLen == 0) continue;

                var rects = GetWrappedSubstringRects(snippet, m.Index, clickableLen, labelRect, style);
                foreach (var r in rects)
                {
                    EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
                    if (ShouldOpenLinkOnMouseUp(r))
                    {
                        if (!string.IsNullOrEmpty(trimmed)) Application.OpenURL(trimmed);
                    }
                }
            }
        }

        // Compute and overlay clickable rects for inline URLs inside the given rendered text rect
        private static void MakeInlineUrlsClickable(string fullText, Rect labelRect, GUIStyle style)
        {
            if (string.IsNullOrEmpty(fullText)) return;
            var matches = UrlRegex.Matches(fullText);
            if (matches == null || matches.Count == 0) return;

            foreach (Match m in matches)
            {
                var raw = m.Value;
                var trimmed = TrimUrlForDisplay(raw, out var suffixLen);
                var clickableLen = Mathf.Max(0, m.Length - suffixLen);
                if (clickableLen == 0) continue;

                var rects = GetWrappedSubstringRects(fullText, m.Index, clickableLen, labelRect, style);
                foreach (var r in rects)
                {
                    EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
                    if (ShouldOpenLinkOnMouseUp(r))
                    {
                        if (!string.IsNullOrEmpty(trimmed)) Application.OpenURL(trimmed);
                    }
                }
            }
        }

        // Return rectangles covering where a substring [start, start+length) is rendered in a wrapped label
        private static List<Rect> GetWrappedSubstringRects(string text, int start, int length, Rect labelRect, GUIStyle style)
        {
            var rects = new List<Rect>();
            if (length <= 0) return rects;

            // Normalize newlines to \n
            var s = text.Replace("\r\n", "\n").Replace('\r', '\n');
            // Map original indices if needed
            int MapIndex(int idx)
            {
                // Since we only collapsed CRLF to \n, the index shift equals number of CR before idx
                // For simplicity and performance in editor, approximate by scanning up to idx
                int shift = 0;
                for (int i = 0, j = 0; i < s.Length && j < idx; i++, j++)
                {
                    // original text may have CRLF; our s removed one char; we skip handling precise mapping here
                }
                return idx; // acceptable approximation for typical Asset Store text
            }

            int linkStart = MapIndex(start);
            int linkEnd = linkStart + length;

            float maxW = Mathf.Max(10f, labelRect.width);
            float lineH = style.lineHeight > 0f ? style.lineHeight : style.CalcSize(new GUIContent("Ay")).y;

            float x = 0f; float y = 0f;
            int iChar = 0;

            // Helper to advance by a run and wrap on spaces; split very long tokens if needed
            while (iChar < s.Length)
            {
                // Handle explicit newline
                if (s[iChar] == '\n') { x = 0f; y += lineH; iChar++; continue; }

                // Build next token (word + following space if any)
                int tokStart = iChar;
                int tokEnd = tokStart;
                while (tokEnd < s.Length && s[tokEnd] != ' ' && s[tokEnd] != '\n') tokEnd++;
                // Include one space if present
                int tokStop = tokEnd < s.Length && s[tokEnd] == ' ' ? tokEnd + 1 : tokEnd;
                string token = s.Substring(tokStart, tokStop - tokStart);
                float tokenW = style.CalcSize(new GUIContent(token)).x;

                // If token doesn't fit, wrap unless at line start; if token itself longer than width, split by chars
                if (x > 0f && x + tokenW > maxW)
                {
                    x = 0f; y += lineH;
                }

                if (tokenW <= maxW)
                {
                    // Whole token fits on this line; if token overlaps link range, carve rects per overlap
                    AddOverlapRectsForRange(tokStart, tokStop, ref x, y, maxW, style, s, linkStart, linkEnd, labelRect, rects);
                    x += tokenW;
                    iChar = tokStop;
                }
                else
                {
                    // Split token across lines character-by-character
                    int k = tokStart;
                    while (k < tokStop)
                    {
                        string ch = s.Substring(k, 1);
                        float cw = style.CalcSize(new GUIContent(ch)).x;
                        if (x > 0f && x + cw > maxW) { x = 0f; y += lineH; }
                        // If this char is within link range, add rect
                        if (k >= linkStart && k < linkEnd)
                        {
                            rects.Add(new Rect(labelRect.x + x, labelRect.y + y, cw, lineH));
                        }
                        x += cw;
                        k++;
                    }
                    iChar = tokStop;
                }
            }

            // Merge adjacent rects on same line for cleaner hit areas
            rects = MergeAdjacentRects(rects);
            return rects;
        }

        private static void AddOverlapRectsForRange(int runStart, int runStop, ref float x, float y, float maxW, GUIStyle style, string s, int linkStart, int linkEnd, Rect baseRect, List<Rect> outRects)
        {
            // run is already known to fit; we only need to add rects for the intersection with [linkStart, linkEnd)
            int a = Mathf.Max(runStart, linkStart);
            int b = Mathf.Min(runStop, linkEnd);
            if (a >= b) return;
            // Width before 'a'
            string pre = s.Substring(runStart, a - runStart);
            float preW = style.CalcSize(new GUIContent(pre)).x;
            float startX = x + preW;
            string mid = s.Substring(a, b - a);
            float midW = style.CalcSize(new GUIContent(mid)).x;
            outRects.Add(new Rect(baseRect.x + startX, baseRect.y + y, midW, style.lineHeight > 0f ? style.lineHeight : style.CalcSize(new GUIContent("Ay")).y));
        }

        private static List<Rect> MergeAdjacentRects(List<Rect> rects)
        {
            if (rects == null || rects.Count < 2) return rects ?? new List<Rect>();
            rects.Sort((r1, r2) =>
            {
                int cy = Mathf.Approximately(r1.y, r2.y) ? 0 : (r1.y < r2.y ? -1 : 1);
                if (cy != 0) return cy;
                return r1.x < r2.x ? -1 : 1;
            });
            var merged = new List<Rect>();
            var cur = rects[0];
            for (int i = 1; i < rects.Count; i++)
            {
                var r = rects[i];
                if (Mathf.Approximately(cur.y, r.y) && cur.xMax + 0.5f >= r.x)
                {
                    cur = new Rect(cur.x, cur.y, Mathf.Max(cur.xMax, r.xMax) - cur.x, Mathf.Max(cur.height, r.height));
                }
                else
                {
                    merged.Add(cur); cur = r;
                }
            }
            merged.Add(cur);
            return merged;
        }

        // ===== Helpers: colorize link substrings using rich text =====
        private static string ColorToHex(Color c)
        {
            var r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            var g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            var b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        // Determine the visible URL text (without trailing punctuation) and suffix length to keep layout identical
        private static string TrimUrlForDisplay(string url, out int suffixLen)
        {
            suffixLen = 0;
            if (string.IsNullOrEmpty(url)) return url;
            var trimmed = url;

            // Trim trailing punctuation visually but keep it outside the link
            int originalLen = trimmed.Length;
            trimmed = trimmed.TrimEnd('.', ',', ';');
            suffixLen += originalLen - trimmed.Length;

            // Special case: URL followed by a ')' without an opening '(' inside URL (common in prose)
            if (trimmed.EndsWith(")") && !trimmed.Contains("("))
            {
                trimmed = trimmed.TrimEnd(')');
                suffixLen += 1;
            }

            return trimmed;
        }

        private static string ColorizeLinks(string text, Color color, bool skipBoundaryEnd = false)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var matches = UrlRegex.Matches(text);
            if (matches == null || matches.Count == 0) return text;

            var hex = ColorToHex(color);
            var sb = new StringBuilder(text.Length + 32);
            int lastIndex = 0;
            foreach (Match m in matches)
            {
                if (skipBoundaryEnd && m.Index + m.Length >= text.Length)
                {
                    // Append the untouched tail before skipping to keep indices aligned
                    continue;
                }

                // Append text before the link
                sb.Append(text, lastIndex, m.Index - lastIndex);

                // Compute visible part and punctuation suffix
                var raw = m.Value;
                var visible = TrimUrlForDisplay(raw, out var suffixLen);
                var suffix = suffixLen > 0 ? raw.Substring(raw.Length - suffixLen) : string.Empty;

                // Wrap only the visible URL; then append the suffix punctuation uncolored
                sb.Append("<color=#").Append(hex).Append(">").Append(visible).Append("</color>");
                sb.Append(suffix);

                lastIndex = m.Index + m.Length;
            }
            if (lastIndex < text.Length) sb.Append(text, lastIndex, text.Length - lastIndex);
            return sb.ToString();
        }
    }
}
#endif




