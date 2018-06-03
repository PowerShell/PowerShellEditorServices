//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
    using System.Management.Automation.Remoting;

    /// <summary>
    /// Manages the lifetime and usage of a PowerShell session.
    /// Handles nested PowerShell prompts and also manages execution of
    /// commands whether inside or outside of the debugger.
    /// </summary>
    public class PowerShellContext : IDisposable, IHostSupportsInteractiveSession
    {
        #region Fields

        private readonly SemaphoreSlim resumeRequestHandle = new SemaphoreSlim(1, 1);

        private bool isPSReadLineEnabled;
        private ILogger logger;
        private PowerShell powerShell;
        private bool ownsInitialRunspace;
        private RunspaceDetails initialRunspace;
        private SessionDetails mostRecentSessionDetails;

        private ProfilePaths profilePaths;

        private IVersionSpecificOperations versionSpecificOperations;

        private Stack<RunspaceDetails> runspaceStack = new Stack<RunspaceDetails>();

        private bool isCommandLoopRestarterSet;

        #endregion

        #region Properties

        private IPromptContext PromptContext { get; set; }

        private PromptNest PromptNest { get; set; }

        private InvocationEventQueue InvocationEventQueue { get; set; }

        private EngineIntrinsics EngineIntrinsics { get; set; }

        private PSHost ExternalHost { get; set; }

        /// <summary>
        /// Gets a boolean that indicates whether the debugger is currently stopped,
        /// either at a breakpoint or because the user broke execution.
        /// </summary>
        public bool IsDebuggerStopped =>
            this.versionSpecificOperations.IsDebuggerStopped(
                PromptNest,
                CurrentRunspace.Runspace);

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
        /// Gets or sets an IHostOutput implementation for use in
        /// writing output to the console.
        /// </summary>
        private IHostOutput ConsoleWriter { get; set; }

        private IHostInput ConsoleReader { get; set; }

        /// <summary>
        /// Gets details pertaining to the current runspace.
        /// </summary>
        public RunspaceDetails CurrentRunspace
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether the current runspace
        /// is ready for a command
        /// </summary>
        public bool IsAvailable => this.SessionState == PowerShellContextState.Ready;

        /// <summary>
        /// Gets the working directory path the PowerShell context was inititially set when the debugger launches.
        /// This path is used to determine whether a script in the call stack is an "external" script.
        /// </summary>
        public string InitialWorkingDirectory { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <param name="isPSReadLineEnabled">
        /// Indicates whether PSReadLine should be used if possible
        /// </param>
        public PowerShellContext(ILogger logger, bool isPSReadLineEnabled)
        {
            this.logger = logger;
            this.isPSReadLineEnabled = isPSReadLineEnabled;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="hostDetails"></param>
        /// <param name="powerShellContext"></param>
        /// <param name="hostUserInterface">
        /// The EditorServicesPSHostUserInterface to use for this instance.
        /// </param>
        /// <param name="logger">An ILogger implementation to use for this instance.</param>
        /// <returns></returns>
        public static Runspace CreateRunspace(
            HostDetails hostDetails,
            PowerShellContext powerShellContext,
            EditorServicesPSHostUserInterface hostUserInterface,
            ILogger logger)
        {
            var psHost = new EditorServicesPSHost(powerShellContext, hostDetails, hostUserInterface, logger);
            powerShellContext.ConsoleWriter = hostUserInterface;
            powerShellContext.ConsoleReader = hostUserInterface;
            return CreateRunspace(psHost);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="psHost"></param>
        /// <returns></returns>
        public static Runspace CreateRunspace(PSHost psHost)
        {
            var initialSessionState = InitialSessionState.CreateDefault2();

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, initialSessionState);
#if !CoreCLR
            runspace.ApartmentState = ApartmentState.STA;
#endif
            runspace.ThreadOptions = PSThreadOptions.ReuseThread;
            runspace.Open();

            return runspace;
        }

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class using
        /// an existing runspace for the session.
        /// </summary>
        /// <param name="profilePaths">An object containing the profile paths for the session.</param>
        /// <param name="initialRunspace">The initial runspace to use for this instance.</param>
        /// <param name="ownsInitialRunspace">If true, the PowerShellContext owns this runspace.</param>
        public void Initialize(
            ProfilePaths profilePaths,
            Runspace initialRunspace,
            bool ownsInitialRunspace)
        {
            this.Initialize(profilePaths, initialRunspace, ownsInitialRunspace, null);
        }

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class using
        /// an existing runspace for the session.
        /// </summary>
        /// <param name="profilePaths">An object containing the profile paths for the session.</param>
        /// <param name="initialRunspace">The initial runspace to use for this instance.</param>
        /// <param name="ownsInitialRunspace">If true, the PowerShellContext owns this runspace.</param>
        /// <param name="consoleHost">An IHostOutput implementation.  Optional.</param>
        public void Initialize(
            ProfilePaths profilePaths,
            Runspace initialRunspace,
            bool ownsInitialRunspace,
            IHostOutput consoleHost)
        {
            Validate.IsNotNull("initialRunspace", initialRunspace);

            this.ownsInitialRunspace = ownsInitialRunspace;
            this.SessionState = PowerShellContextState.NotStarted;
            this.ConsoleWriter = consoleHost;
            this.ConsoleReader = consoleHost as IHostInput;

            // Get the PowerShell runtime version
            this.LocalPowerShellVersion =
                PowerShellVersionDetails.GetVersionDetails(
                    initialRunspace,
                    this.logger);

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
            this.logger.Write(
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
            this.SessionState = PowerShellContextState.Ready;

            // EngineIntrinsics is used in some instances to interact with the initial
            // runspace without having to wait for PSReadLine to check for events.
            this.EngineIntrinsics =
                initialRunspace
                    .SessionStateProxy
                    .PSVariable
                    .GetValue("ExecutionContext")
                    as EngineIntrinsics;

            // The external host is used to properly exit from a nested prompt that
            // was entered by the user.
            this.ExternalHost =
                initialRunspace
                    .SessionStateProxy
                    .PSVariable
                    .GetValue("Host")
                    as PSHost;

            // Now that the runspace is ready, enqueue it for first use
            this.PromptNest = new PromptNest(
                this,
                this.powerShell,
                this.ConsoleReader,
                this.versionSpecificOperations);
            this.InvocationEventQueue = new InvocationEventQueue(this, this.PromptNest);

            if (powerShellVersion.Major >= 5 &&
                this.isPSReadLineEnabled &&
                PSReadLinePromptContext.TryGetPSReadLineProxy(initialRunspace, out PSReadLineProxy proxy))
            {
                this.PromptContext = new PSReadLinePromptContext(
                    this,
                    this.PromptNest,
                    this.InvocationEventQueue,
                    proxy);
            }
            else
            {
                this.PromptContext = new LegacyReadLineContext(this);
            }
        }

        /// <summary>
        /// Imports the PowerShellEditorServices.Commands module into
        /// the runspace.  This method will be moved somewhere else soon.
        /// </summary>
        /// <param name="moduleBasePath"></param>
        /// <returns></returns>
        public Task ImportCommandsModule(string moduleBasePath)
        {
            PSCommand importCommand = new PSCommand();
            importCommand
                .AddCommand("Import-Module")
                .AddArgument(
                    Path.Combine(
                        moduleBasePath,
                        "PowerShellEditorServices.Commands.psd1"));

            return this.ExecuteCommand<PSObject>(importCommand, false, false);
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
            return this.GetRunspaceHandleImpl(CancellationToken.None, isReadLine: false);
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
            return this.GetRunspaceHandleImpl(cancellationToken, isReadLine: false);
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
            // Add history to PSReadLine before cancelling, otherwise it will be restored as the
            // cancelled prompt when it's called again.
            if (executionOptions.AddToHistory)
            {
                this.PromptContext.AddToHistory(psCommand.Commands[0].CommandText);
            }

            bool hadErrors = false;
            RunspaceHandle runspaceHandle = null;
            ExecutionTarget executionTarget = ExecutionTarget.PowerShell;
            IEnumerable<TResult> executionResult = Enumerable.Empty<TResult>();
            var shouldCancelReadLine =
                executionOptions.InterruptCommandPrompt ||
                executionOptions.WriteOutputToHost;

            // If the debugger is active and the caller isn't on the pipeline
            // thread, send the command over to that thread to be executed.
            // Determine if execution should take place in a different thread
            // using the following criteria:
            // 1. The current frame in the prompt nest has a thread controller
            //    (meaning it is a nested prompt or is in the debugger)
            // 2. We aren't already on the thread in question
            // 3. The command is not a candidate for background invocation
            //    via PowerShell eventing
            // 4. The command cannot be for a PSReadLine pipeline while we
            //    are currently in a out of process runspace
            var threadController = PromptNest.GetThreadController();
            if (!(threadController == null ||
                !threadController.IsPipelineThread ||
                threadController.IsCurrentThread() ||
                this.ShouldExecuteWithEventing(executionOptions) ||
                (PromptNest.IsRemote && executionOptions.IsReadLine)))
            {
                this.logger.Write(LogLevel.Verbose, "Passing command execution to pipeline thread.");

                if (shouldCancelReadLine && PromptNest.IsReadLineBusy())
                {
                    // If a ReadLine pipeline is running in the debugger then we'll hang here
                    // if we don't cancel it. Typically we can rely on OnExecutionStatusChanged but
                    // the pipeline request won't even start without clearing the current task.
                    this.ConsoleReader.StopCommandLoop();
                }

                // Send the pipeline execution request to the pipeline thread
                return await threadController.RequestPipelineExecution(
                    new PipelineExecutionRequest<TResult>(
                        this,
                        psCommand,
                        errorMessages,
                        executionOptions));
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

                    executionTarget = GetExecutionTarget(executionOptions);

                    // If a ReadLine pipeline is running we can still execute commands that
                    // don't write output (e.g. command completion)
                    if (executionTarget == ExecutionTarget.InvocationEvent)
                    {
                        return (await this.InvocationEventQueue.ExecuteCommandOnIdle<TResult>(
                            psCommand,
                            errorMessages,
                            executionOptions));
                    }

                    // Prompt is stopped and started based on the execution status, so naturally
                    // we don't want PSReadLine pipelines to factor in.
                    if (!executionOptions.IsReadLine)
                    {
                        this.OnExecutionStatusChanged(
                            ExecutionStatus.Running,
                            executionOptions,
                            false);
                    }

                    runspaceHandle = await this.GetRunspaceHandle(executionOptions.IsReadLine);
                    if (executionOptions.WriteInputToHost)
                    {
                        this.WriteOutput(psCommand.Commands[0].CommandText, true);
                    }

                    if (executionTarget == ExecutionTarget.Debugger)
                    {
                        // Manually change the session state for debugger commands because
                        // we don't have an invocation state event to attach to.
                        if (!executionOptions.IsReadLine)
                        {
                            this.OnSessionStateChanged(
                                this,
                                new SessionStateChangedEventArgs(
                                    PowerShellContextState.Running,
                                    PowerShellExecutionResult.NotFinished,
                                    null));
                        }
                        try
                        {
                            return this.ExecuteCommandInDebugger<TResult>(
                                psCommand,
                                executionOptions.WriteOutputToHost);
                        }
                        catch (Exception e)
                        {
                            logger.Write(
                                LogLevel.Error,
                                "Exception occurred while executing debugger command:\r\n\r\n" + e.ToString());
                        }
                        finally
                        {
                            if (!executionOptions.IsReadLine)
                            {
                                this.OnSessionStateChanged(
                                    this,
                                    new SessionStateChangedEventArgs(
                                        PowerShellContextState.Ready,
                                        PowerShellExecutionResult.Stopped,
                                        null));
                            }
                        }
                    }

                    var invocationSettings = new PSInvocationSettings()
                    {
                        AddToHistory = executionOptions.AddToHistory
                    };

                    this.logger.Write(
                        LogLevel.Verbose,
                        string.Format(
                            "Attempting to execute command(s):\r\n\r\n{0}",
                            GetStringForPSCommand(psCommand)));


                    PowerShell shell = this.PromptNest.GetPowerShell(executionOptions.IsReadLine);
                    shell.Commands = psCommand;

                    // Don't change our SessionState for ReadLine.
                    if (!executionOptions.IsReadLine)
                    {
                        shell.InvocationStateChanged += powerShell_InvocationStateChanged;
                    }

                    shell.Runspace = executionOptions.ShouldExecuteInOriginalRunspace
                        ? this.initialRunspace.Runspace
                        : this.CurrentRunspace.Runspace;
                    try
                    {
                        // Nested PowerShell instances can't be invoked asynchronously. This occurs
                        // in nested prompts and pipeline requests from eventing.
                        if (shell.IsNested)
                        {
                            return shell.Invoke<TResult>(null, invocationSettings);
                        }

                        return await Task.Factory.StartNew<IEnumerable<TResult>>(
                            () => shell.Invoke<TResult>(null, invocationSettings),
                            CancellationToken.None, // Might need a cancellation token
                            TaskCreationOptions.None,
                            TaskScheduler.Default);
                    }
                    finally
                    {
                        if (!executionOptions.IsReadLine)
                        {
                            shell.InvocationStateChanged -= powerShell_InvocationStateChanged;
                        }

                        if (shell.HadErrors)
                        {
                            var strBld = new StringBuilder(1024);
                            strBld.AppendFormat("Execution of the following command(s) completed with errors:\r\n\r\n{0}\r\n",
                                GetStringForPSCommand(psCommand));

                            int i = 1;
                            foreach (var error in shell.Streams.Error)
                            {
                                if (i > 1) strBld.Append("\r\n\r\n");
                                strBld.Append($"Error #{i++}:\r\n");
                                strBld.Append(error.ToString() + "\r\n");
                                strBld.Append("ScriptStackTrace:\r\n");
                                strBld.Append((error.ScriptStackTrace ?? "<null>") + "\r\n");
                                strBld.Append($"Exception:\r\n   {error.Exception?.ToString() ?? "<null>"}");
                                Exception innerEx = error.Exception?.InnerException;
                                while (innerEx != null)
                                {
                                    strBld.Append($"InnerException:\r\n   {innerEx.ToString()}");
                                    innerEx = innerEx.InnerException;
                                }
                            }

                            // We've reported these errors, clear them so they don't keep showing up.
                            shell.Streams.Error.Clear();

                            var errorMessage = strBld.ToString();

                            errorMessages?.Append(errorMessage);
                            this.logger.Write(LogLevel.Error, errorMessage);

                            hadErrors = true;
                        }
                        else
                        {
                            this.logger.Write(
                                LogLevel.Verbose,
                                "Execution completed successfully.");
                        }
                    }
                }
                catch (PSRemotingDataStructureException e)
                {
                    this.logger.Write(
                        LogLevel.Error,
                        "Pipeline stopped while executing command:\r\n\r\n" + e.ToString());

                    errorMessages?.Append(e.Message);
                }
                catch (PipelineStoppedException e)
                {
                    this.logger.Write(
                        LogLevel.Error,
                        "Pipeline stopped while executing command:\r\n\r\n" + e.ToString());

                    errorMessages?.Append(e.Message);
                }
                catch (RuntimeException e)
                {
                    this.logger.Write(
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
                        if (executionTarget == ExecutionTarget.Debugger)
                        {
                            sessionDetails = this.GetSessionDetailsInDebugger();
                        }
                        else if (this.CurrentRunspace.Runspace.RunspaceAvailability == RunspaceAvailability.Available)
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

                    this.OnExecutionStatusChanged(
                        ExecutionStatus.Completed,
                        executionOptions,
                        hadErrors);
                }
            }

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
            return await this.ExecuteCommand<object>(
                new PSCommand().AddScript(scriptString.Trim()),
                errorMessages,
                new ExecutionOptions()
                {
                    WriteOutputToHost = writeOutputToHost,
                    AddToHistory = addToHistory,
                    WriteInputToHost = writeInputToHost
                });
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
                    string workingDir = (await ExecuteCommand<PathInfo>(
                        new PSCommand()
                            .AddCommand("Microsoft.PowerShell.Management\\Get-Location")
                            .AddParameter("PSProvider", "FileSystem"),
                            false,
                            false))
                        .FirstOrDefault()
                        .ProviderPath;

                    workingDir = workingDir.TrimEnd(Path.DirectorySeparatorChar);
                    scriptAbsPath = workingDir + Path.DirectorySeparatorChar + script;
                }
                catch (System.Management.Automation.DriveNotFoundException e)
                {
                    this.logger.Write(
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

        /// <summary>
        /// Forces the <see cref="PromptContext" /> to trigger PowerShell event handling,
        /// reliquishing control of the pipeline thread during event processing.
        /// </summary>
        /// <remarks>
        /// This method is called automatically by <see cref="InvokeOnPipelineThread" /> and
        /// <see cref="ExecuteCommand" />. Consider using them instead of this method directly when
        /// possible.
        /// </remarks>
        internal void ForcePSEventHandling()
        {
            PromptContext.ForcePSEventHandling();
        }

        /// <summary>
        /// Marshals a <see cref="Action{PowerShell}" /> to run on the pipeline thread. A new
        /// <see cref="PromptNestFrame" /> will be created for the invocation.
        /// </summary>
        /// <param name="invocationAction">
        /// The <see cref="Action{PowerShell}" /> to invoke on the pipeline thread. The nested
        /// <see cref="PowerShell" /> instance for the created <see cref="PromptNestFrame" />
        /// will be passed as an argument.
        /// </param>
        /// <returns>
        /// An awaitable <see cref="Task" /> that the caller can use to know when execution completes.
        /// </returns>
        /// <remarks>
        /// This method is called automatically by <see cref="ExecuteCommand" />. Consider using
        /// that method instead of calling this directly when possible.
        /// </remarks>
        internal async Task InvokeOnPipelineThread(Action<PowerShell> invocationAction)
        {
            await this.InvocationEventQueue.InvokeOnPipelineThread(invocationAction);
        }

        internal async Task<string> InvokeReadLine(bool isCommandLine, CancellationToken cancellationToken)
        {
            return await PromptContext.InvokeReadLine(
                isCommandLine,
                cancellationToken);
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

                if (results.Count == 0 || results.FirstOrDefault() == null)
                {
                    return defaultValue;
                }

                if (typeof(TResult) != typeof(PSObject))
                {
                    return results
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
                    await this.ExecuteCommand<object>(command, true, true);
                }

                // Gather the session details (particularly the prompt) after
                // loading the user's profiles.
                await this.GetSessionDetailsInRunspace();
            }
        }

        /// <summary>
        /// Causes the most recent execution to be aborted no matter what state
        /// it is currently in.
        /// </summary>
        public void AbortExecution()
        {
            this.AbortExecution(shouldAbortDebugSession: false);
        }

        /// <summary>
        /// Causes the most recent execution to be aborted no matter what state
        /// it is currently in.
        /// </summary>
        /// <param name="shouldAbortDebugSession">
        /// A value indicating whether a debug session should be aborted if one
        /// is currently active.
        /// </param>
        public void AbortExecution(bool shouldAbortDebugSession)
        {
            if (this.SessionState != PowerShellContextState.Aborting &&
                this.SessionState != PowerShellContextState.Disposed)
            {
                this.logger.Write(LogLevel.Verbose, "Execution abort requested...");

                if (shouldAbortDebugSession)
                {
                    this.ExitAllNestedPrompts();
                }

                if (this.PromptNest.IsInDebugger)
                {
                    if (shouldAbortDebugSession)
                    {
                        this.PromptNest.WaitForCurrentFrameExit(
                            frame =>
                            {
                                this.versionSpecificOperations.StopCommandInDebugger(this);
                                this.ResumeDebugger(DebuggerResumeAction.Stop);
                            });
                    }
                    else
                    {
                        this.versionSpecificOperations.StopCommandInDebugger(this);
                    }
                }
                else
                {
                    this.PromptNest.GetPowerShell(isReadLine: false).BeginStop(null, null);
                }

                this.SessionState = PowerShellContextState.Aborting;

                this.OnExecutionStatusChanged(
                    ExecutionStatus.Aborted,
                    null,
                    false);
            }
            else
            {
                this.logger.Write(
                    LogLevel.Verbose,
                    string.Format(
                        $"Execution abort requested when already aborted (SessionState = {this.SessionState})"));
            }
        }

        /// <summary>
        /// Exit all consecutive nested prompts that the user has entered.
        /// </summary>
        internal void ExitAllNestedPrompts()
        {
            while (this.PromptNest.IsNestedPrompt)
            {
                this.PromptNest.WaitForCurrentFrameExit(frame => this.ExitNestedPrompt());
                this.versionSpecificOperations.ExitNestedPrompt(ExternalHost);
            }
        }

        /// <summary>
        /// Exit all consecutive nested prompts that the user has entered.
        /// </summary>
        /// <returns>
        /// A task object that represents all nested prompts being exited
        /// </returns>
        internal async Task ExitAllNestedPromptsAsync()
        {
            while (this.PromptNest.IsNestedPrompt)
            {
                await this.PromptNest.WaitForCurrentFrameExitAsync(frame => this.ExitNestedPrompt());
                this.versionSpecificOperations.ExitNestedPrompt(ExternalHost);
            }
        }

        /// <summary>
        /// Causes the debugger to break execution wherever it currently is.
        /// This method is internal because the real Break API is provided
        /// by the DebugService.
        /// </summary>
        internal void BreakExecution()
        {
            this.logger.Write(LogLevel.Verbose, "Debugger break requested...");

            // Pause the debugger
            this.versionSpecificOperations.PauseDebugger(
                this.CurrentRunspace.Runspace);
        }

        internal void ResumeDebugger(DebuggerResumeAction resumeAction)
        {
            ResumeDebugger(resumeAction, shouldWaitForExit: true);
        }

        private void ResumeDebugger(DebuggerResumeAction resumeAction, bool shouldWaitForExit)
        {
            resumeRequestHandle.Wait();
            try
            {
                if (this.PromptNest.IsNestedPrompt)
                {
                    this.ExitAllNestedPrompts();
                }

                if (this.PromptNest.IsInDebugger)
                {
                    // Set the result so that the execution thread resumes.
                    // The execution thread will clean up the task.
                    if (shouldWaitForExit)
                    {
                        this.PromptNest.WaitForCurrentFrameExit(
                            frame =>
                            {
                                frame.ThreadController.StartThreadExit(resumeAction);
                                this.ConsoleReader.StopCommandLoop();
                                if (this.SessionState != PowerShellContextState.Ready)
                                {
                                    this.versionSpecificOperations.StopCommandInDebugger(this);
                                }
                            });
                    }
                    else
                    {
                        this.PromptNest.GetThreadController().StartThreadExit(resumeAction);
                        this.ConsoleReader.StopCommandLoop();
                        if (this.SessionState != PowerShellContextState.Ready)
                        {
                            this.versionSpecificOperations.StopCommandInDebugger(this);
                        }
                    }
                }
                else
                {
                    this.logger.Write(
                        LogLevel.Error,
                        $"Tried to resume debugger with action {resumeAction} but there was no debuggerStoppedTask.");
                }
            }
            finally
            {
                resumeRequestHandle.Release();
            }
        }

        /// <summary>
        /// Disposes the runspace and any other resources being used
        /// by this PowerShellContext.
        /// </summary>
        public void Dispose()
        {
            this.PromptNest.Dispose();
            this.SessionState = PowerShellContextState.Disposed;

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

        private async Task<RunspaceHandle> GetRunspaceHandle(bool isReadLine)
        {
            return await this.GetRunspaceHandleImpl(CancellationToken.None, isReadLine);
        }

        private async Task<RunspaceHandle> GetRunspaceHandleImpl(CancellationToken cancellationToken, bool isReadLine)
        {
            return await this.PromptNest.GetRunspaceHandleAsync(cancellationToken, isReadLine);
        }

        private ExecutionTarget GetExecutionTarget(ExecutionOptions options = null)
        {
            if (options == null)
            {
                options = new ExecutionOptions();
            }

            var noBackgroundInvocation =
                options.InterruptCommandPrompt ||
                options.WriteOutputToHost ||
                options.IsReadLine ||
                PromptNest.IsRemote;

            // Take over the pipeline if PSReadLine is running, we aren't trying to run PSReadLine, and
            // we aren't in a remote session.
            if (!noBackgroundInvocation && PromptNest.IsReadLineBusy() && PromptNest.IsMainThreadBusy())
            {
                return ExecutionTarget.InvocationEvent;
            }

            // We can't take the pipeline from PSReadLine if it's in a remote session, so we need to
            // invoke locally in that case.
            if (IsDebuggerStopped && PromptNest.IsInDebugger && !(options.IsReadLine && PromptNest.IsRemote))
            {
                return ExecutionTarget.Debugger;
            }

            return ExecutionTarget.PowerShell;
        }

        private bool ShouldExecuteWithEventing(ExecutionOptions executionOptions)
        {
            return
                this.PromptNest.IsReadLineBusy() &&
                this.PromptNest.IsMainThreadBusy() &&
                !(executionOptions.IsReadLine ||
                executionOptions.InterruptCommandPrompt ||
                executionOptions.WriteOutputToHost ||
                IsCurrentRunspaceOutOfProcess());
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
                    this.logger.Write(
                        LogLevel.Error,
                        $"Caught {exitException.GetType().Name} while exiting {runspaceDetails.Location} runspace:\r\n{exitException.ToString()}");
                }
            }
        }

        internal void ReleaseRunspaceHandle(RunspaceHandle runspaceHandle)
        {
            Validate.IsNotNull("runspaceHandle", runspaceHandle);

            if (PromptNest.IsMainThreadBusy() || (runspaceHandle.IsReadLine && PromptNest.IsReadLineBusy()))
            {
                var unusedTask = PromptNest
                    .ReleaseRunspaceHandleAsync(runspaceHandle)
                    .ConfigureAwait(false);
            }
            else
            {
                // Write the situation to the log since this shouldn't happen
                this.logger.Write(
                    LogLevel.Error,
                    "ReleaseRunspaceHandle was called when the main thread was not busy.");
            }
        }

        /// <summary>
        /// Determines if the current runspace is out of process.
        /// </summary>
        /// <returns>
        /// A value indicating whether the current runspace is out of process.
        /// </returns>
        internal bool IsCurrentRunspaceOutOfProcess()
        {
            return
                CurrentRunspace.Context == RunspaceContext.EnteredProcess ||
                CurrentRunspace.Context == RunspaceContext.DebuggedRunspace ||
                CurrentRunspace.Location == RunspaceLocation.Remote;
        }

        /// <summary>
        /// Called by the external PSHost when $Host.EnterNestedPrompt is called.
        /// </summary>
        internal void EnterNestedPrompt()
        {
            if (this.IsCurrentRunspaceOutOfProcess())
            {
                throw new NotSupportedException();
            }

            this.PromptNest.PushPromptContext(PromptNestFrameType.NestedPrompt);
            var localThreadController = this.PromptNest.GetThreadController();
            this.OnSessionStateChanged(
                this,
                new SessionStateChangedEventArgs(
                    PowerShellContextState.Ready,
                    PowerShellExecutionResult.Stopped,
                    null));

            // Reset command loop mainly for PSReadLine
            this.ConsoleReader.StopCommandLoop();
            this.ConsoleReader.StartCommandLoop();

            var localPipelineExecutionTask = localThreadController.TakeExecutionRequest();
            var localDebuggerStoppedTask = localThreadController.Exit();

            // Wait for off-thread pipeline requests and/or ExitNestedPrompt
            while (true)
            {
                int taskIndex = Task.WaitAny(
                    localPipelineExecutionTask,
                    localDebuggerStoppedTask);

                if (taskIndex == 0)
                {
                    var localExecutionTask = localPipelineExecutionTask.GetAwaiter().GetResult();
                    localPipelineExecutionTask = localThreadController.TakeExecutionRequest();
                    localExecutionTask.Execute().GetAwaiter().GetResult();
                    continue;
                }

                this.ConsoleReader.StopCommandLoop();
                this.PromptNest.PopPromptContext();
                break;
            }
        }

        /// <summary>
        /// Called by the external PSHost when $Host.ExitNestedPrompt is called.
        /// </summary>
        internal void ExitNestedPrompt()
        {
            if (this.PromptNest.NestedPromptLevel == 1 || !this.PromptNest.IsNestedPrompt)
            {
                this.logger.Write(
                    LogLevel.Error,
                    "ExitNestedPrompt was called outside of a nested prompt.");
                return;
            }

            // Stop the command input loop so PSReadLine isn't invoked between ExitNestedPrompt
            // being invoked and EnterNestedPrompt getting the message to exit.
            this.ConsoleReader.StopCommandLoop();
            this.PromptNest.GetThreadController().StartThreadExit(DebuggerResumeAction.Stop);
        }

        /// <summary>
        /// Sets the current working directory of the powershell context.  The path should be
        /// unescaped before calling this method.
        /// </summary>
        /// <param name="path"></param>
        public async Task SetWorkingDirectory(string path)
        {
            await this.SetWorkingDirectory(path, true);
        }

        /// <summary>
        /// Sets the current working directory of the powershell context.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isPathAlreadyEscaped">Specify false to have the path escaped, otherwise specify true if the path has already been escaped.</param>
        public async Task SetWorkingDirectory(string path, bool isPathAlreadyEscaped)
        {
            this.InitialWorkingDirectory = path;

            if (!isPathAlreadyEscaped)
            {
                path = EscapePath(path, false);
            }

            await ExecuteCommand<PSObject>(
                new PSCommand().AddCommand("Set-Location").AddParameter("Path", path),
                null,
                sendOutputToHost: false,
                sendErrorToHost: false,
                addToHistory: false);
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
                this.logger.Write(
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
                this.logger.Write(
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
            this.logger.Write(
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
                this.ResumeDebugger(
                    debuggerResumeAction.Value,
                    shouldWaitForExit: false);
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
            if (this.ConsoleWriter != null)
            {
                this.ConsoleWriter.WriteOutput(
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
            if (this.ConsoleWriter != null)
            {
                this.ConsoleWriter.WriteOutput(
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
                    command: this.PromptNest.IsInDebugger ? "Out-String" : "Out-Default",
                    isScript: false,
                    useLocalScope: true);

            if (this.PromptNest.IsInDebugger)
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
                stringBuilder.Append(command.CommandText);
                foreach (var param in command.Parameters)
                {
                    if (param.Name != null)
                    {
                        stringBuilder.Append($" -{param.Name} {param.Value}");
                    }
                    else
                    {
                        stringBuilder.Append($" {param.Value}");
                    }
                }

                stringBuilder.AppendLine();
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
                this.logger.Write(
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
                    this.logger.WriteException(
                        $"An error occurred while calling Set-ExecutionPolicy, the desired policy of {desiredExecutionPolicy} may not be set.",
                        e);
                }

                this.powerShell.Commands.Clear();
            }
            else
            {
                this.logger.Write(
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
                this.logger.Write(
                    LogLevel.Verbose,
                    "Runtime exception occurred while gathering runspace info:\r\n\r\n" + e.ToString());
            }
            catch (ArgumentNullException)
            {
                this.logger.Write(
                    LogLevel.Error,
                    "Could not retrieve session details but no exception was thrown.");
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

            this.logger.Write(
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

        private void StartCommandLoopOnRunspaceAvailable()
        {
            if (this.isCommandLoopRestarterSet)
            {
                return;
            }

            EventHandler<RunspaceAvailabilityEventArgs> handler = null;
            handler = (runspace, eventArgs) =>
            {
                if (eventArgs.RunspaceAvailability != RunspaceAvailability.Available ||
                    ((Runspace)runspace).Debugger.InBreakpoint)
                {
                    return;
                }

                ((Runspace)runspace).AvailabilityChanged -= handler;
                this.isCommandLoopRestarterSet = false;
                this.ConsoleReader.StartCommandLoop();
            };

            this.CurrentRunspace.Runspace.AvailabilityChanged += handler;
            this.isCommandLoopRestarterSet = true;
        }

        private void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            if (CurrentRunspace.Context == RunspaceContext.Original)
            {
                StartCommandLoopOnRunspaceAvailable();
            }

            this.logger.Write(LogLevel.Verbose, "Debugger stopped execution.");

            PromptNest.PushPromptContext(
                IsCurrentRunspaceOutOfProcess()
                    ? PromptNestFrameType.Debug | PromptNestFrameType.Remote
                    : PromptNestFrameType.Debug);

            ThreadController localThreadController = PromptNest.GetThreadController();

            // Update the session state
            this.OnSessionStateChanged(
                this,
                new SessionStateChangedEventArgs(
                    PowerShellContextState.Ready,
                    PowerShellExecutionResult.Stopped,
                    null));

                // Get the session details and push the current
                // runspace if the session has changed
                SessionDetails sessionDetails = null;
                try
                {
                    sessionDetails = this.GetSessionDetailsInDebugger();
                }
                catch (InvalidOperationException)
                {
                    this.logger.Write(
                        LogLevel.Verbose,
                        "Attempting to get session details failed, most likely due to a running pipeline that is attempting to stop.");
                }

            if (!localThreadController.FrameExitTask.Task.IsCompleted)
            {
                // Push the current runspace if the session has changed
                this.UpdateRunspaceDetailsIfSessionChanged(sessionDetails, isDebuggerStop: true);

                // Raise the event for the debugger service
                this.DebuggerStop?.Invoke(sender, e);
            }

            this.logger.Write(LogLevel.Verbose, "Starting pipeline thread message loop...");

            Task<IPipelineExecutionRequest> localPipelineExecutionTask =
                localThreadController.TakeExecutionRequest();
            Task<DebuggerResumeAction> localDebuggerStoppedTask =
                localThreadController.Exit();
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

                    e.ResumeAction = localDebuggerStoppedTask.GetAwaiter().GetResult();
                    this.logger.Write(LogLevel.Verbose, "Received debugger resume action " + e.ResumeAction.ToString());

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
                    this.logger.Write(LogLevel.Verbose, "Received pipeline thread execution request.");

                    IPipelineExecutionRequest executionRequest = localPipelineExecutionTask.Result;
                    localPipelineExecutionTask = localThreadController.TakeExecutionRequest();
                    executionRequest.Execute().GetAwaiter().GetResult();

                    this.logger.Write(LogLevel.Verbose, "Pipeline thread execution completed.");

                    if (!this.CurrentRunspace.Runspace.Debugger.InBreakpoint)
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

            PromptNest.PopPromptContext();
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

        private void ConfigureRunspaceCapabilities(RunspaceDetails runspaceDetails)
        {
            DscBreakpointCapability.CheckForCapability(this.CurrentRunspace, this, this.logger);
        }

        private void PushRunspace(RunspaceDetails newRunspaceDetails)
        {
            this.logger.Write(
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

                    this.logger.Write(
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
                    this.logger.Write(
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
                    sessionDetails,
                    this.logger));
        }

        void IHostSupportsInteractiveSession.PopRunspace()
        {
            this.PopRunspace();
        }

        #endregion
    }
}
