using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    //    /** SetExceptionBreakpoints request; value of command field is "setExceptionBreakpoints".
    //        Enable that the debuggee stops on exceptions with a StoppedEvent (event type 'exception').
    //    */
    [MessageTypeName("setExceptionBreakpoints")]
    public class SetExceptionBreakpointsRequest : RequestBase<SetExceptionBreakpointsRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // TODO: Add these

            messageWriter.WriteMessage(
                this.PrepareResponse(
                    new SetExceptionBreakpointsResponse()));

            return TaskConstants.Completed;
        }
    }

    public class SetExceptionBreakpointsRequestArguments
    {
    //    /** Arguments for "setExceptionBreakpoints" request. */
    //    export interface SetExceptionBreakpointsArguments {
    //        /** Names of enabled exception breakpoints. */
    //        filters: string[];
    //    }
        public string[] Filters { get; set; }
    }
}
