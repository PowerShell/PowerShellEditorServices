// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
