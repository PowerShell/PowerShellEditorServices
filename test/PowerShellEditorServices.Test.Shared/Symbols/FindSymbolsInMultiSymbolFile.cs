// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Symbols
{
    public static class FindSymbolsInMultiSymbolFile
    {
        public static readonly ScriptRegion SourceDetails =
            new(
                file: TestUtilities.NormalizePath("Symbols/MultipleSymbols.ps1"),
                text: string.Empty,
                startLineNumber: 0,
                startColumnNumber: 0,
                startOffset: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                endOffset: 0);
    }
}
