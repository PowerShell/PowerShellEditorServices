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
        private const string s_psesGlobalVariableNamePrefix = "__psEditorServices_";
        private readonly ILogger<BreakpointService> _logger;
        private readonly PowerShellContextService _powerShellContextService;

        private static int breakpointHitCounter;

        public BreakpointService(
            ILoggerFactory factory,
            PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<BreakpointService>();
            _powerShellContextService = powerShellContextService;
        }

        public async Task<BreakpointDetails[]> SetBreakpointsAsync(string escapedScriptPath, IEnumerable<BreakpointDetails> breakpoints)
        {
            if (VersionUtils.IsPS7OrGreater)
            {
                return BreakpointApiUtils.SetBreakpoints(
                    _powerShellContextService.CurrentRunspace.Runspace.Debugger,
                    breakpoints)
                    .Select(BreakpointDetails.Create).ToArray();
            }

            // Legacy behavior
            PSCommand psCommand = null;
            List<BreakpointDetails> configuredBreakpoints = new List<BreakpointDetails>();
            foreach (BreakpointDetails breakpoint in breakpoints)
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

                // Check if this is a "conditional" line breakpoint.
                if (!string.IsNullOrWhiteSpace(breakpoint.Condition) ||
                    !string.IsNullOrWhiteSpace(breakpoint.HitCondition))
                {
                    ScriptBlock actionScriptBlock =
                        GetBreakpointActionScriptBlock(breakpoint);

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
                    setBreakpoints.Select(BreakpointDetails.Create));
            }

            return configuredBreakpoints.ToArray();
        }

        public async Task<IEnumerable<CommandBreakpointDetails>> SetCommandBreakpoints(IEnumerable<CommandBreakpointDetails> breakpoints)
        {
            if (VersionUtils.IsPS7OrGreater)
            {
                return BreakpointApiUtils.SetBreakpoints(
                    _powerShellContextService.CurrentRunspace.Runspace.Debugger,
                    breakpoints)
                    .Select(CommandBreakpointDetails.Create);
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
                        GetBreakpointActionScriptBlock(breakpoint);

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

        public async Task RemoveBreakpoints(IEnumerable<Breakpoint> breakpoints)
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

        /// <summary>
        /// Inspects the condition, putting in the appropriate scriptblock template
        /// "if (expression) { break }".  If errors are found in the condition, the
        /// breakpoint passed in is updated to set Verified to false and an error
        /// message is put into the breakpoint.Message property.
        /// </summary>
        /// <param name="breakpoint"></param>
        /// <returns></returns>
        private ScriptBlock GetBreakpointActionScriptBlock(
            BreakpointDetailsBase breakpoint)
        {
            try
            {
                ScriptBlock actionScriptBlock;
                int? hitCount = null;

                // If HitCondition specified, parse and verify it.
                if (!(string.IsNullOrWhiteSpace(breakpoint.HitCondition)))
                {
                    if (int.TryParse(breakpoint.HitCondition, out int parsedHitCount))
                    {
                        hitCount = parsedHitCount;
                    }
                    else
                    {
                        breakpoint.Verified = false;
                        breakpoint.Message = $"The specified HitCount '{breakpoint.HitCondition}' is not valid. " +
                                              "The HitCount must be an integer number.";
                        return null;
                    }
                }

                // Create an Action scriptblock based on condition and/or hit count passed in.
                if (hitCount.HasValue && string.IsNullOrWhiteSpace(breakpoint.Condition))
                {
                    // In the HitCount only case, this is simple as we can just use the HitCount
                    // property on the breakpoint object which is represented by $_.
                    string action = $"if ($_.HitCount -eq {hitCount}) {{ break }}";
                    actionScriptBlock = ScriptBlock.Create(action);
                }
                else if (!string.IsNullOrWhiteSpace(breakpoint.Condition))
                {
                    // Must be either condition only OR condition and hit count.
                    actionScriptBlock = ScriptBlock.Create(breakpoint.Condition);

                    // Check for simple, common errors that ScriptBlock parsing will not catch
                    // e.g. $i == 3 and $i > 3
                    if (!ValidateBreakpointConditionAst(actionScriptBlock.Ast, out string message))
                    {
                        breakpoint.Verified = false;
                        breakpoint.Message = message;
                        return null;
                    }

                    // Check for "advanced" condition syntax i.e. if the user has specified
                    // a "break" or  "continue" statement anywhere in their scriptblock,
                    // pass their scriptblock through to the Action parameter as-is.
                    Ast breakOrContinueStatementAst =
                        actionScriptBlock.Ast.Find(
                            ast => (ast is BreakStatementAst || ast is ContinueStatementAst), true);

                    // If this isn't advanced syntax then the conditions string should be a simple
                    // expression that needs to be wrapped in a "if" test that conditionally executes
                    // a break statement.
                    if (breakOrContinueStatementAst == null)
                    {
                        string wrappedCondition;

                        if (hitCount.HasValue)
                        {
                            Interlocked.Increment(ref breakpointHitCounter);

                            string globalHitCountVarName =
                                $"$global:{s_psesGlobalVariableNamePrefix}BreakHitCounter_{breakpointHitCounter}";

                            wrappedCondition =
                                $"if ({breakpoint.Condition}) {{ if (++{globalHitCountVarName} -eq {hitCount}) {{ break }} }}";
                        }
                        else
                        {
                            wrappedCondition = $"if ({breakpoint.Condition}) {{ break }}";
                        }

                        actionScriptBlock = ScriptBlock.Create(wrappedCondition);
                    }
                }
                else
                {
                    // Shouldn't get here unless someone called this with no condition and no hit count.
                    actionScriptBlock = ScriptBlock.Create("break");
                    _logger.LogWarning("No condition and no hit count specified by caller.");
                }

                return actionScriptBlock;
            }
            catch (ParseException ex)
            {
                // Failed to create conditional breakpoint likely because the user provided an
                // invalid PowerShell expression. Let the user know why.
                breakpoint.Verified = false;
                breakpoint.Message = ExtractAndScrubParseExceptionMessage(ex, breakpoint.Condition);
                return null;
            }
        }

        private static bool ValidateBreakpointConditionAst(Ast conditionAst, out string message)
        {
            message = string.Empty;

            // We are only inspecting a few simple scenarios in the EndBlock only.
            if (conditionAst is ScriptBlockAst scriptBlockAst &&
                scriptBlockAst.BeginBlock == null &&
                scriptBlockAst.ProcessBlock == null &&
                scriptBlockAst.EndBlock != null &&
                scriptBlockAst.EndBlock.Statements.Count == 1)
            {
                StatementAst statementAst = scriptBlockAst.EndBlock.Statements[0];
                string condition = statementAst.Extent.Text;

                if (statementAst is AssignmentStatementAst)
                {
                    message = FormatInvalidBreakpointConditionMessage(condition, "Use '-eq' instead of '=='.");
                    return false;
                }

                if (statementAst is PipelineAst pipelineAst
                    && pipelineAst.PipelineElements.Count == 1
                    && pipelineAst.PipelineElements[0].Redirections.Count > 0)
                {
                    message = FormatInvalidBreakpointConditionMessage(condition, "Use '-gt' instead of '>'.");
                    return false;
                }
            }

            return true;
        }

        private static string ExtractAndScrubParseExceptionMessage(ParseException parseException, string condition)
        {
            string[] messageLines = parseException.Message.Split('\n');

            // Skip first line - it is a location indicator "At line:1 char: 4"
            for (int i = 1; i < messageLines.Length; i++)
            {
                string line = messageLines[i];
                if (line.StartsWith("+"))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Note '==' and '>" do not generate parse errors
                    if (line.Contains("'!='"))
                    {
                        line += " Use operator '-ne' instead of '!='.";
                    }
                    else if (line.Contains("'<'") && condition.Contains("<="))
                    {
                        line += " Use operator '-le' instead of '<='.";
                    }
                    else if (line.Contains("'<'"))
                    {
                        line += " Use operator '-lt' instead of '<'.";
                    }
                    else if (condition.Contains(">="))
                    {
                        line += " Use operator '-ge' instead of '>='.";
                    }

                    return FormatInvalidBreakpointConditionMessage(condition, line);
                }
            }

            // If the message format isn't in a form we expect, just return the whole message.
            return FormatInvalidBreakpointConditionMessage(condition, parseException.Message);
        }

        private static string FormatInvalidBreakpointConditionMessage(string condition, string message)
        {
            return $"'{condition}' is not a valid PowerShell expression. {message}";
        }
    }
}
