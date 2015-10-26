using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("scopes")]
    public class ScopesRequest : RequestBase<ScopesRequestArgs>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            VariableScope[] variableScopes = 
                editorSession.DebugService.GetVariableScopes(
                    this.Arguments.FrameId);

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new ScopesResponse
                    {
                        Body = new ScopesResponseBody
                        {
                            Scopes = 
                                variableScopes
                                    .Select(Scope.Create)
                                    .ToArray()
                        }
                    }));
        }
    }

    public class ScopesRequestArgs
    {
        public int FrameId { get; set; }
    }
}
