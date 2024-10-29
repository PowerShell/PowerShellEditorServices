// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal static class CompleteCommandInFile
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
            text: string.Empty,
            startLineNumber: 8,
            startColumnNumber: 10,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly CompletionItem ExpectedCompletion = new()
        {
            Kind = CompletionItemKind.Function,
            Detail = "",
            FilterText = "Get-XYZSomething",
            InsertText = "Get-XYZSomething",
            Label = "Get-XYZSomething",
            SortText = "0001Get-XYZSomething",
            TextEdit = new TextEdit
            {
                NewText = "Get-XYZSomething",
                Range = new Range
                {
                    Start = new Position { Line = 7, Character = 0 },
                    End = new Position { Line = 7, Character = 9 }
                }
            }
        };
    }
}
