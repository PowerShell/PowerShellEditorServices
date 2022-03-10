// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal static class CompleteFilePath
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
            text: string.Empty,
            startLineNumber: 19,
            startColumnNumber: 15,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly TextEdit ExpectedEdit = new()
        {
            NewText = "",
            Range = new Range
            {
                Start = new Position { Line = 18, Character = 14 },
                End = new Position { Line = 18, Character = 14 }
            }
        };
    }
}
