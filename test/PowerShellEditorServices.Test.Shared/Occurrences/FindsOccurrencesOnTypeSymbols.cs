// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences
{
    public static class FindsOccurrencesOnTypeSymbolsData
    {
        public static readonly ScriptRegion ClassSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 8,
            startColumnNumber: 16,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion EnumSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 39,
            startColumnNumber: 7,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion TypeExpressionSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 34,
            startColumnNumber: 16,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion TypeConstraintSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 8,
            startColumnNumber: 24,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion ConstructorSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 13,
            startColumnNumber: 14,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion MethodSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 28,
            startColumnNumber: 22,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion PropertySourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 15,
            startColumnNumber: 18,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion EnumMemberSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 45,
            startColumnNumber: 16,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
