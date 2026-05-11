#if UNITY_EDITOR
namespace Akira.ToolsHub
{
    /// <summary>
    /// Interface for ToolsHub pages with proper separation of concerns.
    /// Pages ONLY provide content - layout/spacing/scrolling handled by PageLayout extension.
    /// </summary>
    public interface IToolsHubPage
    {
        string Title { get; }
        string Description { get; }
        
        /// <summary>
        /// Draw header content (search bars, filters, controls).
        /// NO layout containers, spacing, or padding!
        /// </summary>
        void DrawHeader();
        
        /// <summary>
        /// Draw main content (lists, cards, forms).
        /// NO scroll views or layout containers!
        /// </summary>
        void DrawContent();
        
        /// <summary>
        /// Draw content footer - appears below scrollable content, above action buttons.
        /// Use for forms, extended UI (add package form, preset manager, etc).
        /// NO layout containers or spacing!
        /// </summary>
        void DrawContentFooter();
        
        /// <summary>
        /// Draw footer content (action buttons, warnings).
        /// NO layout containers or spacing!
        /// </summary>
        void DrawFooter();
        
        /// <summary>
        /// Called when the page is closed with a result (Success, Failure, Cancelled)
        /// </summary>
        void OnPageResult(PageOperationResult result);
    }
}
#endif