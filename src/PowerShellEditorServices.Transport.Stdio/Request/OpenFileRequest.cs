using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    public class OpenFileRequest : RequestBase<FileRequestArguments>
    {
        public OpenFileRequest()
        {
            this.Command = "open";
            this.Arguments = new FileRequestArguments();
        }

        public OpenFileRequest(string filePath) : this()
        {
            this.Arguments.File = filePath;
        }

        public override void ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            // Open the file in the current session
            editorSession.OpenFile(this.Arguments.File);
        }
    }
}
