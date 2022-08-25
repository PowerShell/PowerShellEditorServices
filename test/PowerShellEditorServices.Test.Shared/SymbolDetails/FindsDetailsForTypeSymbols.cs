// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails
{
    public static class FindsDetailsForTypeSymbolsData
    {
        public static readonly ScriptRegion EnumMemberSourceDetails = new(
            file: TestUtilities.NormalizePath("SymbolDetails/TypeSymbolDetails.ps1"),
            text: string.Empty,
            startLineNumber: 20,
            startColumnNumber: 6,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion PropertySourceDetails = new(
            file: TestUtilities.NormalizePath("SymbolDetails/TypeSymbolDetails.ps1"),
            text: string.Empty,
            startLineNumber: 6,
            startColumnNumber: 18,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion ConstructorSourceDetails = new(
            file: TestUtilities.NormalizePath("SymbolDetails/TypeSymbolDetails.ps1"),
            text: string.Empty,
            startLineNumber: 2,
            startColumnNumber: 11,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion MethodSourceDetails = new(
            file: TestUtilities.NormalizePath("SymbolDetails/TypeSymbolDetails.ps1"),
            text: string.Empty,
            startLineNumber: 10,
            startColumnNumber: 20,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
