// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal static class CompleteCommandFromModule
    {
        public const string GetRandomDetail =
            "Get-Random [[-Maximum] <Object>] [-SetSeed <int>] [-Minimum <Object>]";

        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
            text: string.Empty,
            startLineNumber: 13,
            startColumnNumber: 8,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly CompletionItem ExpectedCompletion = new()
        {
            Kind = CompletionItemKind.Function,
            Detail = "", // OS-dependent, checked separately.
            FilterText = "Get-Random",
            Label = "Get-Random",
            SortText = "0001Get-Random",
            TextEdit = new TextEdit
            {
                NewText = "Get-Random",
                Range = new Range
                {
                    Start = new Position { Line = 12, Character = 0 },
                    End = new Position { Line = 12, Character = 8 }
                }
            }
        };
    }
}
