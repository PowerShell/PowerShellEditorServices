﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Provides a high-level service for interacting with the
    /// PowerShell debugger in the runspace managed by a PowerShellContext.
    /// </summary>
    internal class DebugService
    {
        #region Fields

        private const string PsesGlobalVariableNamePrefix = "__psEditorServices_";
        private const string TemporaryScriptFileName = "Script Listing.ps1";

        private readonly BreakpointDetails[] s_emptyBreakpointDetailsArray = Array.Empty<BreakpointDetails>();

        private readonly ILogger _logger;
        private readonly PowerShellExecutionService _executionService;
        private readonly BreakpointService _breakpointService;
        private readonly RemoteFileManagerService remoteFileManager;

        private readonly InternalHost _psesHost;

        private readonly IPowerShellDebugContext _debugContext;

        private int nextVariableId;
        private string temporaryScriptListingPath;
        private List<VariableDetailsBase> variables;
        private VariableContainerDetails globalScopeVariables;
        private VariableContainerDetails scriptScopeVariables;
        private StackFrameDetails[] stackFrameDetails;
        private readonly PropertyInfo invocationTypeScriptPositionProperty;

        private static int breakpointHitCounter;

        private readonly SemaphoreSlim debugInfoHandle = AsyncUtils.CreateSimpleLockingSemaphore();
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
        public bool IsDebuggerStopped => _debugContext.IsStopped;

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
        //public DebugService(PowerShellContextService powerShellContext, ILogger logger)
        //    : this(powerShellContext, null, logger)
        //{
        //}

        /// <summary>
        /// Initializes a new instance of the DebugService class and uses
        /// the given PowerShellContext for all future operations.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext to use for all debugging operations.
        /// </param>
        //// <param name = "remoteFileManager" >
        //// A RemoteFileManagerService instance to use for accessing files in remote sessions.
        //// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public DebugService(
            PowerShellExecutionService executionService,
            IPowerShellDebugContext debugContext,
            RemoteFileManagerService remoteFileManager,
            BreakpointService breakpointService,
            InternalHost psesHost,
            ILoggerFactory factory)
        {
            Validate.IsNotNull(nameof(executionService), executionService);

            this._logger = factory.CreateLogger<DebugService>();
            _executionService = executionService;
            _breakpointService = breakpointService;
            _psesHost = psesHost;
            _debugContext = debugContext;
            _debugContext.DebuggerStopped += OnDebuggerStopAsync;
            _debugContext.DebuggerResuming += OnDebuggerResuming;
            _debugContext.BreakpointUpdated += OnBreakpointUpdated;

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
        public async Task<BreakpointDetails[]> SetLineBreakpointsAsync(
            ScriptFile scriptFile,
            BreakpointDetails[] breakpoints,
            bool clearExisting = true)
        {
            DscBreakpointCapability dscBreakpoints = await _debugContext.GetDscBreakpointCapabilityAsync(CancellationToken.None);

            string scriptPath = scriptFile.FilePath;
            // Make sure we're using the remote script path
            if (_psesHost.CurrentRunspace.IsOnRemoteMachine
                && this.remoteFileManager != null)
            {
                if (!this.remoteFileManager.IsUnderRemoteTempPath(scriptPath))
                {
                    this._logger.LogTrace(
                        $"Could not set breakpoints for local path '{scriptPath}' in a remote session.");

                    return s_emptyBreakpointDetailsArray;
                }

                string mappedPath =
                    this.remoteFileManager.GetMappedPath(
                        scriptPath,
                        _psesHost.CurrentRunspace);

                scriptPath = mappedPath;
            }
            else if (
                this.temporaryScriptListingPath != null &&
                this.temporaryScriptListingPath.Equals(scriptPath, StringComparison.CurrentCultureIgnoreCase))
            {
                this._logger.LogTrace(
                    $"Could not set breakpoint on temporary script listing path '{scriptPath}'.");

                return s_emptyBreakpointDetailsArray;
            }

            // Fix for issue #123 - file paths that contain wildcard chars [ and ] need to
            // quoted and have those wildcard chars escaped.
            string escapedScriptPath = PathUtils.WildcardEscapePath(scriptPath);

            if (dscBreakpoints == null || !dscBreakpoints.IsDscResourcePath(escapedScriptPath))
            {
                if (clearExisting)
                {
                    await _breakpointService.RemoveAllBreakpointsAsync(scriptFile.FilePath).ConfigureAwait(false);
                }

                return (await _breakpointService.SetBreakpointsAsync(escapedScriptPath, breakpoints).ConfigureAwait(false)).ToArray();
            }

            return await dscBreakpoints.SetLineBreakpointsAsync(
                _executionService,
                escapedScriptPath,
                breakpoints);
        }

        /// <summary>
        /// Sets the list of command breakpoints for the current debugging session.
        /// </summary>
        /// <param name="breakpoints">CommandBreakpointDetails for each command breakpoint that will be set.</param>
        /// <param name="clearExisting">If true, causes all existing function breakpoints to be cleared before setting new ones.</param>
        /// <returns>An awaitable Task that will provide details about the breakpoints that were set.</returns>
        public async Task<CommandBreakpointDetails[]> SetCommandBreakpointsAsync(
            CommandBreakpointDetails[] breakpoints,
            bool clearExisting = true)
        {
            CommandBreakpointDetails[] resultBreakpointDetails = null;

            if (clearExisting)
            {
                // Flatten dictionary values into one list and remove them all.
                await _breakpointService.RemoveBreakpointsAsync((await _breakpointService.GetBreakpointsAsync()).Where( i => i is CommandBreakpoint)).ConfigureAwait(false);
            }

            if (breakpoints.Length > 0)
            {
                resultBreakpointDetails = (await _breakpointService.SetCommandBreakpoints(breakpoints).ConfigureAwait(false)).ToArray();
            }

            return resultBreakpointDetails ?? new CommandBreakpointDetails[0];
        }

        /// <summary>
        /// Sends a "continue" action to the debugger when stopped.
        /// </summary>
        public void Continue()
        {
            _debugContext.Continue();
        }

        /// <summary>
        /// Sends a "step over" action to the debugger when stopped.
        /// </summary>
        public void StepOver()
        {
            _debugContext.StepOver();
        }

        /// <summary>
        /// Sends a "step in" action to the debugger when stopped.
        /// </summary>
        public void StepIn()
        {
            _debugContext.StepInto();
        }

        /// <summary>
        /// Sends a "step out" action to the debugger when stopped.
        /// </summary>
        public void StepOut()
        {
            _debugContext.StepOut();
        }

        /// <summary>
        /// Causes the debugger to break execution wherever it currently
        /// is at the time.  This is equivalent to clicking "Pause" in a
        /// debugger UI.
        /// </summary>
        public void Break()
        {
            _debugContext.BreakExecution();
        }

        /// <summary>
        /// Aborts execution of the debugger while it is running, even while
        /// it is stopped.  Equivalent to calling PowerShellContext.AbortExecution.
        /// </summary>
        public void Abort()
        {
            _debugContext.Abort();
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
            this.debugInfoHandle.Wait();
            try
            {
                if ((variableReferenceId < 0) || (variableReferenceId >= this.variables.Count))
                {
                    _logger.LogWarning($"Received request for variableReferenceId {variableReferenceId} that is out of range of valid indices.");
                    return Array.Empty<VariableDetailsBase>();
                }

                VariableDetailsBase parentVariable = this.variables[variableReferenceId];
                if (parentVariable.IsExpandable)
                {
                    childVariables = parentVariable.GetChildren(this._logger);
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
                    childVariables = Array.Empty<VariableDetailsBase>();
                }

                return childVariables;
            }
            finally
            {
                this.debugInfoHandle.Release();
            }
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
            IEnumerable<VariableDetailsBase> variableList;

            // Ensure debug info isn't currently being built.
            this.debugInfoHandle.Wait();
            try
            {
                variableList = this.variables;
            }
            finally
            {
                this.debugInfoHandle.Release();
            }

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
        public async Task<string> SetVariableAsync(int variableContainerReferenceId, string name, string value)
        {
            Validate.IsNotNull(nameof(name), name);
            Validate.IsNotNull(nameof(value), value);

            this._logger.LogTrace($"SetVariableRequest for '{name}' to value string (pre-quote processing): '{value}'");

            // An empty or whitespace only value is not a valid expression for SetVariable.
            if (value.Trim().Length == 0)
            {
                throw new InvalidPowerShellExpressionException("Expected an expression.");
            }

            // Evaluate the expression to get back a PowerShell object from the expression string.
            // This may throw, in which case the exception is propagated to the caller
            PSCommand evaluateExpressionCommand = new PSCommand().AddScript(value);
            object expressionResult = (await _executionService.ExecutePSCommandAsync<object>(evaluateExpressionCommand, CancellationToken.None)).FirstOrDefault();

            // If PowerShellContext.ExecuteCommand returns an ErrorRecord as output, the expression failed evaluation.
            // Ideally we would have a separate means from communicating error records apart from normal output.
            if (expressionResult is ErrorRecord errorRecord)
            {
                throw new InvalidPowerShellExpressionException(errorRecord.ToString());
            }

            // OK, now we have a PS object from the supplied value string (expression) to assign to a variable.
            // Get the variable referenced by variableContainerReferenceId and variable name.
            VariableContainerDetails variableContainer = null;
            await this.debugInfoHandle.WaitAsync().ConfigureAwait(false);
            try
            {
                variableContainer = (VariableContainerDetails)this.variables[variableContainerReferenceId];
            }
            finally
            {
                this.debugInfoHandle.Release();
            }

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
                StackFrameDetails[] stackFrames = await this.GetStackFramesAsync().ConfigureAwait(false);
                for (int i = 0; i < stackFrames.Length; i++)
                {
                    var stackFrame = stackFrames[i];
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
            var getVariableCommand = new PSCommand()
                .AddCommand(@"Microsoft.PowerShell.Utility\Get-Variable")
                .AddParameter("Name", name.TrimStart('$'))
                .AddParameter("Scope", scope);

            PSVariable psVariable = (await _executionService.ExecutePSCommandAsync<PSVariable>(getVariableCommand, CancellationToken.None).ConfigureAwait(false)).FirstOrDefault();
            if (psVariable == null)
            {
                throw new Exception($"Failed to retrieve PSVariable object for '{name}' from scope '{scope}'.");
            }

            // We have the PSVariable object for the variable the user wants to set and an object to assign to that variable.
            // The last step is to determine whether the PSVariable is "strongly typed" which may require a conversion.
            // If it is not strongly typed, we simply assign the object directly to the PSVariable potentially changing its type.
            // Turns out ArgumentTypeConverterAttribute is not public. So we call the attribute through it's base class -
            // ArgumentTransformationAttribute.
            ArgumentTransformationAttribute argTypeConverterAttr = null;
            foreach (Attribute variableAttribute in psVariable.Attributes)
            {
                if (variableAttribute is ArgumentTransformationAttribute argTransformAttr
                    && argTransformAttr.GetType().Name.Equals("ArgumentTypeConverterAttribute"))
                {
                    argTypeConverterAttr = argTransformAttr;
                    break;
                }
            }

            if (argTypeConverterAttr != null)
            {
                _logger.LogTrace($"Setting variable '{name}' using conversion to value: {expressionResult ?? "<null>"}");

                psVariable.Value = await _executionService.ExecuteDelegateAsync<object>(
                    "PS debugger argument converter",
                    ExecutionOptions.Default,
                    CancellationToken.None,
                    (pwsh, cancellationToken) =>
                    {
                        var engineIntrinsics = (EngineIntrinsics)pwsh.Runspace.SessionStateProxy.GetVariable("ExecutionContext");

                        // TODO: This is almost (but not quite) the same as LanguagePrimitives.Convert(), which does not require the pipeline thread.
                        //       We should investigate changing it.
                        return argTypeConverterAttr.Transform(engineIntrinsics, expressionResult);

                    }).ConfigureAwait(false);

            }
            else
            {
                // PSVariable is *not* strongly typed. In this case, whack the old value with the new value.
                _logger.LogTrace($"Setting variable '{name}' directly to value: {expressionResult ?? "<null>"} - previous type was {psVariable.Value?.GetType().Name ?? "<unknown>"}");
                psVariable.Value = expressionResult;
            }

            // Use the VariableDetails.ValueString functionality to get the string representation for client debugger.
            // This makes the returned string consistent with the strings normally displayed for variables in the debugger.
            var tempVariable = new VariableDetails(psVariable);
            _logger.LogTrace($"Set variable '{name}' to: {tempVariable.ValueString ?? "<null>"}");
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
        public async Task<VariableDetails> EvaluateExpressionAsync(
            string expressionString,
            int stackFrameId,
            bool writeResultAsOutput)
        {
            var command = new PSCommand().AddScript(expressionString);
            IReadOnlyList<PSObject> results = await _executionService.ExecutePSCommandAsync<PSObject>(
                command,
                CancellationToken.None,
                new PowerShellExecutionOptions { WriteOutputToHost = true }).ConfigureAwait(false);

            // Since this method should only be getting invoked in the debugger,
            // we can assume that Out-String will be getting used to format results
            // of command executions into string output.  However, if null is returned
            // then return null so that no output gets displayed.
            if (writeResultAsOutput || results == null || results.Count == 0)
            {
                return null;
            }

            // If we didn't write output,
            // return a VariableDetails instance.
            return new VariableDetails(
                    expressionString,
                    string.Join(Environment.NewLine, results));
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
            this.debugInfoHandle.Wait();
            try
            {
                return this.stackFrameDetails;
            }
            finally
            {
                this.debugInfoHandle.Release();
            }
        }

        internal StackFrameDetails[] GetStackFrames(CancellationToken cancellationToken)
        {
            this.debugInfoHandle.Wait(cancellationToken);
            try
            {
                return this.stackFrameDetails;
            }
            finally
            {
                this.debugInfoHandle.Release();
            }
        }

        internal async Task<StackFrameDetails[]> GetStackFramesAsync()
        {
            await this.debugInfoHandle.WaitAsync().ConfigureAwait(false);
            try
            {
                return this.stackFrameDetails;
            }
            finally
            {
                this.debugInfoHandle.Release();
            }
        }

        internal async Task<StackFrameDetails[]> GetStackFramesAsync(CancellationToken cancellationToken)
        {
            await this.debugInfoHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return this.stackFrameDetails;
            }
            finally
            {
                this.debugInfoHandle.Release();
            }
        }

        /// <summary>
        /// Gets the list of variable scopes for the stack frame that
        /// is identified by the given ID.
        /// </summary>
        /// <param name="stackFrameId">The ID of the stack frame at which variable scopes should be retrieved.</param>
        /// <returns>The list of VariableScope instances which describe the available variable scopes.</returns>
        public VariableScope[] GetVariableScopes(int stackFrameId)
        {
            var stackFrames = this.GetStackFrames();
            int localStackFrameVariableId = stackFrames[stackFrameId].LocalVariables.Id;
            int autoVariablesId = stackFrames[stackFrameId].AutoVariables.Id;

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

        private async Task FetchStackFramesAndVariablesAsync(string scriptNameOverride)
        {
            await this.debugInfoHandle.WaitAsync().ConfigureAwait(false);
            try
            {
                this.nextVariableId = VariableDetailsBase.FirstVariableId;
                this.variables = new List<VariableDetailsBase>
                {

                    // Create a dummy variable for index 0, should never see this.
                    new VariableDetails("Dummy", null)
                };

                // Must retrieve global/script variales before stack frame variables
                // as we check stack frame variables against globals.
                await FetchGlobalAndScriptVariablesAsync().ConfigureAwait(false);
                await FetchStackFramesAsync(scriptNameOverride).ConfigureAwait(false);
            }
            finally
            {
                this.debugInfoHandle.Release();
            }
        }

        private async Task FetchGlobalAndScriptVariablesAsync()
        {
            // Retrieve globals first as script variable retrieval needs to search globals.
            this.globalScopeVariables =
                await FetchVariableContainerAsync(VariableContainerDetails.GlobalScopeName, null).ConfigureAwait(false);

            this.scriptScopeVariables =
                await FetchVariableContainerAsync(VariableContainerDetails.ScriptScopeName, null).ConfigureAwait(false);
        }

        private async Task<VariableContainerDetails> FetchVariableContainerAsync(
            string scope,
            VariableContainerDetails autoVariables)
        {
            PSCommand psCommand = new PSCommand()
                .AddCommand("Get-Variable")
                .AddParameter("Scope", scope);

            var scopeVariableContainer = new VariableContainerDetails(this.nextVariableId++, "Scope: " + scope);
            this.variables.Add(scopeVariableContainer);

            IReadOnlyList<PSObject> results = await _executionService.ExecutePSCommandAsync<PSObject>(psCommand, CancellationToken.None)
                .ConfigureAwait(false);

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
                    this._logger.LogWarning(
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
                    return variableValue is Array array
                        && array.Length > 0;
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

        private async Task FetchStackFramesAsync(string scriptNameOverride)
        {
            PSCommand psCommand = new PSCommand();

            // This glorious hack ensures that Get-PSCallStack returns a list of CallStackFrame
            // objects (or "deserialized" CallStackFrames) when attached to a runspace in another
            // process.  Without the intermediate variable Get-PSCallStack inexplicably returns
            // an array of strings containing the formatted output of the CallStackFrame list.
            var callStackVarName = $"$global:{PsesGlobalVariableNamePrefix}CallStack";
            psCommand.AddScript($"{callStackVarName} = Get-PSCallStack; {callStackVarName}");

            var results = await _executionService.ExecutePSCommandAsync<PSObject>(psCommand, CancellationToken.None).ConfigureAwait(false);

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
                    await FetchVariableContainerAsync(i.ToString(), autoVariables).ConfigureAwait(false);

                // When debugging, this is the best way I can find to get what is likely the workspace root.
                // This is controlled by the "cwd:" setting in the launch config.
                string workspaceRootPath = _psesHost.InitialWorkingDirectory;

                this.stackFrameDetails[i] =
                    StackFrameDetails.Create(callStackFrames[i], autoVariables, localVariables, workspaceRootPath);

                string stackFrameScriptPath = this.stackFrameDetails[i].ScriptPath;
                if (scriptNameOverride != null &&
                    string.Equals(stackFrameScriptPath, StackFrameDetails.NoFileScriptPath))
                {
                    this.stackFrameDetails[i].ScriptPath = scriptNameOverride;
                }
                else if (_psesHost.CurrentRunspace.IsOnRemoteMachine
                    && this.remoteFileManager != null
                    && !string.Equals(stackFrameScriptPath, StackFrameDetails.NoFileScriptPath))
                {
                    this.stackFrameDetails[i].ScriptPath =
                        this.remoteFileManager.GetMappedPath(
                            stackFrameScriptPath,
                            _psesHost.CurrentRunspace);
                }
            }
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

        internal async void OnDebuggerStopAsync(object sender, DebuggerStopEventArgs e)
        {
            bool noScriptName = false;
            string localScriptPath = e.InvocationInfo.ScriptName;

            // If there's no ScriptName, get the "list" of the current source
            if (this.remoteFileManager != null && string.IsNullOrEmpty(localScriptPath))
            {
                // Get the current script listing and create the buffer
                PSCommand command = new PSCommand();
                command.AddScript($"list 1 {int.MaxValue}");

                IReadOnlyList<PSObject> scriptListingLines =
                    await _executionService.ExecutePSCommandAsync<PSObject>(
                        command, CancellationToken.None).ConfigureAwait(false);

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
                            $"[{_psesHost.CurrentRunspace.SessionDetails.ComputerName}] {TemporaryScriptFileName}",
                            scriptListing,
                            _psesHost.CurrentRunspace);

                    localScriptPath =
                        this.temporaryScriptListingPath
                        ?? StackFrameDetails.NoFileScriptPath;

                    noScriptName = localScriptPath != null;
                }
                else
                {
                    this._logger.LogWarning($"Could not load script context");
                }
            }

            // Get call stack and variables.
            await this.FetchStackFramesAndVariablesAsync(
                noScriptName ? localScriptPath : null).ConfigureAwait(false);

            // If this is a remote connection and the debugger stopped at a line
            // in a script file, get the file contents
            if (_psesHost.CurrentRunspace.IsOnRemoteMachine
                && this.remoteFileManager != null
                && !noScriptName)
            {
                localScriptPath =
                    await this.remoteFileManager.FetchRemoteFileAsync(
                        e.InvocationInfo.ScriptName,
                        _psesHost.CurrentRunspace).ConfigureAwait(false);
            }

            if (this.stackFrameDetails.Length > 0)
            {
                // Augment the top stack frame with details from the stop event

                if (this.invocationTypeScriptPositionProperty
                        .GetValue(e.InvocationInfo) is IScriptExtent scriptExtent)
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
                    _psesHost.CurrentRunspace,
                    localScriptPath);

            // Notify the host that the debugger is stopped
            this.DebuggerStopped?.Invoke(
                sender,
                this.CurrentDebuggerStoppedEventArgs);
        }

        private void OnDebuggerResuming(object sender, DebuggerResumingEventArgs debuggerResumingEventArgs)
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
            if (e.Breakpoint is LineBreakpoint lineBreakpoint)
            {
                string scriptPath = lineBreakpoint.Script;
                if (_psesHost.CurrentRunspace.IsOnRemoteMachine
                    && this.remoteFileManager != null)
                {
                    string mappedPath =
                        this.remoteFileManager.GetMappedPath(
                            scriptPath,
                            _psesHost.CurrentRunspace);

                    if (mappedPath == null)
                    {
                        this._logger.LogError(
                            $"Could not map remote path '{scriptPath}' to a local path.");

                        return;
                    }

                    scriptPath = mappedPath;
                }

                // Normalize the script filename for proper indexing
                string normalizedScriptName = scriptPath.ToLower();

                // Get the list of breakpoints for this file
                if (!_breakpointService.BreakpointsPerFile.TryGetValue(normalizedScriptName, out HashSet<Breakpoint> breakpoints))
                {
                    breakpoints = new HashSet<Breakpoint>();
                    _breakpointService.BreakpointsPerFile.Add(
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
