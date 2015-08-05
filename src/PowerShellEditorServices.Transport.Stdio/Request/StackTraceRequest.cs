using Microsoft.PowerShell.EditorServices.Console;
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
    [MessageTypeName("stackTrace")]
    public class StackTraceRequest : RequestBase<StackTraceRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            StackFrameDetails[] stackFrames =
                editorSession.DebugService.GetStackFrames();

            messageWriter.WriteMessage(
                this.PrepareResponse(
                    StackTraceResponse.Create(stackFrames)));

            return TaskConstants.Completed;
        }
    }

    public class StackTraceRequestArguments
    {
        public int ThreadId { get; private set; }

    //        /** The maximum number of frames to return. If levels is not specified or 0, all frames are returned. */
        public int Levels { get; private set; }
    }
}
