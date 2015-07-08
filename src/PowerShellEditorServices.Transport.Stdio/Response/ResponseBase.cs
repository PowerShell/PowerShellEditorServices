//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Newtonsoft.Json;
using System.Management.Automation;
using System.Text.RegularExpressions;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    public abstract class ResponseBase<TBody> : MessageBase
    {
        [JsonProperty("request_seq")]
        public int RequestSeq { get; set; }

        public bool Success { get; set; }

        public string Command { get; set; }

        public string Message { get; set; }

        public TBody Body { get; set; }

        internal override string PayloadType
        {
            get { return this.Command; }
        }

        public ResponseBase()
        {
            this.Type = "response";
        }
    }

    public class CompletionEntry
    {
        public string Name { get; set; }

        public string Kind { get; set; }

        public string KindModifiers { get; set; }

        public string SortText { get; set; }
    }

    public class CompletionEntryDetails
    {
        public CompletionEntryDetails(CompletionResult completionResult, string entryName)
        {

            Kind = null;
            KindModifiers = null;
            DisplayParts = null;
            Documentation = null;
            DocString = null;

            // if the  result type is a command return null 
            if (!(completionResult.ResultType.Equals(CompletionResultType.Command)))
            {
                //find matches on square brackets in the the tool tip
                var matches = Regex.Matches(completionResult.ToolTip, @"^\[(.+)\]");
                string strippedEntryName = Regex.Replace(entryName, @"^[$_-]","").Replace("{","").Replace("}","");

                if (matches.Count > 0 && matches[0].Groups.Count > 1)
                {                        
                    Name = matches[0].Groups[1].Value;
                }
                // if there are nobracets and the only content is the completion name
                else if (completionResult.ToolTip.Equals(strippedEntryName))
                {
                    Name = null;
                }
                else
                {
                    Name = null;
                    DocString = completionResult.ToolTip;
                }
            }

            else { Name = null; }
        }
        public string Name { get; set; }

        public string Kind { get; set; }

        public string KindModifiers { get; set; }

        public SymbolDisplayPart[] DisplayParts { get; set; }

        public SymbolDisplayPart[] Documentation { get; set; }

        public string DocString { get; set; }

    }

    public class SymbolDisplayPart
    {
        public string Text { get; set; }

        public string Kind { get; set; }
    }
}
