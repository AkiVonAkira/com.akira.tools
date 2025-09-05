#if UNITY_EDITOR
using System.Collections.Generic;

namespace akira.Packages
{
    /// <summary>
    /// Data model for a package listed in the Tools Hub. Supports registry, git, and URL packages.
    /// Includes optional metadata and tag hints for UI chips.
    /// </summary>
    public class PackageEntry
    {
        // Identity
        public string Id;               // e.g., com.unity.cinemachine or git+https://github.com/user/repo.git
        public string DisplayName;      // Friendly name for UI; falls back to Id when empty
        public string Description;      // Short description for UI

        // Selection
        public bool IsEssential;        // Shown with the Essential tag; sorted first
        public bool IsEnabled;          // Included when installing/removing in batch

        // Optional UI/meta hints (override heuristics)
        public bool? IsAssetStore;      // If true, show Asset Store tag
        public bool? IsFree;            // true = Free, false = Paid, null = unknown
        public bool? RequiresRestart;   // If true, show Requires Restart tag
        public bool? IsOwned;          // Asset Store ownership hint; null when unknown

        // Free-form extra tags (e.g., "DOTS", "Editor Only")
        public List<string> ExtraTags = new();

        // Asset Store support
        public string AssetStoreUrl;    // Link to the Asset Store page
        public string AssetStoreId;     // Optional short id or SKU for matching
        public string Price;            // Optional label like "$15"; use IsFree when null/empty

        // Asset Store data fetching
        public long LastAssetFetchUnixSeconds; // last successful asset store fetch (UTC seconds)

        // Asset Store additional metadata
        public string UpmPackageId;        // If present, allows direct install via UPM
        public string AssetTitle;          // Title parsed from Asset Store page (og:title)
        public string AssetAuthor;         // Author/brand name
        public string AssetImageUrl;       // Thumbnail image URL (og:image)
        public float AssetRatingValue;     // Average rating value (0..5)
        public int AssetRatingCount;       // Number of ratings/reviews
        public string AssetCategory;       // Category path like "Tools/Utilities" parsed from page URL/metadata
    }
}
#endif
