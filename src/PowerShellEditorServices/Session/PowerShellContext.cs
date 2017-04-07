//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices
{
    using Session;
    using System.Management.Automation;
    using System.Management.Automation.Host;
    using System.Management.Automation.Runspaces;
    using Microsoft.PowerShell.EditorServices.Session.Capabilities;
    using System.IO;

    /// <summary>
    /// Manages the lifetime and usage of a PowerShell session.
    /// Handles nested PowerShell prompts and also manages execution of
    /// commands whether inside or outside of the debugger.
    /// </summary>
    public class PowerShellContext : IDisposable, IHostSupportsInteractiveSession
    {
        #region Fields

        private PowerShell powerShell;
        private bool ownsInitialRunspace;
        private RunspaceDetails initialRunspace;
        private SessionDetails mostRecentSessionDetails;

        private IConsoleHost consoleHost;
        private ProfilePaths profilePaths;
        private ConsoleServicePSHost psHost;

        private IVersionSpecificOperations versionSpecificOperations;

        private int pipelineThreadId;
        private TaskCompletionSource<DebuggerResumeAction> debuggerStoppedTask;
        private TaskCompletionSource<IPipelineExecutionRequest> pipelineExecutionTask;
        private TaskCompletionSource<IPipelineExecutionRequest> pipelineResultTask;

        private object runspaceMutex = new object();
        private AsyncQueue<RunspaceHandle> runspaceWaitQueue = new AsyncQueue<RunspaceHandle>();

        private Stack<RunspaceDetails> runspaceStack = new Stack<RunspaceDetails>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets a boolean that indicates whether the debugger is currently stopped,
        /// either at a breakpoint or because the user broke execution.
        /// </summary>
        public bool IsDebuggerStopped
        {
            get
            {
                return
                    this.debuggerStoppedTask != null &&
                    this.CurrentRunspace.Runspace.RunspaceAvailability != RunspaceAvailability.Available;
            }
        }

        /// <summary>
        /// Gets the current state of the session.
        /// </summary>
        public PowerShellContextState SessionState
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the PowerShell version details for the initial local runspace.
        /// </summary>
        public PowerShellVersionDetails LocalPowerShellVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets an IConsoleHost implementation for use in
        /// writing output to the console.
        /// </summary>
        internal IConsoleHost ConsoleHost
        {
            get { return this.consoleHost; }
            set
            {
                this.consoleHost = value;
                this.psHost.ConsoleHost = value;
            }
        }

        /// <summary>
        /// Gets details pertaining to the current runspace.
        /// </summary>
        public RunspaceDetails CurrentRunspace
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the prompt string for the current runspace.
        /// </summary>
        public string PromptString
        {
            get { return this.mostRecentSessionDetails.PromptString; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class and
        /// opens a runspace to be used for the session.
        /// </summary>
        public PowerShellContext() : this((HostDetails)null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class and
        /// opens a runspace to be used for the session.
        /// </summary>
        /// <param name="hostDetails">Provides details about the host application.</param>
        /// <param name="profilePaths">An object containing the profile paths for the session.</param>
        public PowerShellContext(HostDetails hostDetails, ProfilePaths profilePaths)
            : this(hostDetails, profilePaths, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class and
        /// opens a runspace to be used for the session.
        /// </summary>
        /// <param name="hostDetails">Provides details about the host application.</param>
        /// <param name="profilePaths">An object containing the profile paths for the session.</param>
        /// <param name="enableConsoleRepl">
        /// Enables a terminal-based REPL for this session.
        /// </param>
        public PowerShellContext(
            HostDetails hostDetails,
            ProfilePaths profilePaths,
            bool enableConsoleRepl)
        {
            hostDetails = hostDetails ?? HostDetails.Default;

            this.psHost = new ConsoleServicePSHost(hostDetails, this, enableConsoleRepl);
            var initialSessionState = InitialSessionState.CreateDefault2();

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, initialSessionState);
#if !CoreCLR
            runspace.ApartmentState = ApartmentState.STA;
#endif
            runspace.ThreadOptions = PSThreadOptions.ReuseThread;
            runspace.Open();

            this.ownsInitialRunspace = true;

            this.Initialize(profilePaths, runspace);
        }

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class using
        /// an existing runspace for the session.
        /// </summary>
        /// <param name="profilePaths">An object containing the profile paths for the session.</param>
        /// <param name="initialRunspace">The initial runspace to use for this instance.</param>
        public PowerShellContext(ProfilePaths profilePaths, Runspace initialRunspace)
        {
            this.Initialize(profilePaths, initialRunspace);
        }

        private void Initialize(ProfilePaths profilePaths, Runspace initialRunspace)
        {
            Validate.IsNotNull("initialRunspace", initialRunspace);

            this.SessionState = PowerShellContextState.NotStarted;

            // Get the PowerShell runtime version
            this.LocalPowerShellVersion =
                PowerShellVersionDetails.GetVersionDetails(
                    initialRunspace);

            this.powerShell = PowerShell.Create();
            this.powerShell.Runspace = initialRunspace;

            this.initialRunspace =
                new RunspaceDetails(
                    initialRunspace,
                    this.GetSessionDetailsInRunspace(initialRunspace),
                    this.LocalPowerShellVersion,
                    RunspaceLocation.Local,
                    RunspaceContext.Original,
                    null);
            this.CurrentRunspace = this.initialRunspace;

            // Write out the PowerShell version for tracking purposes
            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "PowerShell runtime version: {0}, edition: {1}",
                    this.LocalPowerShellVersion.Version,
                    this.LocalPowerShellVersion.Edition));

            Version powerShellVersion = this.LocalPowerShellVersion.Version;
            if (powerShellVersion >= new Version(5, 0))
            {
                this.versionSpecificOperations = new PowerShell5Operations();
            }
            else if (powerShellVersion.Major == 4)
            {
                this.versionSpecificOperations = new PowerShell4Operations();
            }
            else if (powerShellVersion.Major == 3)
            {
                this.versionSpecificOperations = new PowerShell3Operations();
            }
            else
            {
                throw new NotSupportedException(
                    "This computer has an unsupported version of PowerShell installed: " +
                    powerShellVersion.ToString());
            }

            if (this.LocalPowerShellVersion.Edition != "Linux")
            {
                // TODO: Should this be configurable?
                this.SetExecutionPolicy(ExecutionPolicy.RemoteSigned);
            }

            // Set up the runspace
            this.ConfigureRunspace(this.CurrentRunspace);

            // Add runspace capabilities
            this.ConfigureRunspaceCapabilities(this.CurrentRunspace);

            // Set the $profile variable in the runspace
            this.profilePaths = profilePaths;
            if (this.profilePaths != null)
            {
                this.SetProfileVariableInCurrentRunspace(profilePaths);
            }

            // Now that initialization is complete we can watch for InvocationStateChanged
            this.powerShell.InvocationStateChanged += powerShell_InvocationStateChanged;

            this.SessionState = PowerShellContextState.Ready;

            // Now that the runspace is ready, enqueue it for first use
            RunspaceHandle runspaceHandle = new RunspaceHandle(this);
            this.runspaceWaitQueue.EnqueueAsync(runspaceHandle).Wait();
        }

        private static bool CheckIfRunspaceNeedsEventHandlers(RunspaceDetails runspaceDetails)
        {
            // The only types of runspaces that need to be configured are:
            // - Locally created runspaces
            // - Local process entered with Enter-PSHostProcess
            // - Remote session entered with Enter-PSSession
            return
                (runspaceDetails.Location == RunspaceLocation.Local &&
                 (runspaceDetails.Context == RunspaceContext.Original ||
                  runspaceDetails.Context == RunspaceContext.EnteredProcess)) ||
                (runspaceDetails.Location == RunspaceLocation.Remote && runspaceDetails.Context == RunspaceContext.Original);
        }

        private void ConfigureRunspace(RunspaceDetails runspaceDetails)
        {
            runspaceDetails.Runspace.StateChanged += this.HandleRunspaceStateChanged;
            if (runspaceDetails.Runspace.Debugger != null)
            {
                runspaceDetails.Runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
                runspaceDetails.Runspace.Debugger.DebuggerStop += OnDebuggerStop;
            }

            this.versionSpecificOperations.ConfigureDebugger(runspaceDetails.Runspace);
        }

        private void CleanupRunspace(RunspaceDetails runspaceDetails)
        {
            runspaceDetails.Runspace.StateChanged -= this.HandleRunspaceStateChanged;
            if (runspaceDetails.Runspace.Debugger != null)
            {
                runspaceDetails.Runspace.Debugger.BreakpointUpdated -= OnBreakpointUpdated;
                runspaceDetails.Runspace.Debugger.DebuggerStop -= OnDebuggerStop;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a RunspaceHandle for the session's runspace.  This
        /// handle is used to gain temporary ownership of the runspace
        /// so that commands can be executed against it directly.
        /// </summary>
        /// <returns>A RunspaceHandle instance that gives access to the session's runspace.</returns>
        public Task<RunspaceHandle> GetRunspaceHandle()
        {
            return this.GetRunspaceHandle(CancellationToken.None);
        }

        /// <summary>
        /// Gets a RunspaceHandle for the session's runspace.  This
        /// handle is used to gain temporary ownership of the runspace
        /// so that commands can be executed against it directly.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the request.</param>
        /// <returns>A RunspaceHandle instance that gives access to the session's runspace.</returns>
        public Task<RunspaceHandle> GetRunspaceHandle(CancellationToken cancellationToken)
        {
            return this.runspaceWaitQueue.DequeueAsync(cancellationToken);
        }

        /// <summary>
        /// Executes a PSCommand against the session's runspace and returns
        /// a collection of results of the expected type.
        /// </summary>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <param name="psCommand">The PSCommand to be executed.</param>
        /// <param name="sendOutputToHost">
        /// If true, causes any output written during command execution to be written to the host.
        /// </param>
        /// <param name="sendErrorToHost">
        /// If true, causes any errors encountered during command execution to be written to the host.
        /// </param>
        /// <returns>
        /// An awaitable Task which will provide results once the command
        /// execution completes.
        /// </returns>
        public async Task<IEnumerable<TResult>> ExecuteCommand<TResult>(
            PSCommand psCommand,
            bool sendOutputToHost = false,
            bool sendErrorToHost = true)
        {
            return await ExecuteCommand<TResult>(psCommand, null, sendOutputToHost, sendErrorToHost);
        }

        /// <summary>
        /// Executes a PSCommand against the session's runspace and returns
        /// a collection of results of the expected type.
        /// </summary>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <param name="psCommand">The PSCommand to be executed.</param>
        /// <param name="errorMessages">Error messages from PowerShell will be written to the StringBuilder.</param>
        /// <param name="sendOutputToHost">
        /// If true, causes any output written during command execution to be written to the host.
        /// </param>
        /// <param name="sendErrorToHost">
        /// If true, causes any errors encountered during command execution to be written to the host.
        /// </param>
        /// <param name="addToHistory">
        /// If true, adds the command to the user's command history.
        /// </param>
        /// <returns>
        /// An awaitable Task which will provide results once the command
        /// execution completes.
        /// </returns>
        public Task<IEnumerable<TResult>> ExecuteCommand<TResult>(
            PSCommand psCommand,
            StringBuilder errorMessages,
            bool sendOutputToHost = false,
            bool sendErrorToHost = true,
            bool addToHistory = false)
        {
            return
                this.ExecuteCommand<TResult>(
                    psCommand,
                    errorMessages,
                    new ExecutionOptions
                    {
                        WriteOutputToHost = sendOutputToHost,
                        WriteErrorsToHost = sendErrorToHost,
                        AddToHistory = addToHistory
                    });
        }

        /// <summary>
        /// Executes a PSCommand against the session's runspace and returns
        /// a collection of results of the expected type.
        /// </summary>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <param name="psCommand">The PSCommand to be executed.</param>
        /// <param name="errorMessages">Error messages from PowerShell will be written to the StringBuilder.</param>
        /// <param name="executionOptions">Specifies options to be used when executing this command.</param>
        /// <returns>
        /// An awaitable Task which will provide results once the command
        /// execution completes.
        /// </returns>
        public async Task<IEnumerable<TResult>> ExecuteCommand<TResult>(
            PSCommand psCommand,
            StringBuilder errorMessages,
            ExecutionOptions executionOptions)
        {
            bool hadErrors = false;
            RunspaceHandle runspaceHandle = null;
            IEnumerable<TResult> executionResult = Enumerable.Empty<TResult>();

            // If the debugger is active and the caller isn't on the pipeline
            // thread, send the command over to that thread to be executed.
            if (Thread.CurrentThread.ManagedThreadId != this.pipelineThreadId &&
                this.pipelineExecutionTask != null)
            {
                Logger.Write(LogLevel.Verbose, "Passing command execution to pipeline thread.");

                PipelineExecutionRequest<TResult> executionRequest =
                    new PipelineExecutionRequest<TResult>(
                        this,
                        psCommand,
                        errorMessages,
                        executionOptions.WriteOutputToHost);

                // Send the pipeline execution request to the pipeline thread
                this.pipelineResultTask = new TaskCompletionSource<IPipelineExecutionRequest>();
                this.pipelineExecutionTask.SetResult(executionRequest);

                await this.pipelineResultTask.Task;
                return executionRequest.Results;
            }
            else
            {
                try
                {
                    // Instruct PowerShell to send output and errors to the host
                    if (executionOptions.WriteOutputToHost)
                    {
                        psCommand.Commands[0].MergeMyResults(
                            PipelineResultTypes.Error,
                            PipelineResultTypes.Output);

                        psCommand.Commands.Add(
                            this.GetOutputCommand(
                                endOfStatement: false));
                    }

                    this.OnExecutionStatusChanged(
                        ExecutionStatus.Running,
                        executionOptions,
                        false);

                    if (this.CurrentRunspace.Runspace.RunspaceAvailability == RunspaceAvailability.AvailableForNestedCommand ||
                        this.debuggerStoppedTask != null)
                    {
                        executionResult =
                            this.ExecuteCommandInDebugger<TResult>(
                                psCommand,
                                executionOptions.WriteOutputToHost);
                    }
                    else
                    {
                        Logger.Write(
                            LogLevel.Verbose,
                            string.Format(
                                "Attempting to execute command(s):\r\n\r\n{0}",
                                GetStringForPSCommand(psCommand)));

                        // Set the runspace
                        runspaceHandle = await this.GetRunspaceHandle();
                        if (runspaceHandle.Runspace.RunspaceAvailability != RunspaceAvailability.AvailableForNestedCommand)
                        {
                            this.powerShell.Runspace = runspaceHandle.Runspace;
                        }

                        // Invoke the pipeline on a background thread
                        // TODO: Use built-in async invocation!
                        executionResult =
                            await Task.Factory.StartNew<IEnumerable<TResult>>(
                                () =>
                                {
                                    Collection<TResult> result = null;
                                    try
                                    {
                                        this.powerShell.Commands = psCommand;

                                        PSInvocationSettings invocationSettings = new PSInvocationSettings();
                                        invocationSettings.AddToHistory = executionOptions.AddToHistory;
                                        result = this.powerShell.Invoke<TResult>(null, invocationSettings);
                                    }
                                    catch (RemoteException e)
                                    {
                                        if (!e.SerializedRemoteException.TypeNames[0].EndsWith("PipelineStoppedException"))
                                        {
                                            // Rethrow anything that isn't a PipelineStoppedException
                                            throw e;
                                        }
                                    }

                                    return result;
                                },
                                CancellationToken.None, // Might need a cancellation token
                                TaskCreationOptions.None,
                                TaskScheduler.Default
                            );

                        if (this.powerShell.HadErrors)
                        {
                            string errorMessage = "Execution completed with errors:\r\n\r\n";

                            foreach (var error in this.powerShell.Streams.Error)
                            {
                                errorMessage += error.ToString() + "\r\n";
                            }

                            errorMessages?.Append(errorMessage);
                            Logger.Write(LogLevel.Error, errorMessage);

                            hadErrors = true;
                        }
                        else
                        {
                            Logger.Write(
                                LogLevel.Verbose,
                                "Execution completed successfully.");
                        }
                    }
                }
                catch (PipelineStoppedException e)
                {
                    Logger.Write(
                        LogLevel.Error,
                        "Pipeline stopped while executing command:\r\n\r\n" + e.ToString());

                    errorMessages?.Append(e.Message);
                }
                catch (RuntimeException e)
                {
                    Logger.Write(
                        LogLevel.Error,
                        "Runtime exception occurred while executing command:\r\n\r\n" + e.ToString());

                    hadErrors = true;
                    errorMessages?.Append(e.Message);

                    if (executionOptions.WriteErrorsToHost)
                    {
                        // Write the error to the host
                        this.WriteExceptionToHost(e);
                    }
                }
                catch (Exception e)
                {
                    this.OnExecutionStatusChanged(
                        ExecutionStatus.Failed,
                        executionOptions,
                        true);

                    throw e;
                }
                finally
                {
                    // Get the new prompt before releasing the runspace handle
                    if (executionOptions.WriteOutputToHost)
                    {
                        SessionDetails sessionDetails = null;

                        // Get the SessionDetails and then write the prompt
                        if (this.CurrentRunspace.Runspace.RunspaceAvailability == RunspaceAvailability.Available)
                        {
                            // This state can happen if the user types a command that causes the
                            // debugger to exit before we reach this point.  No RunspaceHandle
                            // will exist already so we need to create one and then use it
                            if (runspaceHandle == null)
                            {
                                runspaceHandle = await this.GetRunspaceHandle();
                            }

                            sessionDetails = this.GetSessionDetailsInRunspace(runspaceHandle.Runspace);
                        }
                        else if (this.IsDebuggerStopped)
                        {
                            sessionDetails = this.GetSessionDetailsInDebugger();
                        }
                        else
                        {
                            sessionDetails = this.GetSessionDetailsInNestedPipeline();
                        }

                        // Check if the runspace has changed
                        this.UpdateRunspaceDetailsIfSessionChanged(sessionDetails);
                    }

                    // Dispose of the execution context
                    if (runspaceHandle != null)
                    {
                        runspaceHandle.Dispose();
                    }
                }
            }

            this.OnExecutionStatusChanged(
                ExecutionStatus.Completed,
                executionOptions,
                hadErrors);

            return executionResult;
        }

        /// <summary>
        /// Executes a PSCommand in the session's runspace without
        /// expecting to receive any result.
        /// </summary>
        /// <param name="psCommand">The PSCommand to be executed.</param>
        /// <returns>
        /// An awaitable Task that the caller can use to know when
        /// execution completes.
        /// </returns>
        public Task ExecuteCommand(PSCommand psCommand)
        {
            return this.ExecuteCommand<object>(psCommand);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptString(
            string scriptString)
        {
            return this.ExecuteScriptString(scriptString, false, true);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <param name="errorMessages">Error messages from PowerShell will be written to the StringBuilder.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptString(
            string scriptString,
            StringBuilder errorMessages)
        {
            return this.ExecuteScriptString(scriptString, errorMessages, false, true, false);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <param name="writeInputToHost">If true, causes the script string to be written to the host.</param>
        /// <param name="writeOutputToHost">If true, causes the script output to be written to the host.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptString(
            string scriptString,
            bool writeInputToHost,
            bool writeOutputToHost)
        {
            return this.ExecuteScriptString(scriptString, null, writeInputToHost, writeOutputToHost, false);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <param name="writeInputToHost">If true, causes the script string to be written to the host.</param>
        /// <param name="writeOutputToHost">If true, causes the script output to be written to the host.</param>
        /// <param name="addToHistory">If true, adds the command to the user's command history.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptString(
            string scriptString,
            bool writeInputToHost,
            bool writeOutputToHost,
            bool addToHistory)
        {
            return this.ExecuteScriptString(scriptString, null, writeInputToHost, writeOutputToHost, addToHistory);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <param name="errorMessages">Error messages from PowerShell will be written to the StringBuilder.</param>
        /// <param name="writeInputToHost">If true, causes the script string to be written to the host.</param>
        /// <param name="writeOutputToHost">If true, causes the script output to be written to the host.</param>
        /// <param name="addToHistory">If true, adds the command to the user's command history.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public async Task<IEnumerable<object>> ExecuteScriptString(
            string scriptString,
            StringBuilder errorMessages,
            bool writeInputToHost,
            bool writeOutputToHost,
            bool addToHistory)
        {
            if (writeInputToHost)
            {
                this.WriteOutput(
                    scriptString + Environment.NewLine,
                    true);
            }

            PSCommand psCommand = new PSCommand();
            psCommand.AddScript(scriptString);

            return
                await this.ExecuteCommand<object>(
                    psCommand,
                    errorMessages,
                    writeOutputToHost,
                    addToHistory: addToHistory);
        }

        /// <summary>
        /// Executes a script file at the specified path.
        /// </summary>
        /// <param name="script">The script execute.</param>
        /// <param name="arguments">Arguments to pass to the script.</param>
        /// <param name="writeInputToHost">Writes the executed script path and arguments to the host.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task ExecuteScriptWithArgs(string script, string arguments = null, bool writeInputToHost = false)
        {
            string launchedScript = script;
            PSCommand command = new PSCommand();

            if (arguments != null)
            {
                // Need to determine If the script string is a path to a script file.
                string scriptAbsPath = string.Empty;
                try
                {
                    // Assume we can only debug scripts from the FileSystem provider
                    string workingDir =
                        this.CurrentRunspace.Runspace.SessionStateProxy.Path.CurrentFileSystemLocation.ProviderPath;
                    workingDir = workingDir.TrimEnd(Path.DirectorySeparatorChar);
                    scriptAbsPath = workingDir + Path.DirectorySeparatorChar + script;
                }
                catch (System.Management.Automation.DriveNotFoundException e)
                {
                    Logger.Write(
                        LogLevel.Error,
                        "Could not determine current filesystem location:\r\n\r\n" + e.ToString());
                }

                // If we don't escape wildcard characters in a path to a script file, the script can
                // fail to execute if say the script filename was foo][.ps1.
                // Related to issue #123.
                if (File.Exists(script) || File.Exists(scriptAbsPath))
                {
                    // Dot-source the launched script path
                    script = ". " + EscapePath(script, escapeSpaces: true);
                }

                launchedScript = script + " " + arguments;
                command.AddScript(launchedScript, false);
            }
            else
            {
                command.AddCommand(script, false);
            }

            if (writeInputToHost)
            {
                this.WriteOutput(
                    launchedScript + Environment.NewLine,
                    true);
            }

            await this.ExecuteCommand<object>(
                command,
                null,
                sendOutputToHost: true,
                addToHistory: true);
        }

        internal static TResult ExecuteScriptAndGetItem<TResult>(string scriptToExecute, Runspace runspace, TResult defaultValue = default(TResult))
        {
            Pipeline pipeline = null;

            try
            {
                if (runspace.RunspaceAvailability == RunspaceAvailability.AvailableForNestedCommand)
                {
                    pipeline = runspace.CreateNestedPipeline(scriptToExecute, false);
                }
                else
                {
                    pipeline = runspace.CreatePipeline(scriptToExecute, false);
                }

                Collection<PSObject> results = pipeline.Invoke();

                if (results.Count == 0)
                {
                    return defaultValue;
                }

                if (typeof(TResult) != typeof(PSObject))
                {
                    return
                       results
                            .Select(pso => pso.BaseObject)
                            .OfType<TResult>()
                            .FirstOrDefault();
                }
                else
                {
                    return
                        results
                            .OfType<TResult>()
                            .FirstOrDefault();
                }
            }
            finally
            {
                pipeline.Dispose();
            }
        }

        /// <summary>
        /// Loads PowerShell profiles for the host from the specified
        /// profile locations.  Only the profile paths which exist are
        /// loaded.
        /// </summary>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task LoadHostProfiles()
        {
            if (this.profilePaths != null)
            {
                // Load any of the profile paths that exist
                PSCommand command = null;
                foreach (var profilePath in this.profilePaths.GetLoadableProfilePaths())
                {
                    command = new PSCommand();
                    command.AddCommand(profilePath, false);
                    await this.ExecuteCommand(command);
                }

                // Gather the session details (particularly the prompt) after
                // loading the user's profiles.
                await this.GetSessionDetailsInRunspace();
            }
        }

        /// <summary>
        /// Causes the current execution to be aborted no matter what state
        /// it is currently in.
        /// </summary>
        public void AbortExecution()
        {
            if (this.SessionState != PowerShellContextState.Aborting &&
                this.SessionState != PowerShellContextState.Disposed)
            {
                Logger.Write(LogLevel.Verbose, "Execution abort requested...");

                // Clean up the debugger
                if (this.IsDebuggerStopped)
                {
                    this.ResumeDebugger(DebuggerResumeAction.Stop);
                    this.debuggerStoppedTask = null;
                    this.pipelineExecutionTask = null;
                }

                // Stop the running pipeline
                this.powerShell.BeginStop(null, null);

                this.SessionState = PowerShellContextState.Aborting;
            }
            else
            {
                Logger.Write(
                    LogLevel.Verbose,
                    string.Format(
                        $"Execution abort requested when already aborted (SessionState = {this.SessionState})"));
            }
        }

        /// <summary>
        /// Causes the debugger to break execution wherever it currently is.
        /// This method is internal because the real Break API is provided
        /// by the DebugService.
        /// </summary>
        internal void BreakExecution()
        {
            Logger.Write(LogLevel.Verbose, "Debugger break requested...");

            // Pause the debugger
            this.versionSpecificOperations.PauseDebugger(
                this.CurrentRunspace.Runspace);
        }

        internal void ResumeDebugger(DebuggerResumeAction resumeAction)
        {
            if (this.debuggerStoppedTask != null)
            {
                // Set the result so that the execution thread resumes.
                // The execution thread will clean up the task.
                if (!this.debuggerStoppedTask.TrySetResult(resumeAction))
                {
                    Logger.Write(
                        LogLevel.Error,
                        $"Tried to resume debugger with action {resumeAction} but the task was already completed.");
                }
            }
            else
            {
                Logger.Write(
                    LogLevel.Error,
                    $"Tried to resume debugger with action {resumeAction} but there was no debuggerStoppedTask.");
            }
        }

        /// <summary>
        /// Disposes the runspace and any other resources being used
        /// by this PowerShellContext.
        /// </summary>
        public void Dispose()
        {
            // Do we need to abort a running execution?
            if (this.SessionState == PowerShellContextState.Running ||
                this.IsDebuggerStopped)
            {
                this.AbortExecution();
            }

            this.SessionState = PowerShellContextState.Disposed;

            if (this.powerShell != null)
            {
                this.powerShell.InvocationStateChanged -= this.powerShell_InvocationStateChanged;
                this.powerShell.Dispose();
                this.powerShell = null;
            }

            // Clean up the active runspace
            this.CleanupRunspace(this.CurrentRunspace);

            // Push the active runspace so it will be included in the loop
            this.runspaceStack.Push(this.CurrentRunspace);

            while (this.runspaceStack.Count > 0)
            {
                RunspaceDetails poppedRunspace = this.runspaceStack.Pop();

                // Close the popped runspace if it isn't the initial runspace
                // or if it is the initial runspace and we own that runspace
                if (this.initialRunspace != poppedRunspace || this.ownsInitialRunspace)
                {
                    this.CloseRunspace(poppedRunspace);
                }

                this.OnRunspaceChanged(
                    this,
                    new RunspaceChangedEventArgs(
                        RunspaceChangeAction.Shutdown,
                        poppedRunspace,
                        null));
            }

            this.initialRunspace = null;
        }

        private void CloseRunspace(RunspaceDetails runspaceDetails)
        {
            string exitCommand = null;

            switch (runspaceDetails.Context)
            {
                case RunspaceContext.Original:
                    if (runspaceDetails.Location == RunspaceLocation.Local)
                    {
                        runspaceDetails.Runspace.Close();
                        runspaceDetails.Runspace.Dispose();
                    }
                    else
                    {
                        exitCommand = "Exit-PSSession";
                    }

                    break;

                case RunspaceContext.EnteredProcess:
                    exitCommand = "Exit-PSHostProcess";
                    break;

                case RunspaceContext.DebuggedRunspace:
                    // An attached runspace will be detached when the
                    // running pipeline is aborted
                    break;
            }

            if (exitCommand != null)
            {
                Exception exitException = null;

                try
                {
                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspaceDetails.Runspace;
                        ps.AddCommand(exitCommand);
                        ps.Invoke();
                    }
                }
                catch (RemoteException e)
                {
                    exitException = e;
                }
                catch (RuntimeException e)
                {
                    exitException = e;
                }

                if (exitException != null)
                {
                    Logger.Write(
                        LogLevel.Error,
                        $"Caught {exitException.GetType().Name} while exiting {runspaceDetails.Location} runspace:\r\n{exitException.ToString()}");
                }
            }
        }

        internal void ReleaseRunspaceHandle(RunspaceHandle runspaceHandle)
        {
            Validate.IsNotNull("runspaceHandle", runspaceHandle);

            if (this.runspaceWaitQueue.IsEmpty)
            {
                var newRunspaceHandle = new RunspaceHandle(this);
                this.runspaceWaitQueue.EnqueueAsync(newRunspaceHandle).Wait();
            }
            else
            {
                // Write the situation to the log since this shouldn't happen
                Logger.Write(
                    LogLevel.Error,
                    "The PowerShellContext.runspaceWaitQueue has more than one item");
            }
        }

        /// <summary>
        /// Sets the current working directory of the powershell context.  The path should be
        /// unescaped before calling this method.
        /// </summary>
        /// <param name="path"></param>
        public async Task SetWorkingDirectory(string path)
        {
            using (RunspaceHandle runspaceHandle = await this.GetRunspaceHandle())
            {
                runspaceHandle.Runspace.SessionStateProxy.Path.SetLocation(path);
            }
        }

        /// <summary>
        /// Returns the passed in path with the [ and ] characters escaped. Escaping spaces is optional.
        /// </summary>
        /// <param name="path">The path to process.</param>
        /// <param name="escapeSpaces">Specify True to escape spaces in the path, otherwise False.</param>
        /// <returns>The path with [ and ] escaped.</returns>
        public static string EscapePath(string path, bool escapeSpaces)
        {
            string escapedPath = Regex.Replace(path, @"(?<!`)\[", "`[");
            escapedPath = Regex.Replace(escapedPath, @"(?<!`)\]", "`]");

            if (escapeSpaces)
            {
                escapedPath = Regex.Replace(escapedPath, @"(?<!`) ", "` ");
            }

            return escapedPath;
        }

        /// <summary>
        /// Unescapes any escaped [, ] or space characters. Typically use this before calling a
        /// .NET API that doesn't understand PowerShell escaped chars.
        /// </summary>
        /// <param name="path">The path to unescape.</param>
        /// <returns>The path with the ` character before [, ] and spaces removed.</returns>
        public static string UnescapePath(string path)
        {
            if (!path.Contains("`"))
            {
                return path;
            }

            return Regex.Replace(path, @"`(?=[ \[\]])", "");
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the state of the session has changed.
        /// </summary>
        public event EventHandler<SessionStateChangedEventArgs> SessionStateChanged;

        private void OnSessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            if (this.SessionState != PowerShellContextState.Disposed)
            {
                Logger.Write(
                    LogLevel.Verbose,
                    string.Format(
                        "Session state changed --\r\n\r\n    Old state: {0}\r\n    New state: {1}\r\n    Result: {2}",
                        this.SessionState.ToString(),
                        e.NewSessionState.ToString(),
                        e.ExecutionResult));

                this.SessionState = e.NewSessionState;
                this.SessionStateChanged?.Invoke(sender, e);
            }
            else
            {
                Logger.Write(
                    LogLevel.Warning,
                    $"Received session state change to {e.NewSessionState} when already disposed");
            }
        }

        /// <summary>
        /// Raised when the runspace changes by entering a remote session or one in a different process.
        /// </summary>
        public event EventHandler<RunspaceChangedEventArgs> RunspaceChanged;

        private void OnRunspaceChanged(object sender, RunspaceChangedEventArgs e)
        {
            this.RunspaceChanged?.Invoke(sender, e);
        }

        /// <summary>
        /// Raised when the status of an executed command changes.
        /// </summary>
        public event EventHandler<ExecutionStatusChangedEventArgs> ExecutionStatusChanged;

        private void OnExecutionStatusChanged(
            ExecutionStatus executionStatus,
            ExecutionOptions executionOptions,
            bool hadErrors)
        {
            this.ExecutionStatusChanged?.Invoke(
                this,
                new ExecutionStatusChangedEventArgs(
                    executionStatus,
                    executionOptions,
                    hadErrors));
        }

        #endregion

        #region Private Methods

        private IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(PSCommand psCommand, bool sendOutputToHost)
        {
            Logger.Write(
                LogLevel.Verbose,
                string.Format(
                    "Attempting to execute command(s) in the debugger:\r\n\r\n{0}",
                    GetStringForPSCommand(psCommand)));

            IEnumerable<TResult> output =
                this.versionSpecificOperations.ExecuteCommandInDebugger<TResult>(
                    this,
                    this.CurrentRunspace.Runspace,
                    psCommand,
                    sendOutputToHost,
                    out DebuggerResumeAction? debuggerResumeAction);

            if (debuggerResumeAction.HasValue)
            {
                // Resume the debugger with the specificed action
                this.ResumeDebugger(debuggerResumeAction.Value);
            }

            return output;
        }

        internal void WriteOutput(string outputString, bool includeNewLine)
        {
            this.WriteOutput(
                outputString,
                includeNewLine,
                OutputType.Normal);
        }

        internal void WriteOutput(
            string outputString,
            bool includeNewLine,
            OutputType outputType)
        {
            if (this.ConsoleHost != null)
            {
                this.ConsoleHost.WriteOutput(
                    outputString,
                    includeNewLine,
                    outputType);
            }
        }

        private void WriteExceptionToHost(Exception e)
        {
            const string ExceptionFormat =
                "{0}\r\n{1}\r\n    + CategoryInfo          : {2}\r\n    + FullyQualifiedErrorId : {3}";

            IContainsErrorRecord containsErrorRecord = e as IContainsErrorRecord;

            if (containsErrorRecord == null ||
                containsErrorRecord.ErrorRecord == null)
            {
                this.WriteError(e.Message, null, 0, 0);
                return;
            }

            ErrorRecord errorRecord = containsErrorRecord.ErrorRecord;
            if (errorRecord.InvocationInfo == null)
            {
                this.WriteError(errorRecord.ToString(), String.Empty, 0, 0);
                return;
            }

            string errorRecordString = errorRecord.ToString();
            if ((errorRecord.InvocationInfo.PositionMessage != null) &&
                errorRecordString.IndexOf(errorRecord.InvocationInfo.PositionMessage, StringComparison.Ordinal) != -1)
            {
                this.WriteError(errorRecordString);
                return;
            }

            string message =
                string.Format(
                    CultureInfo.InvariantCulture,
                    ExceptionFormat,
                    errorRecord.ToString(),
                    errorRecord.InvocationInfo.PositionMessage,
                    errorRecord.CategoryInfo,
                    errorRecord.FullyQualifiedErrorId);

            this.WriteError(message);
        }

        private void WriteError(
            string errorMessage,
            string filePath,
            int lineNumber,
            int columnNumber)
        {
            const string ErrorLocationFormat = "At {0}:{1} char:{2}";

            this.WriteError(
                errorMessage +
                Environment.NewLine +
                string.Format(
                    ErrorLocationFormat,
                    String.IsNullOrEmpty(filePath) ? "line" : filePath,
                    lineNumber,
                    columnNumber));
        }

        private void WriteError(string errorMessage)
        {
            if (this.ConsoleHost != null)
            {
                this.ConsoleHost.WriteOutput(
                    errorMessage,
                    true,
                    OutputType.Error,
                    ConsoleColor.Red,
                    ConsoleColor.Black);
            }
        }

        void powerShell_InvocationStateChanged(object sender, PSInvocationStateChangedEventArgs e)
        {
            SessionStateChangedEventArgs eventArgs = TranslateInvocationStateInfo(e.InvocationStateInfo);
            this.OnSessionStateChanged(this, eventArgs);
        }

        private static SessionStateChangedEventArgs TranslateInvocationStateInfo(PSInvocationStateInfo invocationState)
        {
            PowerShellContextState newState = PowerShellContextState.Unknown;
            PowerShellExecutionResult executionResult = PowerShellExecutionResult.NotFinished;

            switch (invocationState.State)
            {
                case PSInvocationState.NotStarted:
                    newState = PowerShellContextState.NotStarted;
                    break;

                case PSInvocationState.Failed:
                    newState = PowerShellContextState.Ready;
                    executionResult = PowerShellExecutionResult.Failed;
                    break;

                case PSInvocationState.Disconnected:
                    // TODO: Any extra work to do in this case?
                    // TODO: Is this a unique state that can be re-connected?
                    newState = PowerShellContextState.Disposed;
                    executionResult = PowerShellExecutionResult.Stopped;
                    break;

                case PSInvocationState.Running:
                    newState = PowerShellContextState.Running;
                    break;

                case PSInvocationState.Completed:
                    newState = PowerShellContextState.Ready;
                    executionResult = PowerShellExecutionResult.Completed;
                    break;

                case PSInvocationState.Stopping:
                    newState = PowerShellContextState.Aborting;
                    break;

                case PSInvocationState.Stopped:
                    newState = PowerShellContextState.Ready;
                    executionResult = PowerShellExecutionResult.Aborted;
                    break;

                default:
                    newState = PowerShellContextState.Unknown;
                    break;
            }

            return
                new SessionStateChangedEventArgs(
                    newState,
                    executionResult,
                    invocationState.Reason);
        }

        private Command GetOutputCommand(bool endOfStatement)
        {
            Command outputCommand =
                new Command(
                    command: this.IsDebuggerStopped ? "Out-String" : "Out-Default",
                    isScript: false,
                    useLocalScope: true);

            if (this.IsDebuggerStopped)
            {
                // Out-String needs the -Stream parameter added
                outputCommand.Parameters.Add("Stream");
            }

            return outputCommand;
        }

        private static string GetStringForPSCommand(PSCommand psCommand)
        {
            StringBuilder stringBuilder = new StringBuilder();

            foreach (var command in psCommand.Commands)
            {
                stringBuilder.Append("    ");
                stringBuilder.AppendLine(command.ToString());
            }

            return stringBuilder.ToString();
        }

        private void SetExecutionPolicy(ExecutionPolicy desiredExecutionPolicy)
        {
            var currentPolicy = ExecutionPolicy.Undefined;

            // Get the current execution policy so that we don't set it higher than it already is
            this.powerShell.Commands.AddCommand("Get-ExecutionPolicy");

            var result = this.powerShell.Invoke<ExecutionPolicy>();
            if (result.Count > 0)
            {
                currentPolicy = result.FirstOrDefault();
            }

            if (desiredExecutionPolicy < currentPolicy ||
                desiredExecutionPolicy == ExecutionPolicy.Bypass ||
                currentPolicy == ExecutionPolicy.Undefined)
            {
                Logger.Write(
                    LogLevel.Verbose,
                    string.Format(
                        "Setting execution policy:\r\n    Current = ExecutionPolicy.{0}\r\n    Desired = ExecutionPolicy.{1}",
                        currentPolicy,
                        desiredExecutionPolicy));

                this.powerShell.Commands.Clear();
                this.powerShell
                    .AddCommand("Set-ExecutionPolicy")
                    .AddParameter("ExecutionPolicy", desiredExecutionPolicy)
                    .AddParameter("Scope", ExecutionPolicyScope.Process)
                    .AddParameter("Force");

                try
                {
                    this.powerShell.Invoke();
                }
                catch (CmdletInvocationException e)
                {
                    Logger.WriteException(
                        $"An error occurred while calling Set-ExecutionPolicy, the desired policy of {desiredExecutionPolicy} may not be set.",
                        e);
                }

                this.powerShell.Commands.Clear();
            }
            else
            {
                Logger.Write(
                    LogLevel.Verbose,
                    string.Format(
                        "Current execution policy: ExecutionPolicy.{0}",
                        currentPolicy));

            }
        }

        private SessionDetails GetSessionDetails(Func<PSCommand, PSObject> invokeAction)
        {
            try
            {
                this.mostRecentSessionDetails =
                    new SessionDetails(
                        invokeAction(
                            SessionDetails.GetDetailsCommand()));

                return this.mostRecentSessionDetails;
            }
            catch (RuntimeException e)
            {
                Logger.Write(
                    LogLevel.Verbose,
                    "Runtime exception occurred while gathering runspace info:\r\n\r\n" + e.ToString());
            }

            // TODO: Return a harmless object if necessary
            this.mostRecentSessionDetails = null;
            return this.mostRecentSessionDetails;
        }

        private async Task<SessionDetails> GetSessionDetailsInRunspace()
        {
            using (RunspaceHandle runspaceHandle = await this.GetRunspaceHandle())
            {
                return this.GetSessionDetailsInRunspace(runspaceHandle.Runspace);
            }
        }

        private SessionDetails GetSessionDetailsInRunspace(Runspace runspace)
        {
            SessionDetails sessionDetails =
                this.GetSessionDetails(
                    command =>
                    {
                        using (PowerShell powerShell = PowerShell.Create())
                        {
                            powerShell.Runspace = runspace;
                            powerShell.Commands = command;

                            return
                                powerShell
                                    .Invoke()
                                    .FirstOrDefault();
                        }
                    });

            return sessionDetails;
        }

        private SessionDetails GetSessionDetailsInDebugger()
        {
            return this.GetSessionDetails(
                command =>
                {
                    // Use LastOrDefault to get the last item returned.  This
                    // is necessary because advanced prompt functions (like those
                    // in posh-git) may return multiple objects in the result.
                    return
                        this.ExecuteCommandInDebugger<PSObject>(command, false)
                            .LastOrDefault();
                });
        }

        private SessionDetails GetSessionDetailsInNestedPipeline()
        {
            using (var pipeline = this.CurrentRunspace.Runspace.CreateNestedPipeline())
            {
                return this.GetSessionDetails(
                    command =>
                    {
                        pipeline.Commands.Clear();
                        pipeline.Commands.Add(command.Commands[0]);

                        return
                            pipeline
                                .Invoke()
                                .FirstOrDefault();
                    });
            }
        }

        private void SetProfileVariableInCurrentRunspace(ProfilePaths profilePaths)
        {
            // Create the $profile variable
            PSObject profile = new PSObject(profilePaths.CurrentUserCurrentHost);

            profile.Members.Add(
                new PSNoteProperty(
                    nameof(profilePaths.AllUsersAllHosts),
                    profilePaths.AllUsersAllHosts));

            profile.Members.Add(
                new PSNoteProperty(
                    nameof(profilePaths.AllUsersCurrentHost),
                    profilePaths.AllUsersCurrentHost));

            profile.Members.Add(
                new PSNoteProperty(
                    nameof(profilePaths.CurrentUserAllHosts),
                    profilePaths.CurrentUserAllHosts));

            profile.Members.Add(
                new PSNoteProperty(
                    nameof(profilePaths.CurrentUserCurrentHost),
                    profilePaths.CurrentUserCurrentHost));

            Logger.Write(
                LogLevel.Verbose,
                string.Format(
                    "Setting $profile variable in runspace.  Current user host profile path: {0}",
                    profilePaths.CurrentUserCurrentHost));

            // Set the variable in the runspace
            this.powerShell.Commands.Clear();
            this.powerShell
                .AddCommand("Set-Variable")
                .AddParameter("Name", "profile")
                .AddParameter("Value", profile)
                .AddParameter("Option", "None");
            this.powerShell.Invoke();
            this.powerShell.Commands.Clear();
        }

        private void HandleRunspaceStateChanged(object sender, RunspaceStateEventArgs args)
        {
            switch (args.RunspaceStateInfo.State)
            {
                case RunspaceState.Opening:
                case RunspaceState.Opened:
                    // These cases don't matter, just return
                    return;

                case RunspaceState.Closing:
                case RunspaceState.Closed:
                case RunspaceState.Broken:
                    // If the runspace closes or fails, pop the runspace
                    ((IHostSupportsInteractiveSession)this).PopRunspace();
                    break;
            }
        }

        #endregion

        #region Events

        // NOTE: This event is 'internal' because the DebugService provides
        //       the publicly consumable event.
        internal event EventHandler<DebuggerStopEventArgs> DebuggerStop;

        /// <summary>
        /// Raised when the debugger is resumed after it was previously stopped.
        /// </summary>
        public event EventHandler<DebuggerResumeAction> DebuggerResumed;

        private void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            Logger.Write(LogLevel.Verbose, "Debugger stopped execution.");

            // Set the task so a result can be set
            this.debuggerStoppedTask =
                new TaskCompletionSource<DebuggerResumeAction>();

            // Save the pipeline thread ID and create the pipeline execution task
            this.pipelineThreadId = Thread.CurrentThread.ManagedThreadId;
            this.pipelineExecutionTask = new TaskCompletionSource<IPipelineExecutionRequest>();

            // Hold on to local task vars so that the fields can be cleared independently
            Task<DebuggerResumeAction> localDebuggerStoppedTask = this.debuggerStoppedTask.Task;
            Task<IPipelineExecutionRequest> localPipelineExecutionTask = this.pipelineExecutionTask.Task;

            // Update the session state
            this.OnSessionStateChanged(
                this,
                new SessionStateChangedEventArgs(
                    PowerShellContextState.Ready,
                    PowerShellExecutionResult.Stopped,
                    null));

            // Get the session details and push the current
            // runspace if the session has changed
            var sessionDetails = this.GetSessionDetailsInDebugger();

            // Push the current runspace if the session has changed
            this.UpdateRunspaceDetailsIfSessionChanged(sessionDetails, isDebuggerStop: true);

            // Raise the event for the debugger service
            this.DebuggerStop?.Invoke(sender, e);

            Logger.Write(LogLevel.Verbose, "Starting pipeline thread message loop...");

            while (true)
            {
                int taskIndex =
                    Task.WaitAny(
                        localDebuggerStoppedTask,
                        localPipelineExecutionTask);

                if (taskIndex == 0)
                {
                    // Write a new output line before continuing
                    this.WriteOutput("", true);

                    e.ResumeAction = localDebuggerStoppedTask.Result;
                    Logger.Write(LogLevel.Verbose, "Received debugger resume action " + e.ResumeAction.ToString());

                    // Notify listeners that the debugger has resumed
                    this.DebuggerResumed?.Invoke(this, e.ResumeAction);

                    // Pop the current RunspaceDetails if we were attached
                    // to a runspace and the resume action is Stop
                    if (this.CurrentRunspace.Context == RunspaceContext.DebuggedRunspace &&
                        e.ResumeAction == DebuggerResumeAction.Stop)
                    {
                        this.PopRunspace();
                    }
                    else if (e.ResumeAction != DebuggerResumeAction.Stop)
                    {
                        // Update the session state
                        this.OnSessionStateChanged(
                            this,
                            new SessionStateChangedEventArgs(
                                PowerShellContextState.Running,
                                PowerShellExecutionResult.NotFinished,
                                null));
                    }

                    break;
                }
                else if (taskIndex == 1)
                {
                    Logger.Write(LogLevel.Verbose, "Received pipeline thread execution request.");

                    IPipelineExecutionRequest executionRequest = localPipelineExecutionTask.Result;

                    this.pipelineExecutionTask = new TaskCompletionSource<IPipelineExecutionRequest>();
                    localPipelineExecutionTask = this.pipelineExecutionTask.Task;

                    executionRequest.Execute().Wait();

                    Logger.Write(LogLevel.Verbose, "Pipeline thread execution completed.");

                    this.pipelineResultTask.SetResult(executionRequest);

                    if (this.CurrentRunspace.Runspace.RunspaceAvailability == RunspaceAvailability.Available)
                    {
                        if (this.CurrentRunspace.Context == RunspaceContext.DebuggedRunspace)
                        {
                            // Notify listeners that the debugger has resumed
                            this.DebuggerResumed?.Invoke(this, DebuggerResumeAction.Stop);

                            // We're detached from the runspace now, send a runspace update.
                            this.PopRunspace();
                        }

                        // If the executed command caused the debugger to exit, break
                        // from the pipeline loop
                        break;
                    }
                }
                else
                {
                    // TODO: How to handle this?
                }
            }

            // Clear the task so that it won't be used again
            this.debuggerStoppedTask = null;
            this.pipelineExecutionTask = null;
        }

        // NOTE: This event is 'internal' because the DebugService provides
        //       the publicly consumable event.
        internal event EventHandler<BreakpointUpdatedEventArgs> BreakpointUpdated;

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            this.BreakpointUpdated?.Invoke(sender, e);
        }

        #endregion

        #region Nested Classes

        private interface IPipelineExecutionRequest
        {
            Task Execute();
        }

        /// <summary>
        /// Contains details relating to a request to execute a
        /// command on the PowerShell pipeline thread.
        /// </summary>
        /// <typeparam name="TResult">The expected result type of the execution.</typeparam>
        private class PipelineExecutionRequest<TResult> : IPipelineExecutionRequest
        {
            PowerShellContext powerShellContext;
            PSCommand psCommand;
            StringBuilder errorMessages;
            bool sendOutputToHost;

            public IEnumerable<TResult> Results { get; private set; }

            public PipelineExecutionRequest(
                PowerShellContext powerShellContext,
                PSCommand psCommand,
                StringBuilder errorMessages,
                bool sendOutputToHost)
            {
                this.powerShellContext = powerShellContext;
                this.psCommand = psCommand;
                this.errorMessages = errorMessages;
                this.sendOutputToHost = sendOutputToHost;
            }

            public async Task Execute()
            {
                this.Results =
                    await this.powerShellContext.ExecuteCommand<TResult>(
                        psCommand,
                        errorMessages,
                        sendOutputToHost);

                // TODO: Deal with errors?
            }
        }

        private void ConfigureRunspaceCapabilities(RunspaceDetails runspaceDetails)
        {
            DscBreakpointCapability.CheckForCapability(this.CurrentRunspace, this);
        }

        private void PushRunspace(RunspaceDetails newRunspaceDetails)
        {
            Logger.Write(
                LogLevel.Verbose,
                $"Pushing {this.CurrentRunspace.Location} ({this.CurrentRunspace.Context}), new runspace is {newRunspaceDetails.Location} ({newRunspaceDetails.Context}), connection: {newRunspaceDetails.ConnectionString}");

            RunspaceDetails previousRunspace = this.CurrentRunspace;

            if (newRunspaceDetails.Context == RunspaceContext.DebuggedRunspace)
            {
                this.WriteOutput(
                    $"Entering debugged runspace on {newRunspaceDetails.Location.ToString().ToLower()} machine {newRunspaceDetails.SessionDetails.ComputerName}",
                    true);
            }

            // Switch out event handlers if necessary
            if (CheckIfRunspaceNeedsEventHandlers(newRunspaceDetails))
            {
                this.CleanupRunspace(previousRunspace);
                this.ConfigureRunspace(newRunspaceDetails);
            }

            this.runspaceStack.Push(previousRunspace);
            this.CurrentRunspace = newRunspaceDetails;

            // Check for runspace capabilities
            this.ConfigureRunspaceCapabilities(newRunspaceDetails);

            this.OnRunspaceChanged(
                this,
                new RunspaceChangedEventArgs(
                    RunspaceChangeAction.Enter,
                    previousRunspace,
                    this.CurrentRunspace));
        }

        private void UpdateRunspaceDetailsIfSessionChanged(SessionDetails sessionDetails, bool isDebuggerStop = false)
        {
            RunspaceDetails newRunspaceDetails = null;

            // If we've exited an entered process or debugged runspace, pop what we've
            // got before we evaluate where we're at
            if (
                (this.CurrentRunspace.Context == RunspaceContext.DebuggedRunspace &&
                 this.CurrentRunspace.SessionDetails.InstanceId != sessionDetails.InstanceId) ||
                (this.CurrentRunspace.Context == RunspaceContext.EnteredProcess &&
                 this.CurrentRunspace.SessionDetails.ProcessId != sessionDetails.ProcessId))
            {
                this.PopRunspace();
            }

            // Are we in a new session that the PushRunspace command won't
            // notify us about?
            //
            // Possible cases:
            // - Debugged runspace in a local or remote session
            // - Entered process in a remote session
            //
            // We don't need additional logic to check for the cases that
            // PowerShell would have notified us about because the CurrentRunspace
            // will already be updated by PowerShell by the time we reach
            // these checks.

            if (this.CurrentRunspace.SessionDetails.InstanceId != sessionDetails.InstanceId && isDebuggerStop)
            {
                // Are we on a local or remote computer?
                bool differentComputer =
                    !string.Equals(
                        sessionDetails.ComputerName,
                        this.initialRunspace.SessionDetails.ComputerName,
                        StringComparison.CurrentCultureIgnoreCase);

                // We started debugging a runspace
                newRunspaceDetails =
                    RunspaceDetails.CreateFromDebugger(
                        this.CurrentRunspace,
                        differentComputer ? RunspaceLocation.Remote : RunspaceLocation.Local,
                        RunspaceContext.DebuggedRunspace,
                        sessionDetails);
            }
            else if (this.CurrentRunspace.SessionDetails.ProcessId != sessionDetails.ProcessId)
            {
                // We entered a different PowerShell host process
                newRunspaceDetails =
                    RunspaceDetails.CreateFromContext(
                        this.CurrentRunspace,
                        RunspaceContext.EnteredProcess,
                        sessionDetails);
            }

            if (newRunspaceDetails != null)
            {
                this.PushRunspace(newRunspaceDetails);
            }
        }

        private void PopRunspace()
        {
            if (this.SessionState != PowerShellContextState.Disposed)
            {
                if (this.runspaceStack.Count > 0)
                {
                    RunspaceDetails previousRunspace = this.CurrentRunspace;
                    this.CurrentRunspace = this.runspaceStack.Pop();

                    Logger.Write(
                        LogLevel.Verbose,
                        $"Popping {previousRunspace.Location} ({previousRunspace.Context}), new runspace is {this.CurrentRunspace.Location} ({this.CurrentRunspace.Context}), connection: {this.CurrentRunspace.ConnectionString}");

                    if (previousRunspace.Context == RunspaceContext.DebuggedRunspace)
                    {
                        this.WriteOutput(
                            $"Leaving debugged runspace on {previousRunspace.Location.ToString().ToLower()} machine {previousRunspace.SessionDetails.ComputerName}",
                            true);
                    }

                    // Switch out event handlers if necessary
                    if (CheckIfRunspaceNeedsEventHandlers(previousRunspace))
                    {
                        this.CleanupRunspace(previousRunspace);
                        this.ConfigureRunspace(this.CurrentRunspace);
                    }

                    this.OnRunspaceChanged(
                        this,
                        new RunspaceChangedEventArgs(
                            RunspaceChangeAction.Exit,
                            previousRunspace,
                            this.CurrentRunspace));
                }
                else
                {
                    Logger.Write(
                        LogLevel.Error,
                        "Caller attempted to pop a runspace when no runspaces are on the stack.");
                }
            }
        }

        #endregion

        #region IHostSupportsInteractiveSession Implementation

        bool IHostSupportsInteractiveSession.IsRunspacePushed
        {
            get
            {
                return this.runspaceStack.Count > 0;
            }
        }

        Runspace IHostSupportsInteractiveSession.Runspace
        {
            get
            {
                return this.CurrentRunspace.Runspace;
            }
        }

        void IHostSupportsInteractiveSession.PushRunspace(Runspace runspace)
        {
            // Get the session details for the new runspace
            SessionDetails sessionDetails = this.GetSessionDetailsInRunspace(runspace);

            this.PushRunspace(
                RunspaceDetails.CreateFromRunspace(
                    runspace,
                    sessionDetails));
        }

        void IHostSupportsInteractiveSession.PopRunspace()
        {
            this.PopRunspace();
        }

        #endregion
    }
}
