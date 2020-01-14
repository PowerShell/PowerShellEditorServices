//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    /// <summary>
    /// Defines the interface for an arbitrary custom view that
    /// can be shown in Visual Studio Code.
    /// </summary>
    public interface ICustomView
    {
        /// <summary>
        /// Gets the unique ID of the view.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the display title of the view.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Shows the view in the specified column.
        /// </summary>
        /// <param name="viewColumn">The column in which the view will be shown.</param>
        /// <returns>A Task which can be awaited for completion.</returns>
        Task Show(ViewColumn viewColumn);

        /// <summary>
        /// Closes the view in the editor.
        /// </summary>
        /// <returns>A Task which can be awaited for completion.</returns>
        Task Close();
    }
}
