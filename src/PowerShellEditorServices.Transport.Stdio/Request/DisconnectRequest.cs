using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("disconnect")]
    public class DisconnectRequest : RequestBase<object>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            EventHandler<SessionStateChangedEventArgs> handler = null;

            handler =
                async (o, e) =>
                    {
                        if (e.NewSessionState == PowerShellSessionState.Ready)
                        {
                            await messageWriter.WriteMessage(new DisconnectResponse {});
                            editorSession.PowerShellSession.SessionStateChanged -= handler;

                            // TODO: Find a way to exit more gracefully!
                            Environment.Exit(0);
                        }
                    };

            editorSession.PowerShellSession.SessionStateChanged += handler;
            editorSession.PowerShellSession.AbortExecution();

            return TaskConstants.Completed;
        }
    }
}
