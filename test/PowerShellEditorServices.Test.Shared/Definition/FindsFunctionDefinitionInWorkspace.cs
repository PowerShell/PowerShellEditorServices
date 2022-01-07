// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Definition
{
    public static class FindsFunctionDefinitionInWorkspaceData
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("References/ReferenceFileD.ps1"),
            text: string.Empty,
            startLineNumber: 1,
            startColumnNumber: 2,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
