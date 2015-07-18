//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("completions")]
    public class CompletionsResponse : ResponseBase<CompletionEntry[]>
    {
        public static CompletionsResponse Create(CommandCompletion commandCompletion)
        {
            List<CompletionEntry> completionResult = new List<CompletionEntry>();

            foreach (var completion in commandCompletion.CompletionMatches)
            {
                completionResult.Add(
                    new CompletionEntry
                    {
                        Name = completion.CompletionText,
                        Kind = GetCompletionKind(completion.ResultType),
                    });
            }

            return new CompletionsResponse
            {
                Body = completionResult.ToArray()
            };
        }

        private static string GetCompletionKind(CompletionResultType resultType)
        {
            switch (resultType)
            {
                case CompletionResultType.Command:
                case CompletionResultType.Method:
                    return "method";
                default:
                    // TODO: Better default
                    return "variable";
            }
        }
    }
}
