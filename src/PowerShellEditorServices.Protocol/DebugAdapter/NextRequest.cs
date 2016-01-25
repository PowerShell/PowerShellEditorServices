//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    //    /** StepOver request; value of command field is "next".
    //        he request starts the debuggee to run again for one step.
    //        penDebug will respond with a StoppedEvent (event type 'step') after running the step.
    public class NextRequest
    {
        public static readonly
            RequestType<object, object> Type =
            RequestType<object, object>.Create("next");
    }
}

