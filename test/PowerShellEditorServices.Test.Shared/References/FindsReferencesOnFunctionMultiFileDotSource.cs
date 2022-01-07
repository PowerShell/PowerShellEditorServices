// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.References
{
    public static class FindsReferencesOnFunctionMultiFileDotSourceFileB
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("References/ReferenceFileB.ps1"),
            text: string.Empty,
            startLineNumber: 5,
            startColumnNumber: 8,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }

    public static class FindsReferencesOnFunctionMultiFileDotSourceFileC
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("References/ReferenceFileC.ps1"),
            text: string.Empty,
            startLineNumber: 4,
            startColumnNumber: 10,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
