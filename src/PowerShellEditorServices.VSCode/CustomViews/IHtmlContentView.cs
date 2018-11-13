//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    /// <summary>
    /// Defines the interface for a custom view which displays
    /// rendered HTML content in an editor tab.
    /// </summary>
    public interface IHtmlContentView : ICustomView
    {
        /// <summary>
        /// Sets the HTML body content of the view.
        /// </summary>
        /// <param name="htmlBodyContent">
        /// The HTML content that is placed inside of the page's body tag.
        /// </param>
        /// <returns>A Task which can be awaited for completion.</returns>
        Task SetContentAsync(string htmlBodyContent);

        /// <summary>
        /// Sets the HTML content of the view.
        /// </summary>
        /// <param name="htmlContent">
        /// The HTML content that is placed inside of the page's body tag.
        /// </param>
        /// <returns>A Task which can be awaited for completion.</returns>
        Task SetContentAsync(HtmlContent htmlContent);

        /// <summary>
        /// Appends HTML body content to the view.
        /// </summary>
        /// <param name="appendedHtmlBodyContent">
        /// The HTML fragment to be appended to the output stream.
        /// </param>
        /// <returns>A Task which can be awaited for completion.</returns>
        Task AppendContentAsync(string appendedHtmlBodyContent);
    }
}
