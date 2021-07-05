// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    /// <summary>
    /// Contains details about the HTML content to be
    /// displayed in an IHtmlContentView.
    /// </summary>
    public class HtmlContent
    {
        /// <summary>
        /// Gets or sets the HTML body content.
        /// </summary>
        public string BodyContent { get; set; }

        /// <summary>
        /// Gets or sets the array of JavaScript file paths
        /// to be used in the HTML content.
        /// </summary>
        public string [] JavaScriptPaths { get; set; }

        /// <summary>
        /// Gets or sets the array of stylesheet (CSS) file
        /// paths to be used in the HTML content.
        /// </summary>
        public string [] StyleSheetPaths { get; set; }
    }
}
