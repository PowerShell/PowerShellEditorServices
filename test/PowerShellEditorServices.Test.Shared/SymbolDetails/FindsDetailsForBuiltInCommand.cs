// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails
{
    public class FindsDetailsForBuiltInCommand
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion(
                file: TestUtilities.NormalizePath("SymbolDetails/SymbolDetails.ps1"),
                text: string.Empty,
                startLineNumber: 1,
                startColumnNumber: 10,
                startOffset: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                endOffset: 0);
    }
}
