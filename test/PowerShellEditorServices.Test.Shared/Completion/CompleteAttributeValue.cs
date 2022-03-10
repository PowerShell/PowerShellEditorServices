// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal static class CompleteAttributeValue
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
            text: string.Empty,
            startLineNumber: 16,
            startColumnNumber: 38,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly CompletionItem ExpectedCompletion1 = new()
        {
            Kind = CompletionItemKind.Property,
            Detail = "System.Boolean ValueFromPipeline",
            FilterText = "ValueFromPipeline",
            Label = "ValueFromPipeline",
            SortText = "0001ValueFromPipeline",
            TextEdit = new TextEdit
            {
                NewText = "ValueFromPipeline",
                Range = new Range
                {
                    Start = new Position { Line = 15, Character = 32 },
                    End = new Position { Line = 15, Character = 37 }
                }
            }
        };

        public static readonly CompletionItem ExpectedCompletion2 = new()
        {
            Kind = CompletionItemKind.Property,
            Detail = "System.Boolean ValueFromPipelineByPropertyName",
            FilterText = "ValueFromPipelineByPropertyName",
            Label = "ValueFromPipelineByPropertyName",
            SortText = "0002ValueFromPipelineByPropertyName",
            TextEdit = new TextEdit
            {
                NewText = "ValueFromPipelineByPropertyName",
                Range = new Range
                {
                    Start = new Position { Line = 15, Character = 32 },
                    End = new Position { Line = 15, Character = 37 }
                }
            }
        };

        public static readonly CompletionItem ExpectedCompletion3 = new()
        {
            Kind = CompletionItemKind.Property,
            Detail = "System.Boolean ValueFromRemainingArguments",
            FilterText = "ValueFromRemainingArguments",
            Label = "ValueFromRemainingArguments",
            SortText = "0003ValueFromRemainingArguments",
            TextEdit = new TextEdit
            {
                NewText = "ValueFromRemainingArguments",
                Range = new Range
                {
                    Start = new Position { Line = 15, Character = 32 },
                    End = new Position { Line = 15, Character = 37 }
                }
            }
        };
    }
}
