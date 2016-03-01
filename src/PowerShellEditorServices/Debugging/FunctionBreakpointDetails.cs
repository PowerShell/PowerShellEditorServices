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
    /// Provides details about a function breakpoint that is set in the
    /// PowerShell debugger.
    /// </summary>
    public class FunctionBreakpointDetails : BreakpointDetailsBase
    {
        /// <summary>
        /// Gets the name of the function or command name for a function breakpoint.
        /// </summary>
        public string Name { get; private set; }

        private FunctionBreakpointDetails()
        {
        }

        /// <summary>
        /// Creates an instance of the BreakpointDetails class from the individual
        /// pieces of breakpoint information provided by the client.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="line"></param>
        /// <param name="column"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static FunctionBreakpointDetails Create(
            string name,
            string condition = null)
        {
            Validate.IsNotNull(nameof(name), name);

            return new FunctionBreakpointDetails {
                Name = name,
                Condition = condition
            };
        }

        /// <summary>
        /// Creates an instance of the BreakpointDetails class from a
        /// PowerShell Breakpoint object.
        /// </summary>
        /// <param name="breakpoint">The Breakpoint instance from which details will be taken.</param>
        /// <returns>A new instance of the BreakpointDetails class.</returns>
        public static FunctionBreakpointDetails Create(Breakpoint breakpoint)
        {
            Validate.IsNotNull("breakpoint", breakpoint);

            CommandBreakpoint commandBreakpoint = breakpoint as CommandBreakpoint;
            if (commandBreakpoint == null)
            {
                throw new ArgumentException(
                    "Unexpected breakpoint type: " + breakpoint.GetType().Name);
            }

            var breakpointDetails = new FunctionBreakpointDetails {
                Verified = true,
                Name = commandBreakpoint.Command,
                Condition = commandBreakpoint.Action?.ToString()
            };

            return breakpointDetails;
        }
    }
}
