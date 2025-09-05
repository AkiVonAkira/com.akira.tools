#if UNITY_EDITOR
namespace akira.AssetStoreNative
{
    // Minimal info for owned assets listing
    public class OwnedAssetInfo
    {
        public long ProductId;
        public string DisplayName;
        public override string ToString() => string.IsNullOrEmpty(DisplayName) ? ProductId.ToString() : $"{ProductId} - {DisplayName}";
    }
}
#endif

