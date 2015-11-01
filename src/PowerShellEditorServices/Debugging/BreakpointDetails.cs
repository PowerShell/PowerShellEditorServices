//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices
{
    public class BreakpointDetails
    {
        public int LineNumber { get; set; }

        public static BreakpointDetails Create(Breakpoint breakpoint)
        {
            Validate.IsNotNull("breakpoint", breakpoint);

            LineBreakpoint lineBreakpoint = breakpoint as LineBreakpoint;
            if (lineBreakpoint != null)
            {
                return new BreakpointDetails
                {
                    LineNumber = lineBreakpoint.Line
                };
            }
            else
            {
                throw new ArgumentException(
                    "Expected breakpoint type:" + breakpoint.GetType().Name);
            }
        }
    }
}
