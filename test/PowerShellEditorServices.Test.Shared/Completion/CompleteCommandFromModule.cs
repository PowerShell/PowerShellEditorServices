//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal class CompleteCommandFromModule
    {
        private static readonly string[] s_getRandomParamSets = {
            "Get-Random [[-Maximum] <Object>] [-SetSeed <int>] [-Minimum <Object>] [<CommonParameters>]",
            "Get-Random [-InputObject] <Object[]> [-SetSeed <int>] [-Count <int>] [<CommonParameters>]"
        };

        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion(
                file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
                text: string.Empty,
                startLineNumber: 13,
                startColumnNumber: 8,
                startOffset: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                endOffset: 0);

        public static readonly CompletionDetails ExpectedCompletion =
            CompletionDetails.Create(
                "Get-Random",
                CompletionType.Command,
                string.Join(Environment.NewLine + Environment.NewLine, s_getRandomParamSets)
            );
    }
}
