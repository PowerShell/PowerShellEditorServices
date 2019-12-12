//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter

{
    internal static class BreakpointApiUtils
    {
        #region Private Static Fields

        private const string s_psesGlobalVariableNamePrefix = "__psEditorServices_";

        private static readonly Lazy<Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>> s_setLineBreakpointLazy;

        private static readonly Lazy<Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>> s_setCommandBreakpointLazy;

        private static readonly Lazy<Func<Debugger, int?, List<Breakpoint>>> s_getBreakpointsLazy;

        private static readonly Lazy<Action<Debugger, IEnumerable<Breakpoint>, int?>> s_setBreakpointsLazy;

        private static readonly Lazy<Func<Debugger, Breakpoint, int?, bool>> s_removeBreakpointLazy;

        private static int breakpointHitCounter;

        #endregion

        #region Static Constructor

        static BreakpointApiUtils()
        {
            // If this version of PowerShell does not support the new Breakpoint APIs introduced in PowerShell 7.0.0-preview.4,
            // do nothing as this class will not get used.
            if (typeof(Debugger).GetMethod("SetLineBreakpoint", BindingFlags.Public | BindingFlags.Instance) == null)
            {
                return;
            }

            s_setLineBreakpointLazy = new Lazy<Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>>(() =>
            {
                MethodInfo setLineBreakpointMethod = typeof(Debugger).GetMethod("SetLineBreakpoint", BindingFlags.Public | BindingFlags.Instance);

                return (Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, string, int, int, ScriptBlock, int?, LineBreakpoint>),
                    firstArgument: null,
                    setLineBreakpointMethod);
            });

            s_setCommandBreakpointLazy = new Lazy<Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>>(() =>
            {
                MethodInfo setCommandBreakpointMethod = typeof(Debugger).GetMethod("SetCommandBreakpoint", BindingFlags.Public | BindingFlags.Instance);

                return (Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, string, ScriptBlock, string, int?, CommandBreakpoint>),
                    firstArgument: null,
                    setCommandBreakpointMethod);
            });

            s_getBreakpointsLazy = new Lazy<Func<Debugger, int?, List<Breakpoint>>>(() =>
            {
                MethodInfo removeBreakpointMethod = typeof(Debugger).GetMethod("GetBreakpoints", BindingFlags.Public | BindingFlags.Instance);

                return (Func<Debugger, int?, List<Breakpoint>>)Delegate.CreateDelegate(
                    typeof(Func<Debugger, int?, List<Breakpoint>>),
                    firstArgument: null,
                    removeBreakpointMethod);
            });

            s_setBreakpointsLazy = new Lazy<Action<Debugger, IEnumerable<Breakpoint>, int?>>(() =>
            {
                MethodInfo removeBreakpointMethod = typeof(Debugger).GetMethod("SetBreakpoints", BindingFlags.Public | BindingFlags.Instance);

                return (Action<Debugger, IEnumerable<Breakpoint>, int?>)Action.CreateDelegate(
                    typeof(Action<Debugger, IEnumerable<Breakpoint>, int?>),
                    firstArgument: null,
                    removeBreakpointMethod);
            });

            s_removeBreakpointLazy = new Lazy<Func<Debugger, Breakpoint, int?, bool>>(() =>
            {
                MethodInfo removeBreakpointMethod = typeof(Debugger).GetMethod("RemoveBreakpoint", BindingFlags.Public | BindingFlags.Instance);

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

        private static Action<Debugger, IEnumerable<Breakpoint>, int?> SetBreakpointsDelegate => s_setBreakpointsLazy.Value;

        private static Func<Debugger, Breakpoint, int?, bool> RemoveBreakpointDelegate => s_removeBreakpointLazy.Value;

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
                actionScriptBlock = GetBreakpointActionScriptBlock(breakpoint.Condition, breakpoint.HitCondition, logMessage);
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

        public static ScriptBlock GetBreakpointActionScriptBlock(string condition, string hitCondition, string logMessage)
        {
            StringBuilder builder = new StringBuilder(
                string.IsNullOrEmpty(logMessage)
                    ? "break"
                    : $"Microsoft.PowerShell.Utility\\Write-Host '{logMessage}'");

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
                        .Append(" }}");
                }

                Interlocked.Increment(ref breakpointHitCounter);

                string globalHitCountVarName =
                    $"$global:{s_psesGlobalVariableNamePrefix}BreakHitCounter_{breakpointHitCounter}";

                builder.Insert(0, $"if (++{globalHitCountVarName} -eq {parsedHitCount}) {{ ")
                    .Append(" }}");
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
                    .Append(" }}");
            }

            return ScriptBlock.Create(builder.ToString());
        }

        /// <summary>
        /// Inspects the condition, putting in the appropriate scriptblock template
        /// "if (expression) { break }".  If errors are found in the condition, the
        /// breakpoint passed in is updated to set Verified to false and an error
        /// message is put into the breakpoint.Message property.
        /// </summary>
        /// <param name="breakpoint"></param>
        /// <returns>ScriptBlock</returns>
        public static ScriptBlock GetBreakpointActionScriptBlock(
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

        #endregion
    }
}
