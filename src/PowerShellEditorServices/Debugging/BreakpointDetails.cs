//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides details about a breakpoint that is set in the
    /// PowerShell debugger.
    /// </summary>
    public class BreakpointDetails
    {
        /// <summary>
        /// Gets the line number at which the breakpoint is set.
        /// </summary>
        public int LineNumber { get; private set; }

        /// <summary>
        /// Creates an instance of the BreakpointDetails class from a
        /// PowerShell Breakpoint object.
        /// </summary>
        /// <param name="breakpoint">The Breakpoint instance from which details will be taken.</param>
        /// <returns>A new instance of the BreakpointDetails class.</returns>
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
