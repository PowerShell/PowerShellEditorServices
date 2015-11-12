//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    [MessageTypeName("pause")]
    public class PauseRequest : RequestBase<object>
    {
        public override Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            editorSession.DebugService.Break();

            return TaskConstants.Completed;
        }
    }
}

