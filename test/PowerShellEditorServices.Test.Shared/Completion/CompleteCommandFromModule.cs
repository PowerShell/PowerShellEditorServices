// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    public class CompleteCommandFromModule
    {
        public static readonly ScriptRegion SourceDetails = 
            new ScriptRegion
            {
                File = @"Completion\CompletionExamples.ps1",
                StartLineNumber = 13,
                StartColumnNumber = 11
            };

        public static readonly CompletionDetails ExpectedCompletion =
            CompletionDetails.Create(
                "Install-Module",
                CompletionType.Command,
                "Install-Module");
    }
}
