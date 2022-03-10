// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal static class CompleteTypeName
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
            text: string.Empty,
            startLineNumber: 21,
            startColumnNumber: 25,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly CompletionItem ExpectedCompletion = new()
        {
            Kind = CompletionItemKind.TypeParameter,
            Detail = "System.Collections.ArrayList",
            FilterText = "System.Collections.ArrayList",
            Label = "ArrayList",
            SortText = "0001ArrayList",
            TextEdit = new TextEdit
            {
                NewText = "System.Collections.ArrayList",
                Range = new Range
                {
                    Start = new Position { Line = 20, Character = 1 },
                    End = new Position { Line = 20, Character = 29 }
                }
            }
        };
    }
}
