//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    public class CompleteCommandFromModule
    {
        private static readonly string[] s_getRandomParamSets = {
            "Get-Random [[-Maximum] <Object>] [-SetSeed <int>] [-Minimum <Object>] [<CommonParameters>]",
            "Get-Random [-InputObject] <Object[]> [-SetSeed <int>] [-Count <int>] [<CommonParameters>]"
        };

        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
                StartLineNumber = 13,
                StartColumnNumber = 8
            };

        public static readonly CompletionDetails ExpectedCompletion =
            CompletionDetails.Create(
                "Get-Random",
                CompletionType.Command,
                string.Join(Environment.NewLine + Environment.NewLine, s_getRandomParamSets)
            );
    }
}
