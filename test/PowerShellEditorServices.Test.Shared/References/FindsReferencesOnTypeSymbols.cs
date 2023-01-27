// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.References
{
    public static class FindsReferencesOnTypeSymbolsData
    {
        public static readonly ScriptRegion ClassSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 8,
            startColumnNumber: 12,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion EnumSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 39,
            startColumnNumber: 8,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion ConstructorSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 9,
            startColumnNumber: 8,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion MethodSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 36,
            startColumnNumber: 16,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion PropertySourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 17,
            startColumnNumber: 15,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion EnumMemberSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 41,
            startColumnNumber: 8,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion TypeExpressionSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 34,
            startColumnNumber: 12,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion TypeConstraintSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 25,
            startColumnNumber: 22,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
