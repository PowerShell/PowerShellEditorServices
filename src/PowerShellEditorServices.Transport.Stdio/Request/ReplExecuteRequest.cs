//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("replExecute")]
    public class ReplExecuteRequest : RequestBase<ReplExecuteArgs>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            editorSession.PowerShellSession.ExecuteScript(
                this.Arguments.CommandString);

            return TaskConstants.Completed;
        }
    }

    public class ReplExecuteArgs
    {
        public string CommandString { get; set; }
    }
}
