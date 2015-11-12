//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("setBreakpoints")]
    public class SetBreakpointsResponse : ResponseBase<SetBreakpointsResponseBody>
    {
        private SetBreakpointsResponse()
        {
        }

        public static SetBreakpointsResponse Create(
            BreakpointDetails[] breakpoints)
        {
            return new SetBreakpointsResponse
            {
                Body = new SetBreakpointsResponseBody
                {
                    Breakpoints =
                        breakpoints
                            .Select(Breakpoint.Create)
                            .ToArray()
                }
            };
        }
    }

    public class SetBreakpointsResponseBody
    {
        public Breakpoint[] Breakpoints { get; set; }
    }
}

