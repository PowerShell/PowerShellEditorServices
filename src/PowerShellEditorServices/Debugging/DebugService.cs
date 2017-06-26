﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Debugging;
using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Session.Capabilities;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a high-level service for interacting with the
    /// PowerShell debugger in the runspace managed by a PowerShellContext.
    /// </summary>
    public class DebugService
    {
        #region Fields

        private const string PsesGlobalVariableNamePrefix = "__psEditorServices_";
        private const string TemporaryScriptFileName = "Script Listing.ps1";

        private ILogger logger;
        private PowerShellContext powerShellContext;
        private RemoteFileManager remoteFileManager;

        // TODO: This needs to be managed per nested session
        private Dictionary<string, List<Breakpoint>> breakpointsPerFile =
            new Dictionary<string, List<Breakpoint>>();

        private int nextVariableId;
        private string temporaryScriptListingPath;
        private List<VariableDetailsBase> variables;
        private VariableContainerDetails globalScopeVariables;
        private VariableContainerDetails scriptScopeVariables;
        private StackFrameDetails[] stackFrameDetails;
        private PropertyInfo invocationTypeScriptPositionProperty;

        private static int breakpointHitCounter = 0;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a boolean that indicates whether a debugger client is
        /// currently attached to the debugger.
        /// </summary>
        public bool IsClientAttached { get; set; }

        /// <summary>
        /// Gets a boolean that indicates whether the debugger is currently
        /// stopped at a breakpoint.
        /// </summary>
        public bool IsDebuggerStopped => this.powerShellContext.IsDebuggerStopped;

        /// <summary>
        /// Gets the current DebuggerStoppedEventArgs when the debugger
        /// is stopped.
        /// </summary>
        public DebuggerStoppedEventArgs CurrentDebuggerStoppedEventArgs { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the DebugService class and uses
        /// the given PowerShellContext for all future operations.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext to use for all debugging operations.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public DebugService(PowerShellContext powerShellContext, ILogger logger)
            : this(powerShellContext, null, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DebugService class and uses
        /// the given PowerShellContext for all future operations.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext to use for all debugging operations.
        /// </param>
        /// <param name="remoteFileManager">
        /// A RemoteFileManager instance to use for accessing files in remote sessions.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public DebugService(
            PowerShellContext powerShellContext,
            RemoteFileManager remoteFileManager,
            ILogger logger)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            this.logger = logger;
            this.powerShellContext = powerShellContext;
            this.powerShellContext.DebuggerStop += this.OnDebuggerStop;
            this.powerShellContext.DebuggerResumed += this.OnDebuggerResumed;

            this.powerShellContext.BreakpointUpdated += this.OnBreakpointUpdated;

            this.remoteFileManager = remoteFileManager;

            this.invocationTypeScriptPositionProperty =
                typeof(InvocationInfo)
                    .GetProperty(
                        "ScriptPosition",
                        BindingFlags.NonPublic | BindingFlags.Instance);
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

            var dscBreakpoints =
                this.powerShellContext
                    .CurrentRunspace
                    .GetCapability<DscBreakpointCapability>();

            // Make sure we're using the remote script path
            string scriptPath = scriptFile.FilePath;
            if (this.powerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote &&
                this.remoteFileManager != null)
            {
                if (!this.remoteFileManager.IsUnderRemoteTempPath(scriptPath))
                {
                    this.logger.Write(
                        LogLevel.Verbose,
                        $"Could not set breakpoints for local path '{scriptPath}' in a remote session.");

                    return resultBreakpointDetails.ToArray();
                }

                string mappedPath =
                    this.remoteFileManager.GetMappedPath(
                        scriptPath,
                        this.powerShellContext.CurrentRunspace);

                scriptPath = mappedPath;
            }
            else if (
                this.temporaryScriptListingPath != null &&
                this.temporaryScriptListingPath.Equals(scriptPath, StringComparison.CurrentCultureIgnoreCase))
            {
                this.logger.Write(
                    LogLevel.Verbose,
                    $"Could not set breakpoint on temporary script listing path '{scriptPath}'.");

                return resultBreakpointDetails.ToArray();
            }

            // Fix for issue #123 - file paths that contain wildcard chars [ and ] need to
            // quoted and have those wildcard chars escaped.
            string escapedScriptPath =
                PowerShellContext.EscapePath(scriptPath, escapeSpaces: false);

            if (dscBreakpoints == null || !dscBreakpoints.IsDscResourcePath(escapedScriptPath))
            {
                if (clearExisting)
                {
                    await this.ClearBreakpointsInFile(scriptFile);
                }

                foreach (BreakpointDetails breakpoint in breakpoints)
                {
                    PSCommand psCommand = new PSCommand();
                    psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Set-PSBreakpoint");
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
                    if (!String.IsNullOrWhiteSpace(breakpoint.Condition) ||
                        !String.IsNullOrWhiteSpace(breakpoint.HitCondition))
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
            else
            {
                resultBreakpointDetails =
                    await dscBreakpoints.SetLineBreakpoints(
                        this.powerShellContext,
                        escapedScriptPath,
                        breakpoints);
            }

            return resultBreakpointDetails.ToArray();
        }

        /// <summary>
        /// Sets the list of command breakpoints for the current debugging session.
        /// </summary>
        /// <param name="breakpoints">CommandBreakpointDetails for each command breakpoint that will be set.</param>
        /// <param name="clearExisting">If true, causes all existing function breakpoints to be cleared before setting new ones.</param>
        /// <returns>An awaitable Task that will provide details about the breakpoints that were set.</returns>
        public async Task<CommandBreakpointDetails[]> SetCommandBreakpoints(
            CommandBreakpointDetails[] breakpoints,
            bool clearExisting = true)
        {
            var resultBreakpointDetails = new List<CommandBreakpointDetails>();

            if (clearExisting)
            {
                await this.ClearCommandBreakpoints();
            }

            if (breakpoints.Length > 0)
            {
                foreach (CommandBreakpointDetails breakpoint in breakpoints)
                {
                    PSCommand psCommand = new PSCommand();
                    psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Set-PSBreakpoint");
                    psCommand.AddParameter("Command", breakpoint.Name);

                    // Check if this is a "conditional" command breakpoint.
                    if (!String.IsNullOrWhiteSpace(breakpoint.Condition) ||
                        !String.IsNullOrWhiteSpace(breakpoint.HitCondition))
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
                        configuredBreakpoints.Select(CommandBreakpointDetails.Create));
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
                childVariables = parentVariable.GetChildren(this.logger);
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
        /// <returns>A VariableDetailsBase object containing the result.</returns>
        public VariableDetailsBase GetVariableFromExpression(string variableExpression, int stackFrameId)
        {
            // NOTE: From a watch we will get passed expressions that are not naked variables references.
            // Probably the right way to do this woudld be to examine the AST of the expr before calling
            // this method to make sure it is a VariableReference.  But for the most part, non-naked variable
            // references are very unlikely to find a matching variable e.g. "$i+5.2" will find no var matching "$i+5".

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
                                variableName,
                                StringComparison.CurrentCultureIgnoreCase));

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
        /// Sets the specified variable by container variableReferenceId and variable name to the
        /// specified new value.  If the variable cannot be set or converted to that value this
        /// method will throw InvalidPowerShellExpressionException, ArgumentTransformationMetadataException, or
        /// SessionStateUnauthorizedAccessException.
        /// </summary>
        /// <param name="variableContainerReferenceId">The container (Autos, Local, Script, Global) that holds the variable.</param>
        /// <param name="name">The name of the variable prefixed with $.</param>
        /// <param name="value">The new string value.  This value must not be null.  If you want to set the variable to $null
        /// pass in the string "$null".</param>
        /// <returns>The string representation of the value the variable was set to.</returns>
        public async Task<string> SetVariable(int variableContainerReferenceId, string name, string value)
        {
            Validate.IsNotNull(nameof(name), name);
            Validate.IsNotNull(nameof(value), value);

            this.logger.Write(LogLevel.Verbose, $"SetVariableRequest for '{name}' to value string (pre-quote processing): '{value}'");

            // An empty or whitespace only value is not a valid expression for SetVariable.
            if (value.Trim().Length == 0)
            {
                throw new InvalidPowerShellExpressionException("Expected an expression.");
            }

            // Evaluate the expression to get back a PowerShell object from the expression string.
            PSCommand psCommand = new PSCommand();
            psCommand.AddScript(value);
            var errorMessages = new StringBuilder();
            var results =
                await this.powerShellContext.ExecuteCommand<object>(
                    psCommand,
                    errorMessages,
                    false,
                    false);

            // Check if PowerShell's evaluation of the expression resulted in an error.
            object psobject = results.FirstOrDefault();
            if ((psobject == null) && (errorMessages.Length > 0))
            {
                throw new InvalidPowerShellExpressionException(errorMessages.ToString());
            }

            // If PowerShellContext.ExecuteCommand returns an ErrorRecord as output, the expression failed evaluation.
            // Ideally we would have a separate means from communicating error records apart from normal output.
            ErrorRecord errorRecord = psobject as ErrorRecord;
            if (errorRecord != null)
            {
                throw new InvalidPowerShellExpressionException(errorRecord.ToString());
            }

            // OK, now we have a PS object from the supplied value string (expression) to assign to a variable.
            // Get the variable referenced by variableContainerReferenceId and variable name.
            VariableContainerDetails variableContainer = (VariableContainerDetails)this.variables[variableContainerReferenceId];
            VariableDetailsBase variable = variableContainer.Children[name];

            // Determine scope in which the variable lives. This is required later for the call to Get-Variable -Scope.
            string scope = null;
            if (variableContainerReferenceId == this.scriptScopeVariables.Id)
            {
                scope = "Script";
            }
            else if (variableContainerReferenceId == this.globalScopeVariables.Id)
            {
                scope = "Global";
            }
            else
            {
                // Determine which stackframe's local scope the variable is in.
                for (int i = 0; i < this.stackFrameDetails.Length; i++)
                {
                    var stackFrame = this.stackFrameDetails[i];
                    if (stackFrame.LocalVariables.ContainsVariable(variable.Id))
                    {
                        scope = i.ToString();
                        break;
                    }
                }
            }

            if (scope == null)
            {
                // Hmm, this would be unexpected.  No scope means do not pass GO, do not collect $200.
                throw new Exception("Could not find the scope for this variable.");
            }

            // Now that we have the scope, get the associated PSVariable object for the variable to be set.
            psCommand.Commands.Clear();
            psCommand = new PSCommand();
            psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Get-Variable");
            psCommand.AddParameter("Name", name.TrimStart('$'));
            psCommand.AddParameter("Scope", scope);

            IEnumerable<PSVariable> result = await this.powerShellContext.ExecuteCommand<PSVariable>(psCommand, sendErrorToHost: false);
            PSVariable psVariable = result.FirstOrDefault();
            if (psVariable == null)
            {
                throw new Exception($"Failed to retrieve PSVariable object for '{name}' from scope '{scope}'.");
            }

            // We have the PSVariable object for the variable the user wants to set and an object to assign to that variable.
            // The last step is to determine whether the PSVariable is "strongly typed" which may require a conversion.
            // If it is not strongly typed, we simply assign the object directly to the PSVariable potentially changing its type.
            // Turns out ArgumentTypeConverterAttribute is not public. So we call the attribute through it's base class -
            // ArgumentTransformationAttribute.
            var argTypeConverterAttr =
                psVariable.Attributes
                          .OfType<ArgumentTransformationAttribute>()
                          .FirstOrDefault(a => a.GetType().Name.Equals("ArgumentTypeConverterAttribute"));

            if (argTypeConverterAttr != null)
            {
                // PSVariable is strongly typed. Need to apply the conversion/transform to the new value.
                psCommand.Commands.Clear();
                psCommand = new PSCommand();
                psCommand.AddCommand(@"Microsoft.PowerShell.Utility\Get-Variable");
                psCommand.AddParameter("Name", "ExecutionContext");
                psCommand.AddParameter("ValueOnly");

                errorMessages.Clear();

                var getExecContextResults =
                    await this.powerShellContext.ExecuteCommand<object>(
                        psCommand,
                        errorMessages,
                        sendErrorToHost: false);

                EngineIntrinsics executionContext = getExecContextResults.OfType<EngineIntrinsics>().FirstOrDefault();

                var msg = $"Setting variable '{name}' using conversion to value: {psobject ?? "<null>"}";
                this.logger.Write(LogLevel.Verbose, msg);

                psVariable.Value = argTypeConverterAttr.Transform(executionContext, psobject);
            }
            else
            {
                // PSVariable is *not* strongly typed. In this case, whack the old value with the new value.
                var msg = $"Setting variable '{name}' directly to value: {psobject ?? "<null>"} - previous type was {psVariable.Value?.GetType().Name ?? "<unknown>"}";
                this.logger.Write(LogLevel.Verbose, msg);
                psVariable.Value = psobject;
            }

            // Use the VariableDetails.ValueString functionality to get the string representation for client debugger.
            // This makes the returned string consistent with the strings normally displayed for variables in the debugger.
            var tempVariable = new VariableDetails(psVariable);
            this.logger.Write(LogLevel.Verbose, $"Set variable '{name}' to: {tempVariable.ValueString ?? "<null>"}");
            return tempVariable.ValueString;
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
                results != null && results.Any() ?
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
                    psCommand.AddParameter("Id", breakpoints.Select(b => b.Id).ToArray());

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

        private async Task FetchStackFramesAndVariables(string scriptNameOverride)
        {
            this.nextVariableId = VariableDetailsBase.FirstVariableId;
            this.variables = new List<VariableDetailsBase>();

            // Create a dummy variable for index 0, should never see this.
            this.variables.Add(new VariableDetails("Dummy", null));

            // Must retrieve global/script variales before stack frame variables
            // as we check stack frame variables against globals.
            await FetchGlobalAndScriptVariables();
            await FetchStackFrames(scriptNameOverride);
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

            var results = await this.powerShellContext.ExecuteCommand<PSObject>(psCommand, sendErrorToHost: false);
            if (results != null)
            {
                foreach (PSObject psVariableObject in results)
                {
                    var variableDetails = new VariableDetails(psVariableObject) { Id = this.nextVariableId++ };
                    this.variables.Add(variableDetails);
                    scopeVariableContainer.Children.Add(variableDetails.Name, variableDetails);

                    if ((autoVariables != null) && AddToAutoVariables(psVariableObject, scope))
                    {
                        autoVariables.Children.Add(variableDetails.Name, variableDetails);
                    }
                }
            }

            return scopeVariableContainer;
        }

        private bool AddToAutoVariables(PSObject psvariable, string scope)
        {
            if ((scope == VariableContainerDetails.GlobalScopeName) ||
                (scope == VariableContainerDetails.ScriptScopeName))
            {
                // We don't A) have a good way of distinguishing built-in from user created variables
                // and B) globalScopeVariables.Children.ContainsKey() doesn't work for built-in variables
                // stored in a child variable container within the globals variable container.
                return false;
            }

            string variableName = psvariable.Properties["Name"].Value as string;
            object variableValue = psvariable.Properties["Value"].Value;

            // Don't put any variables created by PSES in the Auto variable container.
            if (variableName.StartsWith(PsesGlobalVariableNamePrefix) ||
                variableName.Equals("PSDebugContext"))
            {
                return false;
            }

            ScopedItemOptions variableScope = ScopedItemOptions.None;
            PSPropertyInfo optionsProperty = psvariable.Properties["Options"];
            if (string.Equals(optionsProperty.TypeNameOfValue, "System.String"))
            {
                if (!Enum.TryParse<ScopedItemOptions>(
                        optionsProperty.Value as string,
                        out variableScope))
                {
                    this.logger.Write(
                        LogLevel.Warning,
                        $"Could not parse a variable's ScopedItemOptions value of '{optionsProperty.Value}'");
                }
            }
            else if (optionsProperty.Value is ScopedItemOptions)
            {
                variableScope = (ScopedItemOptions)optionsProperty.Value;
            }

            // Some local variables, if they exist, should be displayed by default
            if (psvariable.TypeNames[0].EndsWith("LocalVariable"))
            {
                if (variableName.Equals("_"))
                {
                    return true;
                }
                else if (variableName.Equals("args", StringComparison.OrdinalIgnoreCase))
                {
                    var array = variableValue as Array;
                    return array != null ? array.Length > 0 : false;
                }

                return false;
            }
            else if (!psvariable.TypeNames[0].EndsWith(nameof(PSVariable)))
            {
                return false;
            }

            var constantAllScope = ScopedItemOptions.AllScope | ScopedItemOptions.Constant;
            var readonlyAllScope = ScopedItemOptions.AllScope | ScopedItemOptions.ReadOnly;

            if (((variableScope & constantAllScope) == constantAllScope) ||
                ((variableScope & readonlyAllScope) == readonlyAllScope))
            {
                string prefixedVariableName = VariableDetails.DollarPrefix + variableName;
                if (this.globalScopeVariables.Children.ContainsKey(prefixedVariableName))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task FetchStackFrames(string scriptNameOverride)
        {
            PSCommand psCommand = new PSCommand();

            // This glorious hack ensures that Get-PSCallStack returns a list of CallStackFrame
            // objects (or "deserialized" CallStackFrames) when attached to a runspace in another
            // process.  Without the intermediate variable Get-PSCallStack inexplicably returns
            // an array of strings containing the formatted output of the CallStackFrame list.
            var callStackVarName = $"$global:{PsesGlobalVariableNamePrefix}CallStack";
            psCommand.AddScript($"{callStackVarName} = Get-PSCallStack; {callStackVarName}");

            var results = await this.powerShellContext.ExecuteCommand<PSObject>(psCommand);

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

                // When debugging, this is the best way I can find to get what is likely the workspace root.
                // This is controlled by the "cwd:" setting in the launch config.
                string workspaceRootPath = this.powerShellContext.InitialWorkingDirectory;

                this.stackFrameDetails[i] =
                    StackFrameDetails.Create(callStackFrames[i], autoVariables, localVariables, workspaceRootPath);

                string stackFrameScriptPath = this.stackFrameDetails[i].ScriptPath;
                if (scriptNameOverride != null &&
                    string.Equals(stackFrameScriptPath, StackFrameDetails.NoFileScriptPath))
                {
                    this.stackFrameDetails[i].ScriptPath = scriptNameOverride;
                }
                else if (this.powerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote &&
                    this.remoteFileManager != null &&
                    !string.Equals(stackFrameScriptPath, StackFrameDetails.NoFileScriptPath))
                {
                    this.stackFrameDetails[i].ScriptPath =
                        this.remoteFileManager.GetMappedPath(
                            stackFrameScriptPath,
                            this.powerShellContext.CurrentRunspace);
                }
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
                if (!(String.IsNullOrWhiteSpace(breakpoint.HitCondition)))
                {
                    int parsedHitCount;

                    if (Int32.TryParse(breakpoint.HitCondition, out parsedHitCount))
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
                if (hitCount.HasValue && String.IsNullOrWhiteSpace(breakpoint.Condition))
                {
                    // In the HitCount only case, this is simple as we can just use the HitCount
                    // property on the breakpoint object which is represented by $_.
                    string action = $"if ($_.HitCount -eq {hitCount}) {{ break }}";
                    actionScriptBlock = ScriptBlock.Create(action);
                }
                else if (!String.IsNullOrWhiteSpace(breakpoint.Condition))
                {
                    // Must be either condition only OR condition and hit count.
                    actionScriptBlock = ScriptBlock.Create(breakpoint.Condition);

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
                        string wrappedCondition;

                        if (hitCount.HasValue)
                        {
                            string globalHitCountVarName =
                                $"$global:{PsesGlobalVariableNamePrefix}BreakHitCounter_{breakpointHitCounter++}";

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
                    this.logger.Write(LogLevel.Warning, "No condition and no hit count specified by caller.");
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

        private string TrimScriptListingLine(PSObject scriptLineObj, ref int prefixLength)
        {
            string scriptLine = scriptLineObj.ToString();

            if (!string.IsNullOrWhiteSpace(scriptLine))
            {
                if (prefixLength == 0)
                {
                    // The prefix is a padded integer ending with ':', an asterisk '*'
                    // if this is the current line, and one character of padding
                    prefixLength = scriptLine.IndexOf(':') + 2;
                }

                return scriptLine.Substring(prefixLength);
            }

            return null;
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the debugger stops execution at a breakpoint or when paused.
        /// </summary>
        public event EventHandler<DebuggerStoppedEventArgs> DebuggerStopped;

        private async void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            bool noScriptName = false;
            string localScriptPath = e.InvocationInfo.ScriptName;

            // If there's no ScriptName, get the "list" of the current source
            if (this.remoteFileManager != null && string.IsNullOrEmpty(localScriptPath))
            {
                // Get the current script listing and create the buffer
                PSCommand command = new PSCommand();
                command.AddScript($"list 1 {int.MaxValue}");

                IEnumerable<PSObject> scriptListingLines =
                    await this.powerShellContext.ExecuteCommand<PSObject>(
                        command, false, false);

                if (scriptListingLines != null)
                {
                    int linePrefixLength = 0;

                    string scriptListing =
                        string.Join(
                            Environment.NewLine,
                            scriptListingLines
                                .Select(o => this.TrimScriptListingLine(o, ref linePrefixLength))
                                .Where(s => s != null));

                    this.temporaryScriptListingPath =
                        this.remoteFileManager.CreateTemporaryFile(
                            $"[{this.powerShellContext.CurrentRunspace.SessionDetails.ComputerName}] {TemporaryScriptFileName}",
                            scriptListing,
                            this.powerShellContext.CurrentRunspace);

                    localScriptPath =
                        this.temporaryScriptListingPath
                        ?? StackFrameDetails.NoFileScriptPath;

                    noScriptName = localScriptPath != null;
                }
                else
                {
                    this.logger.Write(
                        LogLevel.Warning,
                        $"Could not load script context");
                }
            }

            // Get call stack and variables.
            await this.FetchStackFramesAndVariables(
                noScriptName ? localScriptPath : null);

            // If this is a remote connection and the debugger stopped at a line
            // in a script file, get the file contents
            if (this.powerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote &&
                this.remoteFileManager != null &&
                !noScriptName)
            {
                localScriptPath =
                    await this.remoteFileManager.FetchRemoteFile(
                        e.InvocationInfo.ScriptName,
                        this.powerShellContext.CurrentRunspace);
            }

            if (this.stackFrameDetails.Length > 0)
            {
                // Augment the top stack frame with details from the stop event
                IScriptExtent scriptExtent =
                    this.invocationTypeScriptPositionProperty
                        .GetValue(e.InvocationInfo) as IScriptExtent;

                if (scriptExtent != null)
                {
                    this.stackFrameDetails[0].StartLineNumber = scriptExtent.StartLineNumber;
                    this.stackFrameDetails[0].EndLineNumber = scriptExtent.EndLineNumber;
                    this.stackFrameDetails[0].StartColumnNumber = scriptExtent.StartColumnNumber;
                    this.stackFrameDetails[0].EndColumnNumber = scriptExtent.EndColumnNumber;
                }
            }

            this.CurrentDebuggerStoppedEventArgs =
                new DebuggerStoppedEventArgs(
                    e,
                    this.powerShellContext.CurrentRunspace,
                    localScriptPath);

            // Notify the host that the debugger is stopped
            this.DebuggerStopped?.Invoke(
                sender,
                this.CurrentDebuggerStoppedEventArgs);
        }

        private void OnDebuggerResumed(object sender, DebuggerResumeAction e)
        {
            this.CurrentDebuggerStoppedEventArgs = null;
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

                string scriptPath = lineBreakpoint.Script;
                if (this.powerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote &&
                    this.remoteFileManager != null)
                {
                    string mappedPath =
                        this.remoteFileManager.GetMappedPath(
                            scriptPath,
                            this.powerShellContext.CurrentRunspace);

                    if (mappedPath == null)
                    {
                        this.logger.Write(
                            LogLevel.Error,
                            $"Could not map remote path '{scriptPath}' to a local path.");

                        return;
                    }

                    scriptPath = mappedPath;
                }

                // Normalize the script filename for proper indexing
                string normalizedScriptName = scriptPath.ToLower();

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
