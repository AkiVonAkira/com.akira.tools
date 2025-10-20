#if UNITY_EDITOR
namespace Akira.ToolsHub
{
    public interface IToolsHubPage
    {
        string Title { get; }
        string Description { get; }
        void DrawContentHeader();
        void DrawScrollContent();
        void DrawContentFooter();
        void DrawFooter();
        void OnPageResult(PageOperationResult result);
    }
}
#endif