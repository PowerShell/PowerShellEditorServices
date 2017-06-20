//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    /// <summary>
    /// Defines a message for setting the content of a HtmlContentView.
    /// </summary>
    public class SetHtmlContentViewRequest
    {
        /// <summary>
        /// The RequestType for this request.
        /// </summary>
        public static readonly
            RequestType<SetHtmlContentViewRequest, object, object, object> Type =
            RequestType<SetHtmlContentViewRequest, object, object, object>.Create("powerShell/setHtmlViewContent");

        /// <summary>
        /// Gets or sets the Id of the view.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the HTML body content to set in the view.
        /// </summary>
        public string HtmlBodyContent { get; set; }
    }

    /// <summary>
    /// Defines a message for appending to the content of an IHtmlContentView.
    /// </summary>
    public class AppendHtmlContentViewRequest
    {
        /// <summary>
        /// The RequestType for this request.
        /// </summary>
        public static readonly
            RequestType<AppendHtmlContentViewRequest, object, object, object> Type =
            RequestType<AppendHtmlContentViewRequest, object, object, object>.Create("powerShell/appendHtmlViewContent");

        /// <summary>
        /// Gets or sets the Id of the view.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the HTML body content to append to the view.
        /// </summary>
        public string AppendedHtmlBodyContent { get; set; }
    }
}
