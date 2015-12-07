//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    public class CompleteCommandInFile
    {
        public static readonly ScriptRegion SourceDetails = 
            new ScriptRegion
            {
                File = @"Completion\CompletionExamples.psm1",
                StartLineNumber = 8,
                StartColumnNumber = 7
            };

        public static readonly CompletionDetails ExpectedCompletion =
            CompletionDetails.Create(
                "Get-Something",
                CompletionType.Command,
                "Get-Something");
    }
}

