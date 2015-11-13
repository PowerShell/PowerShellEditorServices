//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Nito.AsyncEx;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    //    /** SetExceptionBreakpoints request; value of command field is "setExceptionBreakpoints".
    //        Enable that the debuggee stops on exceptions with a StoppedEvent (event type 'exception').
    //    */
    [MessageTypeName("setExceptionBreakpoints")]
    public class SetExceptionBreakpointsRequest : RequestBase<SetExceptionBreakpointsRequestArguments>
    {
        public override async Task ProcessMessage(
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            // TODO: Add these

            await messageWriter.WriteMessage(
                this.PrepareResponse(
                    new SetExceptionBreakpointsResponse()));
        }
    }

    public class SetExceptionBreakpointsRequestArguments
    {
    //    /** Arguments for "setExceptionBreakpoints" request. */
    //    export interface SetExceptionBreakpointsArguments {
    //        /** Names of enabled exception breakpoints. */
    //        filters: string[];
    //    }
        public string[] Filters { get; set; }
    }
}

