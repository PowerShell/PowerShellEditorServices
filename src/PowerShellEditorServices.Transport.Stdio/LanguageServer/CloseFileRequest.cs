//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("close")]
    public class CloseFileRequest : RequestBase<FileRequestArguments>
    {
        public static CloseFileRequest Create(string filePath)
        {
            return new CloseFileRequest
            {
                Arguments = new FileRequestArguments
                {
                    File = filePath
                }
            };
        }

        public override Task ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            // Find and close the file in the current session
            var fileToClose = editorSession.Workspace.GetFile(this.Arguments.File);

            if (fileToClose != null)
            {
                editorSession.Workspace.CloseFile(fileToClose);
            }

            return TaskConstants.Completed;
        }
    }
}
