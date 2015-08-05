using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("source")]
    public class SourceRequest : RequestBase<SourceRequestArguments>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // TODO: What do I return here?

            return TaskConstants.Completed;
        }
    }

    public class SourceRequestArguments
    {
    //        /** The reference to the source. This is the value received in Source.reference. */
        public int SourceReference { get; set; }
    }
}
