//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    /// <summary>
    /// Defines an interface for a component which can create
    /// new IHtmlContentView implementation instances.
    /// </summary>
    public interface IHtmlContentViews
    {
        /// <summary>
        /// Creates an instance of an IHtmlContentView implementation.
        /// </summary>
        /// <param name="viewTitle">The title of the view to create.</param>
        /// <returns>
        /// A Task to await for completion, returns the IHtmlContentView instance.
        /// </returns>
        Task<IHtmlContentView> CreateHtmlContentViewAsync(string viewTitle);
    }
}
