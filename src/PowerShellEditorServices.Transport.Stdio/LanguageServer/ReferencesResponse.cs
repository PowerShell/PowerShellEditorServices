//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("references")]
    public class ReferencesResponse : ResponseBase<ReferencesResponseBody>
    {
        public static ReferencesResponse Create(FindReferencesResult referencesResult, string thisFile)
        {
            if (referencesResult != null && referencesResult.FoundReferences != null)
            {
                List<ReferencesResponseItem> referenceItems 
                    = new List<ReferencesResponseItem>();

                foreach (SymbolReference reference in referencesResult.FoundReferences)
                {
                    referenceItems.Add(
                        new ReferencesResponseItem()
                        {
                            Start = new Location
                            {
                                Line = reference.ScriptRegion.StartLineNumber,
                                Offset = reference.ScriptRegion.StartColumnNumber
                            },
                            End = new Location
                            {
                                Line = reference.ScriptRegion.EndLineNumber,
                                Offset = reference.ScriptRegion.EndColumnNumber
                            },
                            IsWriteAccess = true,
                            File = reference.FilePath,
                            LineText = reference.SourceLine
                        });
                }
                return new ReferencesResponse
                {
                    Body = new ReferencesResponseBody
                    {
                        Refs = referenceItems.ToArray(),
                        SymbolName = referencesResult.SymbolName,
                        SymbolDisplayString = referencesResult.SymbolName,
                        SymbolStartOffest = referencesResult.SymbolFileOffset
                    }
                };
            }
            else
            {
                return new ReferencesResponse
                {
                    Body = null
                };
            }
        }
    }

    public class ReferencesResponseBody
    {
        public ReferencesResponseItem[] Refs { get; set; }
        public string SymbolName { get; set; }
        public int SymbolStartOffest { get; set; }
        public string SymbolDisplayString { get; set; }
    }

    public class ReferencesResponseItem : FileSpan
    {
        public string LineText { get; set; }
        public bool IsWriteAccess { get; set; }
    }
}
