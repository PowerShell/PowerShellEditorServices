//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    public abstract class FileRequest<TArguments> : RequestBase<TArguments>
        where TArguments : FileRequestArguments
    {
        protected ScriptFile GetScriptFile(EditorSession editorSession)
        {
            return 
                editorSession.Workspace.GetFile(
                    this.Arguments.File);
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
