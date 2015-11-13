//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("completions")]
    public class CompletionsResponse : ResponseBase<CompletionEntry[]>
    {
        public static CompletionsResponse Create(CompletionResults completionResults)
        {
            List<CompletionEntry> completionResult = new List<CompletionEntry>();

            foreach (var completion in completionResults.Completions)
            {
                completionResult.Add(
                    new CompletionEntry
                    {
                        Name = completion.CompletionText,
                        Kind = GetCompletionKind(completion.CompletionType),
                    });
            }

            return new CompletionsResponse
            {
                Body = completionResult.ToArray()
            };
        }

        private static string GetCompletionKind(CompletionType completionType)
        {
            switch (completionType)
            {
                case CompletionType.Command:
                case CompletionType.Method:
                    return "method";
                default:
                    // TODO: Better default
                    return "variable";
            }
        }
    }
    public class CompletionEntry
    {
        public string Name { get; set; }

        public string Kind { get; set; }

        public string KindModifiers { get; set; }

        public string SortText { get; set; }
    }

}
