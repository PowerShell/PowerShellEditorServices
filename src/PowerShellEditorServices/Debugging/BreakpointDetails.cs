//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides details about a breakpoint that is set in the
    /// PowerShell debugger.
    /// </summary>
    public class BreakpointDetails
    {
        /// <summary>
        /// Gets or sets a boolean indicator that if true, breakpoint could be set 
        /// (but not necessarily at the desired location).  
        /// </summary>
        public bool Verified { get; set; }

        /// <summary>
        /// Gets or set an optional message about the state of the breakpoint. This is shown to the user 
        /// and can be used to explain why a breakpoint could not be verified.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the source where the breakpoint is located.  Used only for debug purposes.
        /// </summary>
        public string Source { get; private set; }

        /// <summary>
        /// Gets the line number at which the breakpoint is set.
        /// </summary>
        public int LineNumber { get; private set; }

        /// <summary>
        /// Gets the column number at which the breakpoint is set. If null, the default of 1 is used.
        /// </summary>
        public int? ColumnNumber { get; private set; }

        /// <summary>
        /// Gets the breakpoint condition string.
        /// </summary>
        public string Condition { get; private set; }

        private BreakpointDetails()
        {
        }

        /// <summary>
        /// Creates an instance of the BreakpointDetails class from the individual
        /// pieces of breakpoint information provided by the client.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="line"></param>
        /// <param name="column"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static BreakpointDetails Create(
            string source, 
            int line, 
            int? column = null, 
            string condition = null)
        {
            Validate.IsNotNull("source", source);

            return new BreakpointDetails
            {
                Source = source,
                LineNumber = line,
                ColumnNumber = column,
                Condition = condition
            };
        }

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
            if (lineBreakpoint == null)
            {
                throw new ArgumentException(
                    "Expected breakpoint type:" + breakpoint.GetType().Name);
            }

            var breakpointDetails = new BreakpointDetails
            {
                Verified = true,
                Source = lineBreakpoint.Script,
                LineNumber = lineBreakpoint.Line,
                Condition = lineBreakpoint.Action?.ToString()
            };

            if (lineBreakpoint.Column > 0)
            {
                breakpointDetails.ColumnNumber = lineBreakpoint.Column;
            }

            return breakpointDetails;
        }
    }
}
