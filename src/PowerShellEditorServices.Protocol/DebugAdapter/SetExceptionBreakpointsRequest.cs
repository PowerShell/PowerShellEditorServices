//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    /// <summary>
    /// SetExceptionBreakpoints request; value of command field is "setExceptionBreakpoints".
    /// Enable that the debuggee stops on exceptions with a StoppedEvent (event type 'exception').
    /// </summary>
    public class SetExceptionBreakpointsRequest
    {
        public static readonly
            RequestType<SetExceptionBreakpointsRequestArguments, object> Type =
            RequestType<SetExceptionBreakpointsRequestArguments, object>.Create("setExceptionBreakpoints");
    }

    /// <summary>
    /// Arguments for "setExceptionBreakpoints" request.
    /// </summary>
    public class SetExceptionBreakpointsRequestArguments
    {
        /// <summary>
        /// Gets or sets the names of enabled exception breakpoints.
        /// </summary>
        public string[] Filters { get; set; }
    }
}
