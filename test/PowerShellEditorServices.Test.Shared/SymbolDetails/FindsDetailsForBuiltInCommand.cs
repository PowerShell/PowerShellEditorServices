//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails
{
    public class FindsDetailsForBuiltInCommand
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = TestUtilities.NormalizePath("SymbolDetails/SymbolDetails.ps1"),
                StartLineNumber = 1,
                StartColumnNumber = 10
            };
    }
}
