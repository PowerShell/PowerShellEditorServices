// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint
{
    public static class FindsParameterSetsOnCommandWithSpacesData
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("ParameterHints/ParamHints.ps1"),
            text: string.Empty,
            startLineNumber: 9,
            startColumnNumber: 31,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);
    }
}
