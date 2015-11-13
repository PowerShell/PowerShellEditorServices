//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("stackTrace")]
    public class StackTraceRequest : RequestBase<StackTraceRequestArguments>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            StackFrameDetails[] stackFrames =
                editorSession.DebugService.GetStackFrames();

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    StackTraceResponse.Create(stackFrames)));
        }
    }

    public class StackTraceRequestArguments
    {
        public int ThreadId { get; private set; }

    //        /** The maximum number of frames to return. If levels is not specified or 0, all frames are returned. */
        public int Levels { get; private set; }
    }
}

