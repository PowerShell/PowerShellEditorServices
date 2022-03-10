// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal static class CompleteNamespace
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
            text: string.Empty,
            startLineNumber: 22,
            startColumnNumber: 15,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly CompletionItem ExpectedCompletion = new()
        {
            Kind = CompletionItemKind.Module,
            Detail = "Namespace System.Collections",
            FilterText = "System.Collections",
            Label = "Collections",
            SortText = "0001Collections",
            TextEdit = new TextEdit
            {
                NewText = "System.Collections",
                Range = new Range
                {
                    Start = new Position { Line = 21, Character = 1 },
                    End = new Position { Line = 21, Character = 15 }
                }
            }
        };
    }
}
