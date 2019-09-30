//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    /// <summary>
    /// Defines a message for creating a custom view in the editor.
    /// </summary>
    public class NewCustomViewRequest
    {
        /// <summary>
        /// The Language Server Protocol 'method'.
        /// </summary>
        public static string Method => "powerShell/newCustomView";

        /// <summary>
        /// Gets or sets the Id of the view.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the title of the view.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the view's type.
        /// </summary>
        public CustomViewType ViewType { get; set;}
    }

    /// <summary>
    /// Defines a message for showing a custom view in the editor.
    /// </summary>
    public class ShowCustomViewRequest
    {
        /// <summary>
        /// The Language Server Protocol 'method'.
        /// </summary>
        public static string Method => "powerShell/showCustomView";

        /// <summary>
        /// Gets or sets the Id of the view.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the column in which the view should be shown.
        /// </summary>
        public ViewColumn ViewColumn { get; set; }
    }

    /// <summary>
    /// Defines a message for closing a custom view in the editor.
    /// </summary>
    public class CloseCustomViewRequest
    {
        /// <summary>
        /// The Language Server Protocol 'method'.
        /// </summary>
        public static string Method => "powerShell/closeCustomView";

        /// <summary>
        /// Gets or sets the Id of the view.
        /// </summary>
        public Guid Id { get; set; }
    }
}
