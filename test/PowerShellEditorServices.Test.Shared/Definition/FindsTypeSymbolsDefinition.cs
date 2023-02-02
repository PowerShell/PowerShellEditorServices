// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Definition
{
    public static class FindsTypeSymbolsDefinitionData
    {
        public static readonly ScriptRegion ClassSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 8,
            startColumnNumber: 14,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion EnumSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 39,
            startColumnNumber: 10,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion TypeExpressionSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 45,
            startColumnNumber: 5,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion TypeConstraintSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 25,
            startColumnNumber: 24,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion ConstructorSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 9,
            startColumnNumber: 14,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion MethodSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 19,
            startColumnNumber: 25,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion PropertySourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 15,
            startColumnNumber: 32,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly ScriptRegion EnumMemberSourceDetails = new(
            file: TestUtilities.NormalizePath("References/TypeAndClassesFile.ps1"),
            text: string.Empty,
            startLineNumber: 41,
            startColumnNumber: 11,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
