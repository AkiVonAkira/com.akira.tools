#if UNITY_EDITOR
namespace akira.ToolsHub
{
    /// <summary>
    ///     Interface for standardized ToolsHub pages
    /// </summary>
    public interface IToolsHubPage
    {
        /// <summary>
        ///     Title to display for this page
        /// </summary>
        string Title { get; }

        /// <summary>
        ///     Optional description text for this page
        /// </summary>
        string Description { get; }

        /// <summary>
        ///     Draw controls in the content header area
        /// </summary>
        void DrawContentHeader();

        /// <summary>
        ///     Draw the main content area of the page
        /// </summary>
        void DrawScrollContent();

        /// <summary>
        ///     Draw the footer area with additional content
        /// </summary>
        void DrawContentFooter();

        /// <summary>
        ///     Draw the footer area with buttons
        /// </summary>
        void DrawFooter();

        /// <summary>
        ///     Called when the page is closed with a result
        /// </summary>
        void OnPageResult(PageOperationResult result);
    }
}
#endif