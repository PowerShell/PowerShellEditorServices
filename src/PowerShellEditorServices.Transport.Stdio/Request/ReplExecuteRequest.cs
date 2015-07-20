//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("replExecute")]
    public class ReplExecuteRequest : RequestBase<ReplExecuteArgs>
    {
        public override void ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            editorSession.ConsoleService.ExecuteCommand(
                this.Arguments.CommandString);
        }
    }

    public class ReplExecuteArgs
    {
        public string CommandString { get; set; }
    }
}
