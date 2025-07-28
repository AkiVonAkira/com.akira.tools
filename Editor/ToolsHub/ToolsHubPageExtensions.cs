#if UNITY_EDITOR
using akira.UI;

namespace akira.ToolsHub
{
    public static class ToolsHubPageExtensions
    {
        public static void DrawPage(this IToolsHubPage page)
        {
            PageLayout.DrawPageHeader(page.Title, page.Description);

            PageLayout.BeginContentSpacing();
            page.DrawContentHeader();
            PageLayout.EndContentSpacing();

            PageLayout.BeginPageContent();
            page.DrawScrollContent();
            PageLayout.EndPageContent();

            PageLayout.BeginContentSpacing();
            page.DrawContentFooter();
            PageLayout.EndContentSpacing();

            PageLayout.BeginPageFooter();
            page.DrawFooter();
            PageLayout.EndPageFooter();
        }

        public static void ShowInToolsHub(this IToolsHubPage page)
        {
            ToolsHubManger.ShowPage(page.Title, page.DrawPage, page.OnPageResult);
        }
    }
}
#endif