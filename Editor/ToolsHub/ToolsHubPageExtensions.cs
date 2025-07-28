#if UNITY_EDITOR
using akira.UI;

namespace akira.ToolsHub
{
    /// <summary>
    ///     Standard implementation helper for ToolsHub pages
    /// </summary>
    public static class ToolsHubPageExtensions
    {
        /// <summary>
        ///     Draws a full page with standardized layout
        /// </summary>
        public static void DrawPage(this IToolsHubPage page)
        {
            // Draw the header
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

            // Footer with buttons
            PageLayout.BeginPageFooter();
            page.DrawFooter();
            PageLayout.EndPageFooter();
        }

        /// <summary>
        ///     Opens this page in the ToolsHub
        /// </summary>
        public static void ShowInToolsHub(this IToolsHubPage page)
        {
            ToolsHubManger.ShowPage(page.Title, page.DrawPage, page.OnPageResult);
        }
    }
}
#endif