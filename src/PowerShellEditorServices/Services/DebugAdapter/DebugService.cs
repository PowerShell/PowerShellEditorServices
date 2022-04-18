// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;

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

        private readonly ILogger _logger;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly BreakpointService _breakpointService;
        private readonly RemoteFileManagerService _remoteFileManager;

        private readonly PsesInternalHost _psesHost;

        private readonly IPowerShellDebugContext _debugContext;

        // The LSP protocol refers to variables by individual IDs, this is an iterator for that purpose.
        private int nextVariableId;
        private string temporaryScriptListingPath;
        private List<VariableDetailsBase> variables;
        private VariableContainerDetails globalScopeVariables;
        private VariableContainerDetails scriptScopeVariables;
        private VariableContainerDetails localScopeVariables;
        private StackFrameDetails[] stackFrameDetails;
        private readonly PropertyInfo invocationTypeScriptPositionProperty;

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

        /// <summary>
        /// Tracks whether we are running <c>Debug-Runspace</c> in an out-of-process runspace.
        /// </summary>
        public bool IsDebuggingRemoteRunspace
        {
            get => _debugContext.IsDebuggingRemoteRunspace;
            set => _debugContext.IsDebuggingRemoteRunspace = value;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the DebugService class and uses
        /// the given execution service for all future operations.
        /// </summary>
        public DebugService(
            IInternalPowerShellExecutionService executionService,
            IPowerShellDebugContext debugContext,
            RemoteFileManagerService remoteFileManager,
            BreakpointService breakpointService,
            PsesInternalHost psesHost,
            ILoggerFactory factory)
        {
            Validate.IsNotNull(nameof(executionService), executionService);

            _logger = factory.CreateLogger<DebugService>();
            _executionService = executionService;
            _breakpointService = breakpointService;
            _psesHost = psesHost;
            _debugContext = debugContext;
            _debugContext.DebuggerStopped += OnDebuggerStopAsync;
            _debugContext.DebuggerResuming += OnDebuggerResuming;
            _debugContext.BreakpointUpdated += OnBreakpointUpdated;
            _remoteFileManager = remoteFileManager;

            invocationTypeScriptPositionProperty =
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
            DscBreakpointCapability dscBreakpoints = await _debugContext.GetDscBreakpointCapabilityAsync(CancellationToken.None).ConfigureAwait(false);

            string scriptPath = scriptFile.FilePath;

            _psesHost.Runspace.ThrowCancelledIfUnusable();
            // Make sure we're using the remote script path
            if (_psesHost.CurrentRunspace.IsOnRemoteMachine && _remoteFileManager is not null)
            {
                if (!_remoteFileManager.IsUnderRemoteTempPath(scriptPath))
                {
                    _logger.LogTrace($"Could not set breakpoints for local path '{scriptPath}' in a remote session.");
                    return Array.Empty<BreakpointDetails>();
                }

                scriptPath = _remoteFileManager.GetMappedPath(scriptPath, _psesHost.CurrentRunspace);
            }
            else if (temporaryScriptListingPath?.Equals(scriptPath, StringComparison.CurrentCultureIgnoreCase) == true)
            {
                _logger.LogTrace($"Could not set breakpoint on temporary script listing path '{scriptPath}'.");
                return Array.Empty<BreakpointDetails>();
            }

            // Fix for issue #123 - file paths that contain wildcard chars [ and ] need to
            // quoted and have those wildcard chars escaped.
            string escapedScriptPath = PathUtils.WildcardEscapePath(scriptPath);

            if (dscBreakpoints?.IsDscResourcePath(escapedScriptPath) != true)
            {
                if (clearExisting)
                {
                    await _breakpointService.RemoveAllBreakpointsAsync(scriptFile.FilePath).ConfigureAwait(false);
                }

                return (await _breakpointService.SetBreakpointsAsync(escapedScriptPath, breakpoints).ConfigureAwait(false)).ToArray();
            }

            return await dscBreakpoints
                .SetLineBreakpointsAsync(_executionService, escapedScriptPath, breakpoints)
                .ConfigureAwait(false);
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
                IEnumerable<Breakpoint> existingBreakpoints = await _breakpointService.GetBreakpointsAsync().ConfigureAwait(false);
                await _breakpointService.RemoveBreakpointsAsync(existingBreakpoints.OfType<CommandBreakpoint>()).ConfigureAwait(false);
            }

            if (breakpoints.Length > 0)
            {
                resultBreakpointDetails = (await _breakpointService.SetCommandBreakpointsAsync(breakpoints).ConfigureAwait(false)).ToArray();
            }

            return resultBreakpointDetails ?? Array.Empty<CommandBreakpointDetails>();
        }

        /// <summary>
        /// Sends a "continue" action to the debugger when stopped.
        /// </summary>
        public void Continue() => _debugContext.Continue();

        /// <summary>
        /// Sends a "step over" action to the debugger when stopped.
        /// </summary>
        public void StepOver() => _debugContext.StepOver();

        /// <summary>
        /// Sends a "step in" action to the debugger when stopped.
        /// </summary>
        public void StepIn() => _debugContext.StepInto();

        /// <summary>
        /// Sends a "step out" action to the debugger when stopped.
        /// </summary>
        public void StepOut() => _debugContext.StepOut();

        /// <summary>
        /// Causes the debugger to break execution wherever it currently
        /// is at the time. This is equivalent to clicking "Pause" in a
        /// debugger UI.
        /// </summary>
        public void Break() => _debugContext.BreakExecution();

        /// <summary>
        /// Aborts execution of the debugger while it is running, even while
        /// it is stopped.  Equivalent to calling PowerShellContext.AbortExecution.
        /// </summary>
        public void Abort() => _debugContext.Abort();

        /// <summary>
        /// Gets the list of variables that are children of the scope or variable
        /// that is identified by the given referenced ID.
        /// </summary>
        /// <param name="variableReferenceId"></param>
        /// <returns>An array of VariableDetails instances which describe the requested variables.</returns>
        public VariableDetailsBase[] GetVariables(int variableReferenceId)
        {
            VariableDetailsBase[] childVariables;
            debugInfoHandle.Wait();
            try
            {
                if ((variableReferenceId < 0) || (variableReferenceId >= variables.Count))
                {
                    _logger.LogWarning($"Received request for variableReferenceId {variableReferenceId} that is out of range of valid indices.");
                    return Array.Empty<VariableDetailsBase>();
                }

                VariableDetailsBase parentVariable = variables[variableReferenceId];
                if (parentVariable.IsExpandable)
                {
                    childVariables = parentVariable.GetChildren(_logger);
                    foreach (VariableDetailsBase child in childVariables)
                    {
                        // Only add child if it hasn't already been added.
                        if (child.Id < 0)
                        {
                            child.Id = nextVariableId++;
                            variables.Add(child);
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
                debugInfoHandle.Release();
            }
        }

        /// <summary>
        /// Evaluates a variable expression in the context of the stopped
        /// debugger. This method decomposes the variable expression to
        /// walk the cached variable data for the specified stack frame.
        /// </summary>
        /// <param name="variableExpression">The variable expression string to evaluate.</param>
        /// <returns>A VariableDetailsBase object containing the result.</returns>
        public VariableDetailsBase GetVariableFromExpression(string variableExpression)
        {
            // NOTE: From a watch we will get passed expressions that are not naked variables references.
            // Probably the right way to do this would be to examine the AST of the expr before calling
            // this method to make sure it is a VariableReference. But for the most part, non-naked variable
            // references are very unlikely to find a matching variable e.g. "$i+5.2" will find no var matching "$i+5".

            // Break up the variable path
            string[] variablePathParts = variableExpression.Split('.');

            VariableDetailsBase resolvedVariable = null;
            IEnumerable<VariableDetailsBase> variableList;

            // Ensure debug info isn't currently being built.
            debugInfoHandle.Wait();
            try
            {
                variableList = variables;
            }
            finally
            {
                debugInfoHandle.Release();
            }

            foreach (string variableName in variablePathParts)
            {
                if (variableList is null)
                {
                    // If there are no children left to search, break out early.
                    return null;
                }

                resolvedVariable =
                    variableList.FirstOrDefault(
                        v =>
                            string.Equals(
                                v.Name,
                                variableName,
                                StringComparison.CurrentCultureIgnoreCase));

                if (resolvedVariable?.IsExpandable == true)
                {
                    // Continue by searching in this variable's children.
                    variableList = GetVariables(resolvedVariable.Id);
                }
            }

            return resolvedVariable;
        }

        /// <summary>
        /// Sets the specified variable by container variableReferenceId and variable name to the
        /// specified new value. If the variable cannot be set or converted to that value this
        /// method will throw InvalidPowerShellExpressionException, ArgumentTransformationMetadataException, or
        /// SessionStateUnauthorizedAccessException.
        /// </summary>
        /// <param name="variableContainerReferenceId">The container (Autos, Local, Script, Global) that holds the variable.</param>
        /// <param name="name">The name of the variable prefixed with $.</param>
        /// <param name="value">The new string value.  This value must not be null.  If you want to set the variable to $null
        /// pass in the string "$null".</param>
        /// <returns>The string representation of the value the variable was set to.</returns>
        /// <exception cref="InvalidPowerShellExpressionException"></exception>
        public async Task<string> SetVariableAsync(int variableContainerReferenceId, string name, string value)
        {
            Validate.IsNotNull(nameof(name), name);
            Validate.IsNotNull(nameof(value), value);

            _logger.LogTrace($"SetVariableRequest for '{name}' to value string (pre-quote processing): '{value}'");

            // An empty or whitespace only value is not a valid expression for SetVariable.
            if (value.Trim().Length == 0)
            {
                throw new InvalidPowerShellExpressionException("Expected an expression.");
            }

            // Evaluate the expression to get back a PowerShell object from the expression string.
            // This may throw, in which case the exception is propagated to the caller
            PSCommand evaluateExpressionCommand = new PSCommand().AddScript(value);
            IReadOnlyList<object> expressionResults = await _executionService.ExecutePSCommandAsync<object>(evaluateExpressionCommand, CancellationToken.None).ConfigureAwait(false);
            if (expressionResults.Count == 0)
            {
                throw new InvalidPowerShellExpressionException("Expected an expression result.");
            }
            object expressionResult = expressionResults[0];

            // If PowerShellContext.ExecuteCommand returns an ErrorRecord as output, the expression failed evaluation.
            // Ideally we would have a separate means from communicating error records apart from normal output.
            if (expressionResult is ErrorRecord errorRecord)
            {
                throw new InvalidPowerShellExpressionException(errorRecord.ToString());
            }

            await debugInfoHandle.WaitAsync().ConfigureAwait(false);
            try
            {
                // OK, now we have a PS object from the supplied value string (expression) to assign to a variable.
                // Get the variable referenced by variableContainerReferenceId and variable name.
                VariableContainerDetails variableContainer = (VariableContainerDetails)variables[variableContainerReferenceId];
            }
            finally
            {
                debugInfoHandle.Release();
            }

            // Determine scope in which the variable lives so we can pass it to `Get-Variable
            // -Scope`. The default is scope 0 which is safe because if a user is able to see a
            // variable in the debugger and so change it through this interface, it's either in the
            // top-most scope or in one of the following named scopes. The default scope is most
            // likely in the case of changing from the "auto variables" container.
            string scope = "0";
            // NOTE: This can't use a switch because the IDs aren't constant.
            if (variableContainerReferenceId == localScopeVariables.Id)
            {
                scope = VariableContainerDetails.LocalScopeName;
            }
            else if (variableContainerReferenceId == scriptScopeVariables.Id)
            {
                scope = VariableContainerDetails.ScriptScopeName;
            }
            else if (variableContainerReferenceId == globalScopeVariables.Id)
            {
                scope = VariableContainerDetails.GlobalScopeName;
            }

            // Now that we have the scope, get the associated PSVariable object for the variable to be set.
            PSCommand getVariableCommand = new PSCommand()
                .AddCommand(@"Microsoft.PowerShell.Utility\Get-Variable")
                .AddParameter("Name", name.TrimStart('$'))
                .AddParameter("Scope", scope);

            IReadOnlyList<PSVariable> psVariables = await _executionService.ExecutePSCommandAsync<PSVariable>(getVariableCommand, CancellationToken.None).ConfigureAwait(false);
            if (psVariables.Count == 0)
            {
                throw new Exception("Failed to retrieve PSVariables");
            }

            PSVariable psVariable = psVariables[0];
            if (psVariable is null)
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

            if (argTypeConverterAttr is not null)
            {
                // PSVariable *is* strongly typed, so we have to convert it.
                _logger.LogTrace($"Setting variable '{name}' using conversion to value: {expressionResult ?? "<null>"}");

                // NOTE: We use 'Get-Variable' here instead of 'SessionStateProxy.GetVariable()'
                // because we already have a pipeline running (the debugger) and the latter cannot
                // run concurrently (threw 'NoSessionStateProxyWhenPipelineInProgress').
                IReadOnlyList<EngineIntrinsics> results = await _executionService.ExecutePSCommandAsync<EngineIntrinsics>(
                    new PSCommand()
                        .AddCommand(@"Microsoft.PowerShell.Utility\Get-Variable")
                        .AddParameter("Name", "ExecutionContext")
                        .AddParameter("ValueOnly"),
                    CancellationToken.None).ConfigureAwait(false);
                EngineIntrinsics engineIntrinsics = results.Count > 0
                    ? results[0]
                    : throw new Exception("Couldn't get EngineIntrinsics!");

                // TODO: This is almost (but not quite) the same as 'LanguagePrimitives.Convert()',
                // which does not require the pipeline thread. We should investigate changing it.
                psVariable.Value = argTypeConverterAttr.Transform(engineIntrinsics, expressionResult);
            }
            else
            {
                // PSVariable is *not* strongly typed. In this case, whack the old value with the new value.
                _logger.LogTrace($"Setting variable '{name}' directly to value: {expressionResult ?? "<null>"} - previous type was {psVariable.Value?.GetType().Name ?? "<unknown>"}");
                psVariable.Value = expressionResult;
            }

            // Use the VariableDetails.ValueString functionality to get the string representation for client debugger.
            // This makes the returned string consistent with the strings normally displayed for variables in the debugger.
            VariableDetails tempVariable = new(psVariable);
            _logger.LogTrace($"Set variable '{name}' to: {tempVariable.ValueString ?? "<null>"}");
            return tempVariable.ValueString;
        }

        /// <summary>
        /// Evaluates an expression in the context of the stopped
        /// debugger.  This method will execute the specified expression
        /// PowerShellContext.
        /// </summary>
        /// <param name="expressionString">The expression string to execute.</param>
        /// <param name="writeResultAsOutput">
        /// If true, writes the expression result as host output rather than returning the results.
        /// In this case, the return value of this function will be null.</param>
        /// <returns>A VariableDetails object containing the result.</returns>
        public async Task<VariableDetails> EvaluateExpressionAsync(
            string expressionString,
            bool writeResultAsOutput)
        {
            PSCommand command = new PSCommand().AddScript(expressionString);
            IReadOnlyList<PSObject> results = await _executionService.ExecutePSCommandAsync<PSObject>(
                command,
                CancellationToken.None,
                new PowerShellExecutionOptions { WriteOutputToHost = writeResultAsOutput, ThrowOnError = !writeResultAsOutput }).ConfigureAwait(false);

            // Since this method should only be getting invoked in the debugger,
            // we can assume that Out-String will be getting used to format results
            // of command executions into string output.  However, if null is returned
            // then return null so that no output gets displayed.
            if (writeResultAsOutput || results is null || results.Count == 0)
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
            debugInfoHandle.Wait();
            try
            {
                return stackFrameDetails;
            }
            finally
            {
                debugInfoHandle.Release();
            }
        }

        internal StackFrameDetails[] GetStackFrames(CancellationToken cancellationToken)
        {
            debugInfoHandle.Wait(cancellationToken);
            try
            {
                return stackFrameDetails;
            }
            finally
            {
                debugInfoHandle.Release();
            }
        }

        internal async Task<StackFrameDetails[]> GetStackFramesAsync()
        {
            await debugInfoHandle.WaitAsync().ConfigureAwait(false);
            try
            {
                return stackFrameDetails;
            }
            finally
            {
                debugInfoHandle.Release();
            }
        }

        internal async Task<StackFrameDetails[]> GetStackFramesAsync(CancellationToken cancellationToken)
        {
            await debugInfoHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return stackFrameDetails;
            }
            finally
            {
                debugInfoHandle.Release();
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
            StackFrameDetails[] stackFrames = GetStackFrames();
            int autoVariablesId = stackFrames[stackFrameId].AutoVariables.Id;
            int commandVariablesId = stackFrames[stackFrameId].CommandVariables.Id;

            return new VariableScope[]
            {
                new VariableScope(autoVariablesId, VariableContainerDetails.AutoVariablesName),
                new VariableScope(commandVariablesId, VariableContainerDetails.CommandVariablesName),
                new VariableScope(localScopeVariables.Id, VariableContainerDetails.LocalScopeName),
                new VariableScope(scriptScopeVariables.Id, VariableContainerDetails.ScriptScopeName),
                new VariableScope(globalScopeVariables.Id, VariableContainerDetails.GlobalScopeName),
            };
        }

        #endregion

        #region Private Methods

        private async Task FetchStackFramesAndVariablesAsync(string scriptNameOverride)
        {
            await debugInfoHandle.WaitAsync().ConfigureAwait(false);
            try
            {
                nextVariableId = VariableDetailsBase.FirstVariableId;
                variables = new List<VariableDetailsBase>
                {
                    // Create a dummy variable for index 0, should never see this.
                    new VariableDetails("Dummy", null)
                };

                // Must retrieve in order of broadest to narrowest scope for efficient
                // deduplication: global, script, local.
                globalScopeVariables = await FetchVariableContainerAsync(VariableContainerDetails.GlobalScopeName).ConfigureAwait(false);
                scriptScopeVariables = await FetchVariableContainerAsync(VariableContainerDetails.ScriptScopeName).ConfigureAwait(false);
                localScopeVariables = await FetchVariableContainerAsync(VariableContainerDetails.LocalScopeName).ConfigureAwait(false);

                await FetchStackFramesAsync(scriptNameOverride).ConfigureAwait(false);
            }
            finally
            {
                debugInfoHandle.Release();
            }
        }

        private Task<VariableContainerDetails> FetchVariableContainerAsync(string scope) => FetchVariableContainerAsync(scope, autoVarsOnly: false);

        private async Task<VariableContainerDetails> FetchVariableContainerAsync(string scope, bool autoVarsOnly)
        {
            PSCommand psCommand = new PSCommand().AddCommand(@"Microsoft.PowerShell.Utility\Get-Variable").AddParameter("Scope", scope);

            VariableContainerDetails scopeVariableContainer = new(nextVariableId++, "Scope: " + scope);
            variables.Add(scopeVariableContainer);

            IReadOnlyList<PSObject> results;
            try
            {
                results = await _executionService.ExecutePSCommandAsync<PSObject>(psCommand, CancellationToken.None).ConfigureAwait(false);
            }
            // It's possible to be asked to run `Get-Variable -Scope N` where N is a number that
            // exceeds the available scopes. In this case, the command throws this exception, but
            // there's nothing we can do about it, nor can we know the number of scopes that exist,
            // and we shouldn't crash the debugger, so we just return no results instead. All other
            // exceptions should be thrown again.
            catch (CmdletInvocationException ex) when (ex.ErrorRecord.CategoryInfo.Reason.Equals("PSArgumentOutOfRangeException"))
            {
                results = null;
            }

            if (results is not null)
            {
                foreach (PSObject psVariableObject in results)
                {
                    // Under some circumstances, we seem to get variables back with no "Name" field
                    // We skip over those here.
                    if (psVariableObject.Properties["Name"] is null)
                    {
                        continue;
                    }
                    VariableInfo variableInfo = TryVariableInfo(psVariableObject);
                    if (variableInfo is null || !ShouldAddAsVariable(variableInfo))
                    {
                        continue;
                    }
                    if (autoVarsOnly && !ShouldAddToAutoVariables(variableInfo))
                    {
                        continue;
                    }

                    VariableDetails variableDetails = new(variableInfo.Variable) { Id = nextVariableId++ };
                    variables.Add(variableDetails);
                    scopeVariableContainer.Children.Add(variableDetails.Name, variableDetails);
                }
            }

            return scopeVariableContainer;
        }

        // This is a helper type for FetchStackFramesAsync to preserve the variable Type after deserialization.
        private record VariableInfo(string[] Types, PSVariable Variable);

        // Create a VariableInfo for both serialized and deserialized variables.
        private static VariableInfo TryVariableInfo(PSObject psObject)
        {
            if (psObject.TypeNames.Contains("System.Management.Automation.PSVariable"))
            {
                return new VariableInfo(psObject.TypeNames.ToArray(), psObject.BaseObject as PSVariable);
            }
            if (psObject.TypeNames.Contains("Deserialized.System.Management.Automation.PSVariable"))
            {
                // Rehydrate the relevant variable properties and recreate it.
                ScopedItemOptions options = (ScopedItemOptions)Enum.Parse(typeof(ScopedItemOptions), psObject.Properties["Options"].Value.ToString());
                PSVariable reconstructedVar = new(
                    psObject.Properties["Name"].Value.ToString(),
                    psObject.Properties["Value"].Value,
                    options
                );
                return new VariableInfo(psObject.TypeNames.ToArray(), reconstructedVar);
            }

            return null;
        }

        /// <summary>
        /// Filters out variables we don't care about such as built-ins
        /// </summary>
        private static bool ShouldAddAsVariable(VariableInfo variableInfo)
        {
            // Filter built-in constant or readonly variables like $true, $false, $null, etc.
            ScopedItemOptions variableScope = variableInfo.Variable.Options;
            const ScopedItemOptions constantAllScope = ScopedItemOptions.AllScope | ScopedItemOptions.Constant;
            const ScopedItemOptions readonlyAllScope = ScopedItemOptions.AllScope | ScopedItemOptions.ReadOnly;
            if (((variableScope & constantAllScope) == constantAllScope)
                || ((variableScope & readonlyAllScope) == readonlyAllScope))
            {
                return false;
            }

            if (variableInfo.Variable.Name switch { "null" => true, _ => false })
            {
                return false;
            }

            return true;
        }

        // This method curates variables that should be added to the "auto" view, which we define as variables that are
        // very likely to be contextually relevant to the user, in an attempt to reduce noise when debugging.
        // Variables not listed here can still be found in the other containers like local and script, this is
        // provided as a convenience.
        private bool ShouldAddToAutoVariables(VariableInfo variableInfo)
        {
            PSVariable variableToAdd = variableInfo.Variable;
            if (!ShouldAddAsVariable(variableInfo))
            {
                return false;
            }

            // Filter internal variables created by Powershell Editor Services.
            if (variableToAdd.Name.StartsWith(PsesGlobalVariableNamePrefix)
                || variableToAdd.Name.Equals("PSDebugContext"))
            {
                return false;
            }

            // Filter Global-Scoped variables. We first cast to VariableDetails to ensure the prefix
            // is added for purposes of comparison.
            VariableDetails variableToAddDetails = new(variableToAdd);
            if (globalScopeVariables.Children.ContainsKey(variableToAddDetails.Name))
            {
                return false;
            }

            // We curate a list of LocalVariables that, if they exist, should be displayed by default.
            if (variableInfo.Types[0].EndsWith("LocalVariable"))
            {
                return variableToAdd.Name switch
                {
                    "PSItem" or "_" or "" => true,
                    "args" or "input" => variableToAdd.Value is Array array && array.Length > 0,
                    "PSBoundParameters" => variableToAdd.Value is IDictionary dict && dict.Count > 0,
                    _ => false
                };
            }

            // Any other PSVariables that survive the above criteria should be included.
            return variableInfo.Types[0].EndsWith("PSVariable");
        }

        private async Task FetchStackFramesAsync(string scriptNameOverride)
        {
            // This glorious hack ensures that Get-PSCallStack returns a list of CallStackFrame
            // objects (or "deserialized" CallStackFrames) when attached to a runspace in another
            // process. Without the intermediate variable Get-PSCallStack inexplicably returns an
            // array of strings containing the formatted output of the CallStackFrame list. So we
            // run a script that builds the list of CallStackFrames and their variables.
            const string callStackVarName = $"$global:{PsesGlobalVariableNamePrefix}CallStack";
            const string getPSCallStack = $"Get-PSCallStack | ForEach-Object {{ [void]{callStackVarName}.Add(@($PSItem, $PSItem.GetFrameVariables())) }}";

            _psesHost.Runspace.ThrowCancelledIfUnusable();
            // If we're attached to a remote runspace, we need to serialize the list prior to
            // transport because the default depth is too shallow. From testing, we determined the
            // correct depth is 3. The script always calls `Get-PSCallStack`. In a local runspace, we
            // just return its results. In a remote runspace we serialize it first and then later
            // deserialize it.
            bool isRemoteRunspace = _psesHost.CurrentRunspace.Runspace.RunspaceIsRemote;
            string returnSerializedIfInRemoteRunspace = isRemoteRunspace
                ? $"[Management.Automation.PSSerializer]::Serialize({callStackVarName}, 3)"
                : callStackVarName;

            // PSObject is used here instead of the specific type because we get deserialized
            // objects from remote sessions and want a common interface.
            PSCommand psCommand = new PSCommand().AddScript($"[Collections.ArrayList]{callStackVarName} = @(); {getPSCallStack}; {returnSerializedIfInRemoteRunspace}");
            IReadOnlyList<PSObject> results = await _executionService.ExecutePSCommandAsync<PSObject>(psCommand, CancellationToken.None).ConfigureAwait(false);

            IEnumerable callStack = isRemoteRunspace
                ? (PSSerializer.Deserialize(results[0].BaseObject as string) as PSObject)?.BaseObject as IList
                : results;

            List<StackFrameDetails> stackFrameDetailList = new();
            bool isTopStackFrame = true;
            foreach (object callStackFrameItem in callStack)
            {
                // We have to use reflection to get the variable dictionary.
                IList callStackFrameComponents = (callStackFrameItem as PSObject)?.BaseObject as IList;
                PSObject callStackFrame = callStackFrameComponents[0] as PSObject;
                IDictionary callStackVariables = isRemoteRunspace
                    ? (callStackFrameComponents[1] as PSObject)?.BaseObject as IDictionary
                    : callStackFrameComponents[1] as IDictionary;

                VariableContainerDetails autoVariables = new(
                    nextVariableId++,
                    VariableContainerDetails.AutoVariablesName);

                variables.Add(autoVariables);

                VariableContainerDetails commandVariables = new(
                    nextVariableId++,
                    VariableContainerDetails.CommandVariablesName);

                variables.Add(commandVariables);

                foreach (DictionaryEntry entry in callStackVariables)
                {
                    VariableInfo psVarInfo = TryVariableInfo(new PSObject(entry.Value));
                    if (psVarInfo is null)
                    {
                        _logger.LogError("A object was received that is not a PSVariable object");
                        continue;
                    }

                    VariableDetails variableDetails = new(psVarInfo.Variable) { Id = nextVariableId++ };
                    variables.Add(variableDetails);

                    commandVariables.Children.Add(variableDetails.Name, variableDetails);

                    if (ShouldAddToAutoVariables(psVarInfo))
                    {
                        autoVariables.Children.Add(variableDetails.Name, variableDetails);
                    }
                }

                // If this is the top stack frame, we also want to add relevant local variables to
                // the "Auto" container (not to be confused with Automatic PowerShell variables).
                //
                // TODO: We can potentially use `Get-Variable -Scope x` to add relevant local
                // variables to other frames but frames and scopes are not perfectly analogous and
                // we'd need a way to detect things such as module borders and dot-sourced files.
                if (isTopStackFrame)
                {
                    VariableContainerDetails localScopeAutoVariables = await FetchVariableContainerAsync(VariableContainerDetails.LocalScopeName, autoVarsOnly: true).ConfigureAwait(false);
                    foreach (KeyValuePair<string, VariableDetailsBase> entry in localScopeAutoVariables.Children)
                    {
                        // NOTE: `TryAdd` doesn't work on `IDictionary`.
                        if (!autoVariables.Children.ContainsKey(entry.Key))
                        {
                            autoVariables.Children.Add(entry.Key, entry.Value);
                        }
                    }
                    isTopStackFrame = false;
                }

                StackFrameDetails stackFrameDetailsEntry = StackFrameDetails.Create(callStackFrame, autoVariables, commandVariables);
                string stackFrameScriptPath = stackFrameDetailsEntry.ScriptPath;

                if (scriptNameOverride is not null
                    && string.Equals(stackFrameScriptPath, StackFrameDetails.NoFileScriptPath))
                {
                    stackFrameDetailsEntry.ScriptPath = scriptNameOverride;
                }
                else if (_psesHost.CurrentRunspace.IsOnRemoteMachine
                    && _remoteFileManager is not null
                    && !string.Equals(stackFrameScriptPath, StackFrameDetails.NoFileScriptPath))
                {
                    stackFrameDetailsEntry.ScriptPath =
                        _remoteFileManager.GetMappedPath(stackFrameScriptPath, _psesHost.CurrentRunspace);
                }

                stackFrameDetailList.Add(stackFrameDetailsEntry);
            }

            stackFrameDetails = stackFrameDetailList.ToArray();
        }

        private static string TrimScriptListingLine(PSObject scriptLineObj, ref int prefixLength)
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
            try
            {
                bool noScriptName = false;
                string localScriptPath = e.InvocationInfo.ScriptName;

                // If there's no ScriptName, get the "list" of the current source
                if (_remoteFileManager is not null && string.IsNullOrEmpty(localScriptPath))
                {
                    // Get the current script listing and create the buffer
                    PSCommand command = new PSCommand().AddScript($"list 1 {int.MaxValue}");

                    IReadOnlyList<PSObject> scriptListingLines =
                        await _executionService.ExecutePSCommandAsync<PSObject>(
                            command, CancellationToken.None).ConfigureAwait(false);

                    if (scriptListingLines is not null)
                    {
                        int linePrefixLength = 0;

                        string scriptListing =
                            string.Join(
                                Environment.NewLine,
                                scriptListingLines
                                    .Select(o => TrimScriptListingLine(o, ref linePrefixLength))
                                    .Where(s => s is not null));

                        temporaryScriptListingPath =
                            _remoteFileManager.CreateTemporaryFile(
                                $"[{_psesHost.CurrentRunspace.SessionDetails.ComputerName}] {TemporaryScriptFileName}",
                                scriptListing,
                                _psesHost.CurrentRunspace);

                        localScriptPath =
                            temporaryScriptListingPath
                            ?? StackFrameDetails.NoFileScriptPath;

                        noScriptName = localScriptPath is not null;
                    }
                    else
                    {
                        _logger.LogWarning("Could not load script context");
                    }
                }

                // Get call stack and variables.
                await FetchStackFramesAndVariablesAsync(noScriptName ? localScriptPath : null).ConfigureAwait(false);

                // If this is a remote connection and the debugger stopped at a line
                // in a script file, get the file contents
                if (_psesHost.CurrentRunspace.IsOnRemoteMachine
                    && _remoteFileManager is not null
                    && !noScriptName)
                {
                    localScriptPath =
                        await _remoteFileManager.FetchRemoteFileAsync(
                            e.InvocationInfo.ScriptName,
                            _psesHost.CurrentRunspace).ConfigureAwait(false);
                }

                if (stackFrameDetails.Length > 0)
                {
                    // Augment the top stack frame with details from the stop event
                    if (invocationTypeScriptPositionProperty.GetValue(e.InvocationInfo) is IScriptExtent scriptExtent)
                    {
                        stackFrameDetails[0].StartLineNumber = scriptExtent.StartLineNumber;
                        stackFrameDetails[0].EndLineNumber = scriptExtent.EndLineNumber;
                        stackFrameDetails[0].StartColumnNumber = scriptExtent.StartColumnNumber;
                        stackFrameDetails[0].EndColumnNumber = scriptExtent.EndColumnNumber;
                    }
                }

                CurrentDebuggerStoppedEventArgs =
                    new DebuggerStoppedEventArgs(
                        e,
                        _psesHost.CurrentRunspace,
                        localScriptPath);

                // Notify the host that the debugger is stopped.
                DebuggerStopped?.Invoke(sender, CurrentDebuggerStoppedEventArgs);
            }
            catch (OperationCanceledException)
            {
                // Ignore, likely means that a remote runspace has closed.
            }
            catch (Exception exception)
            {
                // Log in a catch all so we don't crash the process.
                _logger.LogError(
                    exception,
                    "Error occurred while obtaining debug info. Message: {message}",
                    exception.Message);
            }
        }

        private void OnDebuggerResuming(object sender, DebuggerResumingEventArgs debuggerResumingEventArgs) => CurrentDebuggerStoppedEventArgs = null;

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
                // TODO: This could be either a path or a script block!
                string scriptPath = lineBreakpoint.Script;
                if (_psesHost.CurrentRunspace.IsOnRemoteMachine
                    && _remoteFileManager is not null)
                {
                    string mappedPath = _remoteFileManager.GetMappedPath(scriptPath, _psesHost.CurrentRunspace);

                    if (mappedPath is null)
                    {
                        _logger.LogError($"Could not map remote path '{scriptPath}' to a local path.");
                        return;
                    }

                    scriptPath = mappedPath;
                }

                // TODO: It is very strange that we use the path as the key, which it could also be
                // a script block.
                Validate.IsNotNullOrEmptyString(nameof(scriptPath), scriptPath);

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

            BreakpointUpdated?.Invoke(sender, e);
        }

        #endregion
    }
}
