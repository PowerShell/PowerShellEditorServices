﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Provides details about a command breakpoint that is set in the PowerShell debugger.
    /// </summary>
    internal sealed class CommandBreakpointDetails : BreakpointDetailsBase
    {
        /// <summary>
        /// Gets the name of the command on which the command breakpoint has been set.
        /// </summary>
        public string Name { get; private set; }

        private CommandBreakpointDetails()
        {
        }

        /// <summary>
        /// Creates an instance of the <see cref="CommandBreakpointDetails"/> class from the individual
        /// pieces of breakpoint information provided by the client.
        /// </summary>
        /// <param name="name">The name of the command to break on.</param>
        /// <param name="condition">Condition string that would be applied to the breakpoint Action parameter.</param>
        /// <returns></returns>
        internal static CommandBreakpointDetails Create(string name, string condition = null)
        {
            Validate.IsNotNull(nameof(name), name);

            return new CommandBreakpointDetails
            {
                Name = name,
                Condition = condition
            };
        }

        /// <summary>
        /// Creates an instance of the <see cref="CommandBreakpointDetails"/> class from a
        /// PowerShell CommandBreakpoint object.
        /// </summary>
        /// <param name="breakpoint">The Breakpoint instance from which details will be taken.</param>
        /// <returns>A new instance of the BreakpointDetails class.</returns>
        internal static CommandBreakpointDetails Create(Breakpoint breakpoint)
        {
            Validate.IsNotNull(nameof(breakpoint), breakpoint);

            if (breakpoint is not CommandBreakpoint commandBreakpoint)
            {
                throw new ArgumentException(
                    "Unexpected breakpoint type: " + breakpoint.GetType().Name);
            }

            return new()
            {
                Verified = true,
                Name = commandBreakpoint.Command,
                Condition = commandBreakpoint.Action?.ToString()
            };
        }
    }
}
