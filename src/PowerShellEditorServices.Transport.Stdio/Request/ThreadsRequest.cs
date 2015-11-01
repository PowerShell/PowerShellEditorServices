using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("threads")]
    public class ThreadsRequest : RequestBase<object>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new ThreadsResponse
                    {
                        Body = new ThreadsResponseBody
                        {
                            Threads = new Thread[]
                            {
                                // TODO: What do I do with these?
                                new Thread
                                {
                                    Id = 1,
                                    Name = "Main Thread"
                                }
                            }
                        }
                    }));
        }
    }
}
