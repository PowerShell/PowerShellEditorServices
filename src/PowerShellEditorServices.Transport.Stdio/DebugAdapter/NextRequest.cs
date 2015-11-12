//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    //    /** StepOver request; value of command field is "next".
    //        he request starts the debuggee to run again for one step.
    //        penDebug will respond with a StoppedEvent (event type 'step') after running the step.
    [MessageTypeName("next")]
    public class NextRequest : RequestBase<object>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            editorSession.DebugService.StepOver();

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new NextResponse()));
        }
    }
}

