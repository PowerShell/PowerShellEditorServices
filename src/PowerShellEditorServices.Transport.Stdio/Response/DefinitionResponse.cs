//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("definition")]
    public class DefinitionResponse : ResponseBase<FileSpan[]>
    {
        public static DefinitionResponse Create(SymbolReference result, string thisFile)
        {
            if (result != null)
            {
                //The protocol expects a filespan when there whould only be one definition
                List<FileSpan> declarResult = new List<FileSpan>();
                declarResult.Add(
                        new FileSpan()
                        {
                            Start = new Location
                            {
                                Line = result.ScriptRegion.StartLineNumber,
                                Offset = result.ScriptRegion.StartColumnNumber
                            },
                            End = new Location
                            {
                                Line = result.ScriptRegion.EndLineNumber,
                                Offset = result.ScriptRegion.EndColumnNumber
                            },
                            File = thisFile,
                        });
                return new DefinitionResponse
                {
                    Body = declarResult.ToArray()
                };   
            }
            else 
            {
                return new DefinitionResponse
                {
                    Body = null
                };            
            }
        }
    }
}
