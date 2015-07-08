using System.Collections.Generic;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    public class CompletionsResponse : ResponseBase<CompletionEntry[]>
    {
        public CompletionsResponse()
        {
            this.Command = "completions";
        }

        public CompletionsResponse(CommandCompletion commandCompletion) : this()
        {
            List<CompletionEntry> completionResult = new List<CompletionEntry>();

            foreach (var completion in commandCompletion.CompletionMatches)
            {
                completionResult.Add(
                    new CompletionEntry
                    {
                        Name = completion.CompletionText,
                        Kind = this.GetCompletionKind(completion.ResultType),
                    });
            }

            this.Body = completionResult.ToArray();
        }

        private string GetCompletionKind(CompletionResultType resultType)
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
