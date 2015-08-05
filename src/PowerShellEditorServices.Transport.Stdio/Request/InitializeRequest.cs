using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("initialize")]
    public class InitializeRequest : RequestBase<InitializeRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // Send the Initialized event first so that we get breakpoints
            messageWriter.WriteMessage(
                new InitializedEvent());

            // Now send the Initialize response to continue setup
            messageWriter.WriteMessage(
                this.PrepareResponse(
                    new InitializeResponse()));

            return TaskConstants.Completed;
        }
    }

    public class InitializeRequestArguments
    {
        public string AdapterId { get; set; }

        public bool LinesStartAt1 { get; set; }

        public string PathFormat { get; set; }

        public bool SourceMaps { get; set; }

        public string GeneratedCodeDirectory { get; set; }
    }
}