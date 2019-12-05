//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class BreakpointService
    {
        private readonly ILogger<BreakpointService> _logger;
        private readonly PowerShellContextService _powerShellContextService;

        public BreakpointService(
            ILoggerFactory factory,
            PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<BreakpointService>();
            _powerShellContextService = powerShellContextService;
        }

        public async Task<IEnumerable<BreakpointDetails>> SetBreakpointsAsync(string escapedScriptPath, IEnumerable<BreakpointDetails> breakpoints)
        {
            if (VersionUtils.IsPS7OrGreater)
            {
                foreach (BreakpointDetails breakpointDetails in breakpoints)
                {
                    try
                    {
                        BreakpointApiUtils.SetBreakpoint(_powerShellContextService.CurrentRunspace.Runspace.Debugger, breakpointDetails);

                    }
                    catch(InvalidOperationException e)
                    {
                        breakpointDetails.Message = e.Message;
                        breakpointDetails.Verified = false;
                    }
                }

                return breakpoints;
            }

            // Legacy behavior
            PSCommand psCommand = null;
            List<BreakpointDetails> configuredBreakpoints = new List<BreakpointDetails>();
            foreach (BreakpointDetails breakpoint in breakpoints)
            {
                ScriptBlock actionScriptBlock = null;

                // Check if this is a "conditional" line breakpoint.
                if (!string.IsNullOrWhiteSpace(breakpoint.Condition) ||
                    !string.IsNullOrWhiteSpace(breakpoint.HitCondition) ||
                    !string.IsNullOrWhiteSpace(breakpoint.LogMessage))
                {
                    try
                    {
                        actionScriptBlock = BreakpointApiUtils.GetBreakpointActionScriptBlock(
                            breakpoint.Condition,
                            breakpoint.HitCondition,
                            breakpoint.LogMessage);
                    }
                    catch (InvalidOperationException e)
                    {
                        breakpoint.Verified = false;
                        breakpoint.Message = e.Message;
                    }
                }

                // On first iteration psCommand will be null, every subsequent
                // iteration will need to start a new statement.
                if (psCommand == null)
                {
                    psCommand = new PSCommand();
                }
                else
                {
                    psCommand.AddStatement();
                }

                psCommand
                    .AddCommand(@"Microsoft.PowerShell.Utility\Set-PSBreakpoint")
                    .AddParameter("Script", escapedScriptPath)
                    .AddParameter("Line", breakpoint.LineNumber);

                // Check if the user has specified the column number for the breakpoint.
                if (breakpoint.ColumnNumber.HasValue && breakpoint.ColumnNumber.Value > 0)
                {
                    // It bums me out that PowerShell will silently ignore a breakpoint
                    // where either the line or the column is invalid.  I'd rather have an
                    // error or warning message I could relay back to the client.
                    psCommand.AddParameter("Column", breakpoint.ColumnNumber.Value);
                }

                if (actionScriptBlock != null)
                {
                    psCommand.AddParameter("Action", actionScriptBlock);
                }
            }

            // If no PSCommand was created then there are no breakpoints to set.
            if (psCommand != null)
            {
                IEnumerable<Breakpoint> setBreakpoints =
                    await _powerShellContextService.ExecuteCommandAsync<Breakpoint>(psCommand);
                configuredBreakpoints.AddRange(
                    setBreakpoints.Select(BreakpointDetails.Create));
            }

            return configuredBreakpoints;
        }

        public async Task<IEnumerable<CommandBreakpointDetails>> SetCommandBreakpoints(IEnumerable<CommandBreakpointDetails> breakpoints)
        {
            if (VersionUtils.IsPS7OrGreater)
            {
                foreach (CommandBreakpointDetails commandBreakpointDetails in breakpoints)
                {
                    try
                    {
                        BreakpointApiUtils.SetBreakpoint(_powerShellContextService.CurrentRunspace.Runspace.Debugger, commandBreakpointDetails);
                    }
                    catch(InvalidOperationException e)
                    {
                        commandBreakpointDetails.Message = e.Message;
                        commandBreakpointDetails.Verified = false;
                    }
                }

                return breakpoints;
            }

            // Legacy behavior
            PSCommand psCommand = null;
            List<CommandBreakpointDetails> configuredBreakpoints = new List<CommandBreakpointDetails>();
            foreach (CommandBreakpointDetails breakpoint in breakpoints)
            {
                // On first iteration psCommand will be null, every subsequent
                // iteration will need to start a new statement.
                if (psCommand == null)
                {
                    psCommand = new PSCommand();
                }
                else
                {
                    psCommand.AddStatement();
                }

                psCommand
                    .AddCommand(@"Microsoft.PowerShell.Utility\Set-PSBreakpoint")
                    .AddParameter("Command", breakpoint.Name);

                // Check if this is a "conditional" line breakpoint.
                if (!string.IsNullOrWhiteSpace(breakpoint.Condition) ||
                    !string.IsNullOrWhiteSpace(breakpoint.HitCondition))
                {
                    ScriptBlock actionScriptBlock =
                        BreakpointApiUtils.GetBreakpointActionScriptBlock(breakpoint);

                    // If there was a problem with the condition string,
                    // move onto the next breakpoint.
                    if (actionScriptBlock == null)
                    {
                        configuredBreakpoints.Add(breakpoint);
                        continue;
                    }

                    psCommand.AddParameter("Action", actionScriptBlock);
                }
            }

            // If no PSCommand was created then there are no breakpoints to set.
            if (psCommand != null)
            {
                IEnumerable<Breakpoint> setBreakpoints =
                    await _powerShellContextService.ExecuteCommandAsync<Breakpoint>(psCommand);
                configuredBreakpoints.AddRange(
                    setBreakpoints.Select(CommandBreakpointDetails.Create));
            }

            return configuredBreakpoints;
        }

        /// <summary>
        /// Clears all breakpoints in the current session.
        /// </summary>
        public async Task RemoveAllBreakpointsAsync()
        {
            try
            {
                if (VersionUtils.IsPS7OrGreater)
                {
                    foreach (Breakpoint breakpoint in BreakpointApiUtils.GetBreakpoints(
                            _powerShellContextService.CurrentRunspace.Runspace.Debugger))
                    {
                        BreakpointApiUtils.RemoveBreakpoint(
                            _powerShellContextService.CurrentRunspace.Runspace.Debugger,
                            breakpoint);
                    }

                    return;
                }

                // Legacy behavior

                PSCommand psCommand = new PSCommand();
                psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Get-PSBreakpoint");
                psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Remove-PSBreakpoint");

                await _powerShellContextService.ExecuteCommandAsync<object>(psCommand);
            }
            catch (Exception e)
            {
                _logger.LogException("Caught exception while clearing breakpoints from session", e);
            }
        }

        public async Task RemoveBreakpointsAsync(IEnumerable<Breakpoint> breakpoints)
        {
            if (VersionUtils.IsPS7OrGreater)
            {
                foreach (Breakpoint breakpoint in breakpoints)
                {
                    BreakpointApiUtils.RemoveBreakpoint(
                        _powerShellContextService.CurrentRunspace.Runspace.Debugger,
                        breakpoint);
                }

                return;
            }

            // Legacy behavior
            var breakpointIds = breakpoints.Select(b => b.Id).ToArray();
            if(breakpointIds.Length > 0)
            {
                PSCommand psCommand = new PSCommand();
                psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Remove-PSBreakpoint");
                psCommand.AddParameter("Id", breakpoints.Select(b => b.Id).ToArray());

                await _powerShellContextService.ExecuteCommandAsync<object>(psCommand);
            }
        }


    }
}
