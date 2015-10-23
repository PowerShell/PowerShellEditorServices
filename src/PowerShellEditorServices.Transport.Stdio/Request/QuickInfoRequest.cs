using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
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
