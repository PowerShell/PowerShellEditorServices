//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Analysis
{
    /// <summary>
    /// Contains details for a code correction which can be applied from a ScriptFileMarker.
    /// </summary>
    public class MarkerCorrection
    {
        /// <summary>
        /// Gets or sets the display name of the code correction.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the list of ScriptRegions that define the edits to be made by the correction.
        /// </summary>
        public ScriptRegion[] Edits { get; set; }
    }
}
