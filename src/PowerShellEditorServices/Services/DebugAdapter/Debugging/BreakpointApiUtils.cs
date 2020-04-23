//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter

{
    internal static class BreakpointApiUtils
    {
        #region Private Static Fields

        private const string s_psesGlobalVariableNamePrefix = "__psEditorServices_";

        private static readonly Lazy<Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>> s_setLineBreakpointLazy;

        private static readonly Lazy<Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>> s_setCommandBreakpointLazy;

        private static readonly Lazy<Func<Debugger, int?, List<Breakpoint>>> s_getBreakpointsLazy;

        private static readonly Lazy<Func<Debugger, Breakpoint, int?, bool>> s_removeBreakpointLazy;

        private static int breakpointHitCounter;

        #endregion

        #region Static Constructor

        static BreakpointApiUtils()
        {
            // If this version of PowerShell does not support the new Breakpoint APIs introduced in PowerShell 7.0.0,
            // do nothing as this class will not get used.
            if (!SupportsBreakpointApis)
            {
                return;
            }

            s_setLineBreakpointLazy = new Lazy<Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>>(() =>
            {
                Type[] setLineBreakpointParameters = new[] { typeof(string), typeof(int), typeof(int), typeof(ScriptBlock), typeof(int?) };
                MethodInfo setLineBreakpointMethod = typeof(Debugger).GetMethod("SetLineBreakpoint", setLineBreakpointParameters);

                return (Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>),
                    firstArgument: null,
                    setLineBreakpointMethod);
            });

            s_setCommandBreakpointLazy = new Lazy<Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>>(() =>
            {
                Type[] setCommandBreakpointParameters = new[] { typeof(string), typeof(ScriptBlock), typeof(string), typeof(int?) };
                MethodInfo setCommandBreakpointMethod = typeof(Debugger).GetMethod("SetCommandBreakpoint", setCommandBreakpointParameters);

                return (Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>),
                    firstArgument: null,
                    setCommandBreakpointMethod);
            });

            s_getBreakpointsLazy = new Lazy<Func<Debugger, int?, List<Breakpoint>>>(() =>
            {
                Type[] getBreakpointsParameters = new[] { typeof(int?) };
                MethodInfo getBreakpointsMethod = typeof(Debugger).GetMethod("GetBreakpoints", getBreakpointsParameters);

                return (Func<Debugger, int?, List<Breakpoint>>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, int?, List<Breakpoint>>),
                    firstArgument: null,
                    getBreakpointsMethod);
            });

            s_removeBreakpointLazy = new Lazy<Func<Debugger, Breakpoint, int?, bool>>(() =>
            {
                Type[] removeBreakpointParameters = new[] { typeof(Breakpoint), typeof(int?) };
                MethodInfo removeBreakpointMethod = typeof(Debugger).GetMethod("RemoveBreakpoint", removeBreakpointParameters);

                return (Func<Debugger, Breakpoint, int?, bool>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, Breakpoint, int?, bool>),
                    firstArgument: null,
                    removeBreakpointMethod);
            });
        }

        #endregion

        #region Public Static Properties

        private static Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint> SetLineBreakpointDelegate => s_setLineBreakpointLazy.Value;

        private static Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint> SetCommandBreakpointDelegate => s_setCommandBreakpointLazy.Value;

        private static Func<Debugger, int?, List<Breakpoint>> GetBreakpointsDelegate => s_getBreakpointsLazy.Value;

        private static Func<Debugger, Breakpoint, int?, bool> RemoveBreakpointDelegate => s_removeBreakpointLazy.Value;

        #endregion

        #region Public Static Properties

        // TODO: Try to compute this more dynamically. If we're launching a script in the PSIC, there are APIs are available in PS 5.1 and up.
        // For now, only PS7 or greater gets this feature.
        public static bool SupportsBreakpointApis => VersionUtils.IsPS7OrGreater;

        #endregion

        #region Public Static Methods

        public static Breakpoint SetBreakpoint(Debugger debugger, BreakpointDetailsBase breakpoint, int? runspaceId = null)
        {
            ScriptBlock actionScriptBlock = null;
            string logMessage = breakpoint is BreakpointDetails bd ? bd.LogMessage : null;
            // Check if this is a "conditional" line breakpoint.
            if (!string.IsNullOrWhiteSpace(breakpoint.Condition) ||
                !string.IsNullOrWhiteSpace(breakpoint.HitCondition) ||
                !string.IsNullOrWhiteSpace(logMessage))
            {
                actionScriptBlock = GetBreakpointActionScriptBlock(
                    breakpoint.Condition,
                    breakpoint.HitCondition,
                    logMessage,
                    out string errorMessage);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    // This is handled by the caller where it will set the 'Message' and 'Verified' on the BreakpointDetails
                    throw new InvalidOperationException(errorMessage);
                }
            }

            switch (breakpoint)
            {
                case BreakpointDetails lineBreakpoint:
                    return SetLineBreakpointDelegate(debugger, lineBreakpoint.Source, lineBreakpoint.LineNumber, lineBreakpoint.ColumnNumber ?? 0, actionScriptBlock, runspaceId);

                case CommandBreakpointDetails commandBreakpoint:
                    return SetCommandBreakpointDelegate(debugger, commandBreakpoint.Name, null, null, runspaceId);

                default:
                    throw new NotImplementedException("Other breakpoints not supported yet");
            }
        }

        public static List<Breakpoint> GetBreakpoints(Debugger debugger, int? runspaceId = null)
        {
            return GetBreakpointsDelegate(debugger, runspaceId);
        }

        public static bool RemoveBreakpoint(Debugger debugger, Breakpoint breakpoint, int? runspaceId = null)
        {
            return RemoveBreakpointDelegate(debugger, breakpoint, runspaceId);
        }

        /// <summary>
        /// Inspects the condition, putting in the appropriate scriptblock template
        /// "if (expression) { break }".  If errors are found in the condition, the
        /// breakpoint passed in is updated to set Verified to false and an error
        /// message is put into the breakpoint.Message property.
        /// </summary>
        /// <param name="condition">The expression that needs to be true for the breakpoint to be triggered.</param>
        /// <param name="hitCondition">The amount of times this line should be hit til the breakpoint is triggered.</param>
        /// <param name="logMessage">The log message to write instead of calling 'break'. In VS Code, this is called a 'logPoint'.</param>
        /// <returns>ScriptBlock</returns>
        public static ScriptBlock GetBreakpointActionScriptBlock(string condition, string hitCondition, string logMessage, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                StringBuilder builder = new StringBuilder(
                    string.IsNullOrEmpty(logMessage)
                        ? "break"
                        : $"Microsoft.PowerShell.Utility\\Write-Host \"{logMessage.Replace("\"","`\"")}\"");

                // If HitCondition specified, parse and verify it.
                if (!(string.IsNullOrWhiteSpace(hitCondition)))
                {
                    if (!int.TryParse(hitCondition, out int parsedHitCount))
                    {
                        throw new InvalidOperationException("Hit Count was not a valid integer.");
                    }

                    if(string.IsNullOrWhiteSpace(condition))
                    {
                        // In the HitCount only case, this is simple as we can just use the HitCount
                        // property on the breakpoint object which is represented by $_.
                        builder.Insert(0, $"if ($_.HitCount -eq {parsedHitCount}) {{ ")
                            .Append(" }");
                    }

                    int incrementResult = Interlocked.Increment(ref breakpointHitCounter);

                    string globalHitCountVarName =
                        $"$global:{s_psesGlobalVariableNamePrefix}BreakHitCounter_{incrementResult}";

                    builder.Insert(0, $"if (++{globalHitCountVarName} -eq {parsedHitCount}) {{ ")
                        .Append(" }");
                }

                if (!string.IsNullOrWhiteSpace(condition))
                {
                    ScriptBlock parsed = ScriptBlock.Create(condition);

                    // Check for simple, common errors that ScriptBlock parsing will not catch
                    // e.g. $i == 3 and $i > 3
                    if (!ValidateBreakpointConditionAst(parsed.Ast, out string message))
                    {
                        throw new InvalidOperationException(message);
                    }

                    // Check for "advanced" condition syntax i.e. if the user has specified
                    // a "break" or  "continue" statement anywhere in their scriptblock,
                    // pass their scriptblock through to the Action parameter as-is.
                    if (parsed.Ast.Find(ast =>
                        (ast is BreakStatementAst || ast is ContinueStatementAst), true) != null)
                    {
                        return parsed;
                    }

                    builder.Insert(0, $"if ({condition}) {{ ")
                        .Append(" }");
                }

                return ScriptBlock.Create(builder.ToString());
            }
            catch (ParseException e)
            {
                errorMessage = ExtractAndScrubParseExceptionMessage(e, condition);
                return null;
            }
            catch (InvalidOperationException e)
            {
                errorMessage = e.Message;
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

        #endregion
    }
}
