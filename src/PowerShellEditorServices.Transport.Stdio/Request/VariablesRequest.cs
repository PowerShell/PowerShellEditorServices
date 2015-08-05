using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("variables")]
    public class VariablesRequest : RequestBase<VariablesRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            VariableDetails[] variables =
                editorSession.DebugService.GetVariables(
                    this.Arguments.VariablesReference);

            messageWriter.WriteMessage(
                this.PrepareResponse(
                    VariablesResponse.Create(variables)));

            return TaskConstants.Completed;
        }
    }

    public class VariablesRequestArguments
    {
        public int VariablesReference { get; set; }
    }
}
