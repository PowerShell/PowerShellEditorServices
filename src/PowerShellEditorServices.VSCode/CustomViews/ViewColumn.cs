//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.VSCode.CustomViews
{
    /// <summary>
    /// Defines the possible columns in which a custom view
    /// can be displayed.
    /// </summary>
    public enum ViewColumn
    {
        /// <summary>
        /// The first view column.
        /// </summary>
        One = 1,

        /// <summary>
        /// The second view column.
        /// </summary>
        Two,

        /// <summary>
        /// The third view column.
        /// </summary>
        Three
    }
}
