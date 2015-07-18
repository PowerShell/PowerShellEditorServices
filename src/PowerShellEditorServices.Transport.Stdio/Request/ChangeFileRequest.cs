//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("change")]
    public class ChangeFileRequest : FileRequest<ChangeFileRequestArguments>
    {
        public override void ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            ScriptFile scriptFile = this.GetScriptFile(editorSession);
            scriptFile.ApplyChange(this.Arguments.GetFileChangeDetails());
        }
    }

    public class FormatRequestArguments : FileLocationRequestArgs
    {
        // TODO: This class may need to move somewhere else when used by other arg types

        public int EndLine { get; set; }

        public int EndOffset { get; set; }
    }

    public class ChangeFileRequestArguments : FormatRequestArguments
    {
        public string InsertString { get; set; }

        public FileChange GetFileChangeDetails()
        {
            return new FileChange
            {
                InsertString = this.InsertString,
                Line = this.Line,
                Offset = this.Offset,
                EndLine = this.EndLine,
                EndOffset = this.EndOffset
            };
        }
    }
}
