// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Definition
{
    public static class FindsFunctionDefinitionOfAliasData
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("References/SimpleFile.ps1"),
            text: string.Empty,
            startLineNumber: 20,
            startColumnNumber: 4,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
