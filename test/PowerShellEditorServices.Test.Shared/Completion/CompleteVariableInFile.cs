//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal class CompleteVariableInFile
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion(
                    file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
                    text: string.Empty,
                    startLineNumber: 10,
                    startColumnNumber: 9,
                    startOffset: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    endOffset: 0);

        public static readonly CompletionDetails ExpectedCompletion =
            CompletionDetails.Create(
                "$testVar1",
                CompletionType.Variable,
                "testVar1");
    }
}
