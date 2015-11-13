//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("signatureHelp")]
    public class SignatureHelpResponse : ResponseBase<SignatureHelpItems>
    {
        public static SignatureHelpResponse Create(ParameterSetSignatures parameterSets)
        {
            if (parameterSets != null && parameterSets.Signatures != null)
            {
                return new SignatureHelpResponse
                {
                    Body = SignatureHelpItems.Create(parameterSets)
                };
            }
            else
            {
                return new SignatureHelpResponse
                {
                    Body = null
                };
            }
        }
    }

    public class SignatureHelpItems
    {
        public IEnumerable<SignatureHelpItem> Items { get; set; }
        public TextSpan ApplicableSpan { get; set; }
        public int SelectedItemIndex { get; set; }
        public int ArgumentIndex { get; set; }
        public int ArgumentCount { get; set; }
        public string CommandName { get; set; }

        public static SignatureHelpItems Create(ParameterSetSignatures parameterSets)
        {
            List<SignatureHelpItem> itemsList = new List<SignatureHelpItem>();
            foreach (ParameterSetSignature item in parameterSets.Signatures)
            {
                itemsList.Add(SignatureHelpItem.Create(item));
            }

            TextSpan textSpan =                 
                new TextSpan
                {
                    Start = new Location
                    {
                        Line = parameterSets.ScriptRegion.StartLineNumber,
                        Offset = parameterSets.ScriptRegion.StartColumnNumber
                    },
                    End = new Location
                    {
                        Line = parameterSets.ScriptRegion.EndLineNumber,
                        Offset = parameterSets.ScriptRegion.EndColumnNumber
                    }
                };

            return new SignatureHelpItems
            {
                ArgumentCount = parameterSets.Signatures.Length,
                ArgumentIndex = 0,
                SelectedItemIndex = 0,
                CommandName = parameterSets.CommandName,
                ApplicableSpan = textSpan,
                Items = itemsList
            };
        }
    }

    /**
     * Represents a single signature to show in signature help.
     * */
    public class SignatureHelpItem
    {
        public bool IsVariadic { get; set; }
        public IEnumerable<SymbolDisplayPart> PrefixDisplayParts { get; set; }
        public IEnumerable<SymbolDisplayPart> SuffixDisplayParts { get; set; }
        public IEnumerable<SymbolDisplayPart> SeparatorDisplayParts { get; set; }
        public IEnumerable<SignatureHelpParameter> Parameters { get; set; }
        public IEnumerable<SymbolDisplayPart> Documentation { get; set; }
        public string SignatureText { get; set; }

        public static SignatureHelpItem Create(ParameterSetSignature paramSetSignature)
        {
            List<SignatureHelpParameter> parameterList =
                new List<SignatureHelpParameter>();
            foreach (ParameterInfo paramInfo in paramSetSignature.Parameters)
            {
                parameterList.Add(SignatureHelpParameter.Create(paramInfo));
            }

            return new SignatureHelpItem
            {
                IsVariadic = false,
                PrefixDisplayParts = new List<SymbolDisplayPart>(),
                SuffixDisplayParts = new List<SymbolDisplayPart>(),
                SeparatorDisplayParts = new List<SymbolDisplayPart>(),
                Parameters = parameterList,
                Documentation = new List<SymbolDisplayPart>(),
                SignatureText = paramSetSignature.SignatureText
            };
        }
    }


    /**
     * Signature help information for a single parameter
     * */
    public class SignatureHelpParameter
    {
        public string Name { get; set; }
        public IEnumerable<SymbolDisplayPart> Documentation { get; set; }
        public IEnumerable<SymbolDisplayPart> DisplayParts { get; set; }
        public bool IsOptional { get; set; }

        public static SignatureHelpParameter Create(ParameterInfo paramInfo)
        {
            return new SignatureHelpParameter
            {
                Name = paramInfo.Name,
                Documentation = new List<SymbolDisplayPart>(),
                DisplayParts = new List<SymbolDisplayPart>(),
                IsOptional = false
            };
        }
    }
}