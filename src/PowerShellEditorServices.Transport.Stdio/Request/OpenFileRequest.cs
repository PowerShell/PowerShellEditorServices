using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("open")]
    public class OpenFileRequest : RequestBase<FileRequestArguments>
    {
        public static OpenFileRequest Create(string filePath)
        {
            return new OpenFileRequest
            {
                Arguments = new FileRequestArguments
                {
                    File = filePath
                }
            };
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
