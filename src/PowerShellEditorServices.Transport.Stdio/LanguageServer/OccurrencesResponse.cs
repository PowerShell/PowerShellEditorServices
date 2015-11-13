//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("occurrences")]
    public class OccurrencesResponse : ResponseBase<OccurrencesResponseItem[]>
    {
        public static OccurrencesResponse Create(FindOccurrencesResult occurrencesResult, string thisFile)
        {
            if (occurrencesResult != null)
            {
                List<OccurrencesResponseItem> occurrenceItems =
                    new List<OccurrencesResponseItem>();

                foreach (SymbolReference reference in occurrencesResult.FoundOccurrences)
                {
                    occurrenceItems.Add(
                        new OccurrencesResponseItem()
                        {
                            IsWriteAccess = true,
                            File = thisFile,
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
                        });
                }
                return new OccurrencesResponse
                {
                    Body = occurrenceItems.ToArray()
                };              
            }
            else
            {
                return new OccurrencesResponse
                {
                    Body = null
                };
            }
        }
    }

    public class OccurrencesResponseItem : FileSpan
    {
        public bool IsWriteAccess { get; set; }
    }
}
