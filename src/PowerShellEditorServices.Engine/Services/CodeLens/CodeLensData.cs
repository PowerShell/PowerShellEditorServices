//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.CodeLenses
{
    /// <summary>
    /// Represents data expected back in an LSP CodeLens response.
    /// </summary>
    internal class CodeLensData
    {
        public string Uri { get; set; }

        public string ProviderId { get; set; }
    }
}
