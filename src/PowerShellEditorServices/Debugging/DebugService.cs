//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a high-level service for interacting with the
    /// PowerShell debugger in the runspace managed by a PowerShellContext.
    /// </summary>
    public class DebugService
    {
        #region Fields

        private PowerShellContext powerShellContext;

        // TODO: This needs to be managed per nested session
        private Dictionary<string, List<Breakpoint>> breakpointsPerFile = 
            new Dictionary<string, List<Breakpoint>>();

        private int nextVariableId;
        private List<VariableDetailsBase> variables;
        private VariableContainerDetails globalScopeVariables;
        private VariableContainerDetails scriptScopeVariables;
        private StackFrameDetails[] stackFrameDetails;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the DebugService class and uses
        /// the given PowerShellContext for all future operations.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext to use for all debugging operations.
        /// </param>
        public DebugService(PowerShellContext powerShellContext)
        {
            Validate.IsNotNull("powerShellContext", powerShellContext);

            this.powerShellContext = powerShellContext;
            this.powerShellContext.DebuggerStop += this.OnDebuggerStop;
            this.powerShellContext.BreakpointUpdated += this.OnBreakpointUpdated;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the list of line breakpoints for the current debugging session.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which breakpoints will be set.</param>
        /// <param name="breakpoints">BreakpointDetails for each breakpoint that will be set.</param>
        /// <param name="clearExisting">If true, causes all existing breakpoints to be cleared before setting new ones.</param>
        /// <returns>An awaitable Task that will provide details about the breakpoints that were set.</returns>
        public async Task<BreakpointDetails[]> SetLineBreakpoints(
            ScriptFile scriptFile, 
            BreakpointDetails[] breakpoints, 
            bool clearExisting = true)
        {
            var resultBreakpointDetails = new List<BreakpointDetails>();

            if (clearExisting)
            {
                await this.ClearBreakpointsInFile(scriptFile);
            }

            if (breakpoints.Length > 0)
            {
                // Fix for issue #123 - file paths that contain wildcard chars [ and ] need to
                // quoted and have those wildcard chars escaped.
                string escapedScriptPath = 
                    PowerShellContext.EscapePath(scriptFile.FilePath, escapeSpaces: false);

                foreach (BreakpointDetails breakpoint in breakpoints)
                {
                    PSCommand psCommand = new PSCommand();
                    psCommand.AddCommand("Set-PSBreakpoint");
                    psCommand.AddParameter("Script", escapedScriptPath);
                    psCommand.AddParameter("Line", breakpoint.LineNumber);

                    // Check if the user has specified the column number for the breakpoint.
                    if (breakpoint.ColumnNumber.HasValue)
                    {
                        // It bums me out that PowerShell will silently ignore a breakpoint
                        // where either the line or the column is invalid.  I'd rather have an
                        // error or warning message I could relay back to the client.
                        psCommand.AddParameter("Column", breakpoint.ColumnNumber.Value);
                    }

                    // Check if this is a "conditional" line breakpoint.
                    if (breakpoint.Condition != null)
                    {
                        ScriptBlock actionScriptBlock =
                            GetBreakpointActionScriptBlock(breakpoint);

                        // If there was a problem with the condition string, 
                        // move onto the next breakpoint.
                        if (actionScriptBlock == null)
                        {
                            resultBreakpointDetails.Add(breakpoint);
                            continue;
                        }

                        psCommand.AddParameter("Action", actionScriptBlock);
                    }

                    IEnumerable<Breakpoint> configuredBreakpoints =
                        await this.powerShellContext.ExecuteCommand<Breakpoint>(psCommand);

                    // The order in which the breakpoints are returned is significant to the 
                    // VSCode client and should match the order in which they are passed in.
                    resultBreakpointDetails.AddRange(
                        configuredBreakpoints.Select(BreakpointDetails.Create));
                }
            }

            return resultBreakpointDetails.ToArray();
        }

        /// <summary>
        /// Sets the list of command breakpoints for the current debugging session. 
        /// </summary>
        /// <param name="breakpoints">BreakpointDetails for each command breakpoint that will be set.</param>
        /// <param name="clearExisting">If true, causes all existing function breakpoints to be cleared before setting new ones.</param>
        /// <returns>An awaitable Task that will provide details about the breakpoints that were set.</returns>
        public async Task<FunctionBreakpointDetails[]> SetCommandBreakpoints(
            FunctionBreakpointDetails[] breakpoints,
            bool clearExisting = true)
        {
            var resultBreakpointDetails = new List<FunctionBreakpointDetails>();

            if (clearExisting)
            {
                await this.ClearCommandBreakpoints();
            }

            if (breakpoints.Length > 0)
            {
                foreach (FunctionBreakpointDetails breakpoint in breakpoints)
                {
                    PSCommand psCommand = new PSCommand();
                    psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Set-PSBreakpoint");
                    psCommand.AddParameter("Command", breakpoint.Name);

                    // Check if this is a "conditional" command breakpoint.
                    if (breakpoint.Condition != null)
                    {
                        ScriptBlock actionScriptBlock = GetBreakpointActionScriptBlock(breakpoint);

                        // If there was a problem with the condition string, 
                        // move onto the next breakpoint.
                        if (actionScriptBlock == null)
                        {
                            resultBreakpointDetails.Add(breakpoint);
                            continue;
                        }

                        psCommand.AddParameter("Action", actionScriptBlock);
                    }

                    IEnumerable<Breakpoint> configuredBreakpoints =
                        await this.powerShellContext.ExecuteCommand<Breakpoint>(psCommand);

                    // The order in which the breakpoints are returned is significant to the 
                    // VSCode client and should match the order in which they are passed in.
                    resultBreakpointDetails.AddRange(
                        configuredBreakpoints.Select(FunctionBreakpointDetails.Create));
                }
            }

            return resultBreakpointDetails.ToArray();
        }

        /// <summary>
        /// Sends a "continue" action to the debugger when stopped.
        /// </summary>
        public void Continue()
        {
            this.powerShellContext.ResumeDebugger(
                DebuggerResumeAction.Continue);
        }

        /// <summary>
        /// Sends a "step over" action to the debugger when stopped. 
        /// </summary>
        public void StepOver()
        {
            this.powerShellContext.ResumeDebugger(
                DebuggerResumeAction.StepOver);
        }

        /// <summary>
        /// Sends a "step in" action to the debugger when stopped.
        /// </summary>
        public void StepIn()
        {
            this.powerShellContext.ResumeDebugger(
                DebuggerResumeAction.StepInto);
        }

        /// <summary>
        /// Sends a "step out" action to the debugger when stopped.
        /// </summary>
        public void StepOut()
        {
            this.powerShellContext.ResumeDebugger(
                DebuggerResumeAction.StepOut);
        }

        /// <summary>
        /// Causes the debugger to break execution wherever it currently
        /// is at the time.  This is equivalent to clicking "Pause" in a 
        /// debugger UI.
        /// </summary>
        public void Break()
        {
            // Break execution in the debugger
            this.powerShellContext.BreakExecution();
        }

        /// <summary>
        /// Aborts execution of the debugger while it is running, even while
        /// it is stopped.  Equivalent to calling PowerShellContext.AbortExecution.
        /// </summary>
        public void Abort()
        {
            this.powerShellContext.AbortExecution();
        }

        /// <summary>
        /// Gets the list of variables that are children of the scope or variable
        /// that is identified by the given referenced ID.
        /// </summary>
        /// <param name="variableReferenceId"></param>
        /// <returns>An array of VariableDetails instances which describe the requested variables.</returns>
        public VariableDetailsBase[] GetVariables(int variableReferenceId)
        {
            VariableDetailsBase[] childVariables;

            VariableDetailsBase parentVariable = this.variables[variableReferenceId];
            if (parentVariable.IsExpandable)
            {
                childVariables = parentVariable.GetChildren();
                foreach (var child in childVariables)
                {
                    // Only add child if it hasn't already been added.
                    if (child.Id < 0)
                    {
                        child.Id = this.nextVariableId++;
                        this.variables.Add(child);
                    }
                }
            }
            else
            {
                childVariables = new VariableDetailsBase[0];
            }

            return childVariables;
        }

        /// <summary>
        /// Evaluates a variable expression in the context of the stopped
        /// debugger.  This method decomposes the variable expression to
        /// walk the cached variable data for the specified stack frame.
        /// </summary>
        /// <param name="variableExpression">The variable expression string to evaluate.</param>
        /// <param name="stackFrameId">The ID of the stack frame in which the expression should be evaluated.</param>
        /// <returns>A VariableDetails object containing the result.</returns>
        public VariableDetailsBase GetVariableFromExpression(string variableExpression, int stackFrameId)
        {
            // Break up the variable path
            string[] variablePathParts = variableExpression.Split('.');

            VariableDetailsBase resolvedVariable = null;
            IEnumerable<VariableDetailsBase> variableList = this.variables;

            foreach (var variableName in variablePathParts)
            {
                if (variableList == null)
                {
                    // If there are no children left to search, break out early
                    return null;
                }

                resolvedVariable =
                    variableList.FirstOrDefault(
                        v =>
                            string.Equals(
                                v.Name,
                                variableExpression,
                                StringComparison.InvariantCultureIgnoreCase));

                if (resolvedVariable != null &&
                    resolvedVariable.IsExpandable)
                {
                    // Continue by searching in this variable's children
                    variableList = this.GetVariables(resolvedVariable.Id);
                }
            }

            return resolvedVariable;
        }

        /// <summary>
        /// Evaluates an expression in the context of the stopped
        /// debugger.  This method will execute the specified expression
        /// PowerShellContext.
        /// </summary>
        /// <param name="expressionString">The expression string to execute.</param>
        /// <param name="stackFrameId">The ID of the stack frame in which the expression should be executed.</param>
        /// <param name="writeResultAsOutput">
        /// If true, writes the expression result as host output rather than returning the results.
        /// In this case, the return value of this function will be null.</param>
        /// <returns>A VariableDetails object containing the result.</returns>
        public async Task<VariableDetails> EvaluateExpression(
            string expressionString,
            int stackFrameId,
            bool writeResultAsOutput)
        {
            var results = 
                await this.powerShellContext.ExecuteScriptString(
                    expressionString,
                    false,
                    writeResultAsOutput);

            // Since this method should only be getting invoked in the debugger,
            // we can assume that Out-String will be getting used to format results
            // of command executions into string output.  However, if null is returned
            // then return null so that no output gets displayed.
            string outputString =
                results != null ?
                    string.Join(Environment.NewLine, results) :
                    null;

            // If we've written the result as output, don't return a
            // VariableDetails instance.
            return
                writeResultAsOutput ?
                    null :
                    new VariableDetails(
                        expressionString,
                        outputString);
        }

        /// <summary>
        /// Gets the list of stack frames at the point where the
        /// debugger sf stopped.
        /// </summary>
        /// <returns>
        /// An array of StackFrameDetails instances that contain the stack trace.
        /// </returns>
        public StackFrameDetails[] GetStackFrames()
        {
            return this.stackFrameDetails;
        }

        /// <summary>
        /// Gets the list of variable scopes for the stack frame that
        /// is identified by the given ID.
        /// </summary>
        /// <param name="stackFrameId">The ID of the stack frame at which variable scopes should be retrieved.</param>
        /// <returns>The list of VariableScope instances which describe the available variable scopes.</returns>
        public VariableScope[] GetVariableScopes(int stackFrameId)
        {
            int localStackFrameVariableId = this.stackFrameDetails[stackFrameId].LocalVariables.Id;
            int autoVariablesId = this.stackFrameDetails[stackFrameId].AutoVariables.Id;

            return new VariableScope[]
            {
                new VariableScope(autoVariablesId, VariableContainerDetails.AutoVariablesName),
                new VariableScope(localStackFrameVariableId, VariableContainerDetails.LocalScopeName),
                new VariableScope(this.scriptScopeVariables.Id, VariableContainerDetails.ScriptScopeName),
                new VariableScope(this.globalScopeVariables.Id, VariableContainerDetails.GlobalScopeName),  
            };
        }

        #endregion

        #region Private Methods

        private async Task ClearBreakpointsInFile(ScriptFile scriptFile)
        {
            List<Breakpoint> breakpoints = null;

            // Get the list of breakpoints for this file
            if (this.breakpointsPerFile.TryGetValue(scriptFile.Id, out breakpoints))
            {
                if (breakpoints.Count > 0)
                {
                    PSCommand psCommand = new PSCommand();
                    psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Remove-PSBreakpoint");
                    psCommand.AddParameter("Breakpoint", breakpoints.ToArray());

                    await this.powerShellContext.ExecuteCommand<object>(psCommand);

                    // Clear the existing breakpoints list for the file
                    breakpoints.Clear();
                }
            }
        }

        private async Task ClearCommandBreakpoints()
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Get-PSBreakpoint");
            psCommand.AddParameter("Type", "Command");
            psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Remove-PSBreakpoint");

            await this.powerShellContext.ExecuteCommand<object>(psCommand);
        }

        private async Task FetchStackFramesAndVariables()
        {
            this.nextVariableId = VariableDetailsBase.FirstVariableId;
            this.variables = new List<VariableDetailsBase>();

            // Create a dummy variable for index 0, should never see this.
            this.variables.Add(new VariableDetails("Dummy", null));

            // Must retrieve global/script variales before stack frame variables
            // as we check stack frame variables against globals.
            await FetchGlobalAndScriptVariables();
            await FetchStackFrames();
        }

        private async Task FetchGlobalAndScriptVariables()
        {
            // Retrieve globals first as script variable retrieval needs to search globals.
            this.globalScopeVariables = 
                await FetchVariableContainer(VariableContainerDetails.GlobalScopeName, null);

            this.scriptScopeVariables = 
                await FetchVariableContainer(VariableContainerDetails.ScriptScopeName, null);
        }

        private async Task<VariableContainerDetails> FetchVariableContainer(
            string scope, 
            VariableContainerDetails autoVariables)
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-Variable");
            psCommand.AddParameter("Scope", scope);

            var scopeVariableContainer = 
                new VariableContainerDetails(this.nextVariableId++, "Scope: " + scope);
            this.variables.Add(scopeVariableContainer);

            var results = await this.powerShellContext.ExecuteCommand<PSVariable>(psCommand, sendErrorToHost: false);
            if (results != null)
            {
                foreach (PSVariable psvariable in results)
                {
                    var variableDetails = new VariableDetails(psvariable) {Id = this.nextVariableId++};
                    this.variables.Add(variableDetails);
                    scopeVariableContainer.Children.Add(variableDetails.Name, variableDetails);

                    if ((autoVariables != null) && AddToAutoVariables(psvariable, scope))
                    {
                        autoVariables.Children.Add(variableDetails.Name, variableDetails);
                    }
                }
            }

            return scopeVariableContainer;
        }

        private bool AddToAutoVariables(PSVariable psvariable, string scope)
        {
            if ((scope == VariableContainerDetails.GlobalScopeName) || 
                (scope == VariableContainerDetails.ScriptScopeName))
            {
                // We don't A) have a good way of distinguishing built-in from user created variables
                // and B) globalScopeVariables.Children.ContainsKey() doesn't work for built-in variables
                // stored in a child variable container within the globals variable container.
                return false;
            }

            var constantAllScope = ScopedItemOptions.AllScope | ScopedItemOptions.Constant;
            var readonlyAllScope = ScopedItemOptions.AllScope | ScopedItemOptions.ReadOnly;

            // Some local variables, if they exist, should be displayed by default
            if (psvariable.GetType().Name == "LocalVariable")
            {
                if (psvariable.Name.Equals("_"))
                {
                    return true;
                }
                else if (psvariable.Name.Equals("args", StringComparison.OrdinalIgnoreCase))
                {
                    var array = psvariable.Value as Array;
                    return array != null ? array.Length > 0 : false;
                }

                return false;
            }
            else if (psvariable.GetType() != typeof(PSVariable))
            {
                return false;
            }

            if (((psvariable.Options | constantAllScope) == constantAllScope) ||
                ((psvariable.Options | readonlyAllScope) == readonlyAllScope))
            {
                string prefixedVariableName = VariableDetails.DollarPrefix + psvariable.Name;
                if (this.globalScopeVariables.Children.ContainsKey(prefixedVariableName))
                {
                    return false;
                }
            }

            if ((psvariable.Value != null) && (psvariable.Value.GetType() == typeof(PSDebugContext)))
            {
                return false;
            }

            return true;
        }

        private async Task FetchStackFrames()
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddCommand("Get-PSCallStack");

            var results = await this.powerShellContext.ExecuteCommand<CallStackFrame>(psCommand);

            var callStackFrames = results.ToArray();
            this.stackFrameDetails = new StackFrameDetails[callStackFrames.Length];

            for (int i = 0; i < callStackFrames.Length; i++)
            {
                VariableContainerDetails autoVariables =
                    new VariableContainerDetails(
                        this.nextVariableId++, 
                        VariableContainerDetails.AutoVariablesName);

                this.variables.Add(autoVariables);

                VariableContainerDetails localVariables =
                    await FetchVariableContainer(i.ToString(), autoVariables);

                this.stackFrameDetails[i] = 
                    StackFrameDetails.Create(callStackFrames[i], autoVariables, localVariables);
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
                ScriptBlock actionScriptBlock = ScriptBlock.Create(breakpoint.Condition);

                // Check for simple, common errors that ScriptBlock parsing will not catch 
                // e.g. $i == 3 and $i > 3
                string message;
                if (!ValidateBreakpointConditionAst(actionScriptBlock.Ast, out message))
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
                    string wrappedCondition = $"if ({breakpoint.Condition}) {{ break }}";
                    actionScriptBlock = ScriptBlock.Create(wrappedCondition);
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

        private bool ValidateBreakpointConditionAst(Ast conditionAst, out string message)
        {
            message = string.Empty;

            // We are only inspecting a few simple scenarios in the EndBlock only.
            ScriptBlockAst scriptBlockAst = conditionAst as ScriptBlockAst;
            if ((scriptBlockAst != null) && 
                (scriptBlockAst.BeginBlock == null) && 
                (scriptBlockAst.ProcessBlock == null) && 
                (scriptBlockAst.EndBlock != null) && 
                (scriptBlockAst.EndBlock.Statements.Count == 1))
            {
                StatementAst statementAst = scriptBlockAst.EndBlock.Statements[0];
                string condition = statementAst.Extent.Text;

                if (statementAst is AssignmentStatementAst)
                {
                    message = FormatInvalidBreakpointConditionMessage(condition, "Use '-eq' instead of '=='.");
                    return false;
                }

                PipelineAst pipelineAst = statementAst as PipelineAst;
                if ((pipelineAst != null) && (pipelineAst.PipelineElements.Count == 1) &&
                    (pipelineAst.PipelineElements[0].Redirections.Count > 0))
                {
                    message = FormatInvalidBreakpointConditionMessage(condition, "Use '-gt' instead of '>'.");
                    return false;
                }
            }

            return true;
        }

        private string ExtractAndScrubParseExceptionMessage(ParseException parseException, string condition)
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

        private string FormatInvalidBreakpointConditionMessage(string condition, string message)
        {
            return $"'{condition}' is not a valid PowerShell expression. {message}";
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the debugger stops execution at a breakpoint or when paused.
        /// </summary>
        public event EventHandler<DebuggerStopEventArgs> DebuggerStopped;

        private async void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            // Get call stack and variables.
            await this.FetchStackFramesAndVariables();

            // Notify the host that the debugger is stopped
            if (this.DebuggerStopped != null)
            {
                this.DebuggerStopped(sender, e);
            }
        }

        /// <summary>
        /// Raised when a breakpoint is added/removed/updated in the debugger.
        /// </summary>
        public event EventHandler<BreakpointUpdatedEventArgs> BreakpointUpdated;

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            // This event callback also gets called when a CommandBreakpoint is modified.
            // Only execute the following code for LineBreakpoint so we can keep track
            // of which line breakpoints exist per script file.  We use this later when
            // we need to clear all breakpoints in a script file.  We do not need to do
            // this for CommandBreakpoint, as those span all script files.
            LineBreakpoint lineBreakpoint = e.Breakpoint as LineBreakpoint;
            if (lineBreakpoint != null)
            {
                List<Breakpoint> breakpoints;

                // Normalize the script filename for proper indexing
                string normalizedScriptName = lineBreakpoint.Script.ToLower();

                // Get the list of breakpoints for this file
                if (!this.breakpointsPerFile.TryGetValue(normalizedScriptName, out breakpoints))
                {
                    breakpoints = new List<Breakpoint>();
                    this.breakpointsPerFile.Add(
                        normalizedScriptName,
                        breakpoints);
                }

                // Add or remove the breakpoint based on the update type
                if (e.UpdateType == BreakpointUpdateType.Set)
                {
                    breakpoints.Add(e.Breakpoint);
                }
                else if (e.UpdateType == BreakpointUpdateType.Removed)
                {
                    breakpoints.Remove(e.Breakpoint);
                }
                else
                {
                    // TODO: Do I need to switch out instances for updated breakpoints?
                }
            }

            this.BreakpointUpdated?.Invoke(sender, e);
        }

        #endregion
    }
}
