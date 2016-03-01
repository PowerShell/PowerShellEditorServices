//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class SetFunctionBreakpointsRequest
    {
        public static readonly
            RequestType<SetFunctionBreakpointsRequestArguments, SetBreakpointsResponseBody> Type =
            RequestType<SetFunctionBreakpointsRequestArguments, SetBreakpointsResponseBody>.Create("setFunctionBreakpoints");
    }

    public class SetFunctionBreakpointsRequestArguments
    {
        public FunctionBreakpoint[] Breakpoints { get; set; }
    }

    public class FunctionBreakpoint
    {
        /// <summary>
        /// Gets or sets the name of the function to break on when it is invoked.
        /// </summary>
        public string Name { get; set; }

        public string Condition { get; set; }
    }
}
