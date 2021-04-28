// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal class CompleteAttributeValue
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion(
                file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
                text: string.Empty,
                startLineNumber: 16,
                startColumnNumber: 38,
                startOffset: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                endOffset: 0);

        public static readonly BufferRange ExpectedRange =
            new BufferRange(
                new BufferPosition(16, 33),
                new BufferPosition(16, 38));
    }
}

