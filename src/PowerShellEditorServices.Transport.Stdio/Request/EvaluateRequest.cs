using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("evaluate")]
    public class EvaluateRequest : RequestBase<EvaluateRequestArguments>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            VariableDetails result =
                editorSession.DebugService.EvaluateExpression(
                    this.Arguments.Expression,
                    this.Arguments.FrameId);

            string valueString = null;
            int variableId = 0;

            if (result != null)
            {
                valueString = result.ValueString;
                variableId =
                    result.IsExpandable ?
                        result.Id : 0;
            }

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new EvaluateResponse
                    {
                        Body = new EvaluateResponseBody
                        {
                            Result = valueString,
                            VariablesReference = variableId
                        }
                    }));
        }
    }

    public class EvaluateRequestArguments
    {
        public string Expression { get; set; }

    //        /** Evaluate the expression in the context of this stack frame. If not specified, the top most frame is used. */
        public int FrameId { get; set; }
    }
}
