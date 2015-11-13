//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    [MessageTypeName("quickinfo")]
    public class QuickInfoRequest : FileRequest<FileLocationRequestArgs>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            SymbolReference symbolReference =
                editorSession
                    .LanguageService
                    .FindSymbolAtLocation(
                        this.GetScriptFile(editorSession),
                        this.Arguments.Line,
                        this.Arguments.Offset);

            QuickInfoResponse response =
                new QuickInfoResponse
                {
                    // TODO ###: Add Documentation and KindModifiers to QuickInfo response
                    Body = new QuickInfoResponseBody
                    {
                        DisplayString = "A symbol!",
                        Documentation = "", 
                        Kind = symbolReference.SymbolType.ToString(),
                        KindModifiers = "",
                        Start = new Location
                        {
                            Line = symbolReference.ScriptRegion.StartLineNumber,
                            Offset = symbolReference.ScriptRegion.StartColumnNumber
                        },
                        End = new Location
                        {
                            Line = symbolReference.ScriptRegion.EndLineNumber,
                            Offset = symbolReference.ScriptRegion.EndColumnNumber + 1 
                        }
                    }
                };

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    response));
        }
    }
}

