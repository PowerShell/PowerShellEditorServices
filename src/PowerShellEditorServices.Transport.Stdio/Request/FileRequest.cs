//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using System.IO;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    public abstract class FileRequest<TArguments> : RequestBase<TArguments>
        where TArguments : FileRequestArguments
    {
        protected ScriptFile GetScriptFile(EditorSession editorSession)
        {
            ScriptFile scriptFile = null;

            if(!editorSession.Workspace.TryGetFile(
                this.Arguments.File, 
                out scriptFile))
            {
                // TODO: Throw an exception that the message loop can create a response out of

                throw new FileNotFoundException(
                    "A ScriptFile with the following path was not found in the EditorSession: {0}",
                    this.Arguments.File);
            }

            return scriptFile;
        }
    }

    public class FileRequestArguments
    {
        public string File { get; set; }
    }

    public class FileLocationRequestArgs : FileRequestArguments
    {
        public int Line { get; set; }

        public int Offset { get; set; }
    }
}
