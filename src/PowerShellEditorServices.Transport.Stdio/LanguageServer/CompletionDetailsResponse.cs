//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Text.RegularExpressions;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("completionEntryDetails")]
    public class CompletionDetailsResponse : ResponseBase<CompletionEntryDetails[]>
    {
    }

    public class CompletionEntryDetails
    {
        public CompletionEntryDetails(CompletionDetails completionResult, string entryName)
        {

            Kind = null;
            KindModifiers = null;
            DisplayParts = null;
            Documentation = null;
            DocString = null;

            // if the  result type is a command return null
            if (completionResult != null &&
                !(completionResult.CompletionType.Equals(CompletionType.Command)))
            {
                //find matches on square brackets in the the tool tip
                var matches = 
                    Regex.Matches(completionResult.ToolTipText, @"^\[(.+)\]");
                string strippedEntryName = 
                    Regex.Replace(entryName, @"^[$_-]", "").Replace("{", "").Replace("}", "");

                if (matches.Count > 0 && matches[0].Groups.Count > 1)
                {
                    Name = matches[0].Groups[1].Value;
                }
                // if there are nobracets and the only content is the completion name
                else if (completionResult.ToolTipText.Equals(strippedEntryName))
                {
                    Name = null;
                }
                else
                {
                    Name = null;
                    DocString = completionResult.ToolTipText;
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
