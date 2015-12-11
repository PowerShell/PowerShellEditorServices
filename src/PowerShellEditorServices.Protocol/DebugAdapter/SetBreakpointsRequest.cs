//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    //    /** SetBreakpoints request; value of command field is "setBreakpoints".
    //        Sets multiple breakpoints for a single source and clears all previous breakpoints in that source.
    //        To clear all breakpoint for a source, specify an empty array.
    //        When a breakpoint is hit, a StoppedEvent (event type 'breakpoint') is generated.
    //    */
    public class SetBreakpointsRequest
    {
        public static readonly
            RequestType<SetBreakpointsRequestArguments, SetBreakpointsResponseBody> Type =
            RequestType<SetBreakpointsRequestArguments, SetBreakpointsResponseBody>.Create("setBreakpoints");
    }

    public class SetBreakpointsRequestArguments
    {
        public Source Source { get; set; }

        public int[] Lines { get; set; }
    }

    public class SetBreakpointsResponseBody
    {
        public Breakpoint[] Breakpoints { get; set; }
    }
}

