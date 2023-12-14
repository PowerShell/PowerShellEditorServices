// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal static class CompleteVariableInFile
    {
        public static readonly ScriptRegion SourceDetails = new(
            file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
            text: string.Empty,
            startLineNumber: 10,
            startColumnNumber: 9,
            startOffset: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            endOffset: 0);

        public static readonly CompletionItem ExpectedCompletion = new()
        {
            Kind = CompletionItemKind.Variable,
            // PowerShell 7.4 now lights up a type for the detail, otherwise it's the same as the
            // label and therefore hidden.
            Detail = Utility.VersionUtils.IsPS74 ? "[string]" : "",
            FilterText = "$testVar1",
            InsertText = "$testVar1",
            Label = "testVar1",
            SortText = "0001testVar1",
            TextEdit = new TextEdit
            {
                NewText = "$testVar1",
                Range = new Range
                {
                    Start = new Position { Line = 9, Character = 0 },
                    End = new Position { Line = 9, Character = 8 }
                }
            }
        };
    }
}
