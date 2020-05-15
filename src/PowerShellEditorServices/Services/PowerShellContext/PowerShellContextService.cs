//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation.Host;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services
{
    using System.Management.Automation;

    /// <summary>
    /// Manages the lifetime and usage of a PowerShell session.
    /// Handles nested PowerShell prompts and also manages execution of
    /// commands whether inside or outside of the debugger.
    /// </summary>
    internal class PowerShellContextService : IHostSupportsInteractiveSession
    {
        private static readonly string s_commandsModulePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "../../Commands/PowerShellEditorServices.Commands.psd1"));

        private static readonly Action<Runspace, ApartmentState> s_runspaceApartmentStateSetter;
        private static readonly PropertyInfo s_writeStreamProperty;
        private static readonly object s_errorStreamValue;

        [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "cctor needed for version specific initialization")]
        static PowerShellContextService()
        {
            // PowerShell ApartmentState APIs aren't available in PSStandard, so we need to use reflection.
            if (!VersionUtils.IsNetCore || VersionUtils.IsPS7OrGreater)
            {
                MethodInfo setterInfo = typeof(Runspace).GetProperty("ApartmentState").GetSetMethod();
                Delegate setter = Delegate.CreateDelegate(typeof(Action<Runspace, ApartmentState>), firstArgument: null, method: setterInfo);
                s_runspaceApartmentStateSetter = (Action<Runspace, ApartmentState>)setter;
            }

            if (VersionUtils.IsPS7OrGreater)
            {
                // Used to write ErrorRecords to the Error stream. Using Public and NonPublic because the plan is to make this property
                // public in 7.0.1
                s_writeStreamProperty = typeof(PSObject).GetProperty("WriteStream", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Type writeStreamType = typeof(PSObject).Assembly.GetType("System.Management.Automation.WriteStreamType");
                s_errorStreamValue = Enum.Parse(writeStreamType, "Error");
            }
        }

        #region Fields

        private readonly SemaphoreSlim resumeRequestHandle = AsyncUtils.CreateSimpleLockingSemaphore();
        private readonly SessionStateLock sessionStateLock = new SessionStateLock();

        private readonly OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer _languageServer;
        private readonly bool isPSReadLineEnabled;
        private readonly ILogger logger;

        private PowerShell powerShell;
        private bool ownsInitialRunspace;
        private RunspaceDetails initialRunspace;
        private SessionDetails mostRecentSessionDetails;

        private ProfilePathInfo profilePaths;

        private IVersionSpecificOperations versionSpecificOperations;

        private readonly Stack<RunspaceDetails> runspaceStack = new Stack<RunspaceDetails>();

        private int isCommandLoopRestarterSet;

        #endregion

        #region Properties

        private IPromptContext PromptContext { get; set; }

        private PromptNest PromptNest { get; set; }

        private InvocationEventQueue InvocationEventQueue { get; set; }

        private EngineIntrinsics EngineIntrinsics { get; set; }

        internal PSHost ExternalHost { get; set; }

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

        internal IHostInput ConsoleReader { get; private set; }

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

        internal bool IsDebugServerActive { get; set; }

        internal DebuggerStopEventArgs CurrentDebuggerStopEventArgs { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <param name="isPSReadLineEnabled">
        /// Indicates whether PSReadLine should be used if possible
        /// </param>
        public PowerShellContextService(
            ILogger logger,
            OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer languageServer,
            bool isPSReadLineEnabled)
        {
            _languageServer = languageServer;
            this.logger = logger;
            this.isPSReadLineEnabled = isPSReadLineEnabled;

            RunspaceChanged += PowerShellContext_RunspaceChangedAsync;
            ExecutionStatusChanged += PowerShellContext_ExecutionStatusChangedAsync;
        }

        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Checked by Validate call")]
        public static PowerShellContextService Create(
            ILoggerFactory factory,
            OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer languageServer,
            HostStartupInfo hostStartupInfo)
        {
            Validate.IsNotNull(nameof(hostStartupInfo), hostStartupInfo);

            var logger = factory.CreateLogger<PowerShellContextService>();

            bool shouldUsePSReadLine = hostStartupInfo.ConsoleReplEnabled
                && !hostStartupInfo.UsesLegacyReadLine;

            var powerShellContext = new PowerShellContextService(
                logger,
                languageServer,
                shouldUsePSReadLine);

            EditorServicesPSHostUserInterface hostUserInterface =
                hostStartupInfo.ConsoleReplEnabled
                    ? (EditorServicesPSHostUserInterface) new TerminalPSHostUserInterface(powerShellContext, hostStartupInfo.PSHost, logger)
                    : new ProtocolPSHostUserInterface(languageServer, powerShellContext, logger);

            EditorServicesPSHost psHost =
                new EditorServicesPSHost(
                    powerShellContext,
                    hostStartupInfo,
                    hostUserInterface,
                    logger);

            Runspace initialRunspace = PowerShellContextService.CreateRunspace(psHost, hostStartupInfo.LanguageMode);
            powerShellContext.Initialize(hostStartupInfo.ProfilePaths, initialRunspace, true, hostUserInterface);

            powerShellContext.ImportCommandsModuleAsync();

            // TODO: This can be moved to the point after the $psEditor object
            // gets initialized when that is done earlier than LanguageServer.Initialize
            foreach (string module in hostStartupInfo.AdditionalModules)
            {
                var command =
                    new PSCommand()
                        .AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                        .AddParameter("Name", module);

#pragma warning disable CS4014
                // This call queues the loading on the pipeline thread, so no need to await
                powerShellContext.ExecuteCommandAsync<PSObject>(
                    command,
                    sendOutputToHost: false,
                    sendErrorToHost: true);
#pragma warning restore CS4014
            }

            return powerShellContext;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="hostDetails"></param>
        /// <param name="powerShellContext"></param>
        /// <param name="hostUserInterface">The EditorServicesPSHostUserInterface to use for this instance.</param>
        /// <param name="logger">An ILogger implementation to use for this instance.</param>
        /// <returns></returns>
        public static Runspace CreateRunspace(
            HostStartupInfo hostDetails,
            PowerShellContextService powerShellContext,
            EditorServicesPSHostUserInterface hostUserInterface,
            ILogger logger)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            var psHost = new EditorServicesPSHost(powerShellContext, hostDetails, hostUserInterface, logger);
            powerShellContext.ConsoleWriter = hostUserInterface;
            powerShellContext.ConsoleReader = hostUserInterface;
            return CreateRunspace(psHost, hostDetails.LanguageMode);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="psHost">The PSHost that will be used for this Runspace.</param>
        /// <param name="languageMode">The language mode inherited from the orginal PowerShell process. This will be used when creating runspaces so that we honor the same language mode.</param>
        /// <returns></returns>
        public static Runspace CreateRunspace(PSHost psHost, PSLanguageMode languageMode)
        {
            InitialSessionState initialSessionState;
            if (Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1") {
                initialSessionState = InitialSessionState.CreateDefault();
            } else {
                initialSessionState = InitialSessionState.CreateDefault2();
            }

            // Create and initialize a new Runspace while honoring the LanguageMode of the original runspace
            // that started PowerShell Editor Services. This is because the PowerShell Integrated Console
            // should have the same LanguageMode of whatever is set by the system.
            initialSessionState.LanguageMode = languageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, initialSessionState);

            // Windows PowerShell must be hosted in STA mode
            // This must be set on the runspace *before* it is opened
            if (s_runspaceApartmentStateSetter != null)
            {
                s_runspaceApartmentStateSetter(runspace, ApartmentState.STA);
            }

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
            ProfilePathInfo profilePaths,
            Runspace initialRunspace,
            bool ownsInitialRunspace)
        {
            this.Initialize(profilePaths, initialRunspace, ownsInitialRunspace, consoleHost: null);
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
            ProfilePathInfo profilePaths,
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
                    connectionString: null);
            this.CurrentRunspace = this.initialRunspace;

            // Write out the PowerShell version for tracking purposes
            this.logger.LogInformation(
                string.Format(
                    "PowerShell runtime version: {0}, edition: {1}",
                    this.LocalPowerShellVersion.Version,
                    this.LocalPowerShellVersion.Edition));

            Version powerShellVersion = this.LocalPowerShellVersion.Version;
            if (powerShellVersion >= new Version(5, 0))
            {
                this.versionSpecificOperations = new PowerShell5Operations();
            }
            else
            {
                throw new NotSupportedException(
                    "This computer has an unsupported version of PowerShell installed: " +
                    powerShellVersion.ToString());
            }

            // Set up the runspace
            this.ConfigureRunspace(this.CurrentRunspace);

            // Add runspace capabilities
            this.ConfigureRunspaceCapabilities(this.CurrentRunspace);

            // Set the $profile variable in the runspace
            this.profilePaths = profilePaths;
            if (profilePaths != null)
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
            this.InvocationEventQueue = InvocationEventQueue.Create(this, this.PromptNest);

            if (powerShellVersion.Major >= 5 &&
                this.isPSReadLineEnabled &&
                PSReadLinePromptContext.TryGetPSReadLineProxy(logger, initialRunspace, out PSReadLineProxy proxy))
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

            if (VersionUtils.IsWindows)
            {
                this.SetExecutionPolicy();
            }
        }

        /// <summary>
        /// Imports the PowerShellEditorServices.Commands module into
        /// the runspace.  This method will be moved somewhere else soon.
        /// </summary>
        /// <returns></returns>
        public Task ImportCommandsModuleAsync() => ImportCommandsModuleAsync(s_commandsModulePath);

        public Task ImportCommandsModuleAsync(string path)
        {
            PSCommand importCommand = new PSCommand()
                .AddCommand("Import-Module")
                .AddArgument(path);

            return this.ExecuteCommandAsync<PSObject>(importCommand, sendOutputToHost: false, sendErrorToHost: false);
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
        public Task<RunspaceHandle> GetRunspaceHandleAsync()
        {
            return this.GetRunspaceHandleImplAsync(CancellationToken.None, isReadLine: false);
        }

        /// <summary>
        /// Gets a RunspaceHandle for the session's runspace.  This
        /// handle is used to gain temporary ownership of the runspace
        /// so that commands can be executed against it directly.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the request.</param>
        /// <returns>A RunspaceHandle instance that gives access to the session's runspace.</returns>
        public Task<RunspaceHandle> GetRunspaceHandleAsync(CancellationToken cancellationToken)
        {
            return this.GetRunspaceHandleImplAsync(cancellationToken, isReadLine: false);
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
        public Task<IEnumerable<TResult>> ExecuteCommandAsync<TResult>(
            PSCommand psCommand,
            bool sendOutputToHost = false,
            bool sendErrorToHost = true)
        {
            return ExecuteCommandAsync<TResult>(psCommand, errorMessages: null, sendOutputToHost, sendErrorToHost);
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
        public Task<IEnumerable<TResult>> ExecuteCommandAsync<TResult>(
            PSCommand psCommand,
            StringBuilder errorMessages,
            bool sendOutputToHost = false,
            bool sendErrorToHost = true,
            bool addToHistory = false)
        {
            return
                this.ExecuteCommandAsync<TResult>(
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
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Checked by Validate call")]
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "PowerShellContext must catch and log all exceptions to be robust")]
        public async Task<IEnumerable<TResult>> ExecuteCommandAsync<TResult>(
            PSCommand psCommand,
            StringBuilder errorMessages,
            ExecutionOptions executionOptions)
        {
            Validate.IsNotNull(nameof(psCommand), psCommand);
            Validate.IsNotNull(nameof(executionOptions), executionOptions);

            // Add history to PSReadLine before cancelling, otherwise it will be restored as the
            // cancelled prompt when it's called again.
            if (executionOptions.AddToHistory)
            {
                this.PromptContext.AddToHistory(executionOptions.InputString ?? psCommand.Commands[0].CommandText);
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
                this.logger.LogTrace("Passing command execution to pipeline thread.");

                if (shouldCancelReadLine && PromptNest.IsReadLineBusy())
                {
                    // If a ReadLine pipeline is running in the debugger then we'll hang here
                    // if we don't cancel it. Typically we can rely on OnExecutionStatusChanged but
                    // the pipeline request won't even start without clearing the current task.
                    this.ConsoleReader?.StopCommandLoop();
                }

                // Send the pipeline execution request to the pipeline thread
                return await threadController.RequestPipelineExecutionAsync(
                    new PipelineExecutionRequest<TResult>(
                        this,
                        psCommand,
                        errorMessages,
                        executionOptions)).ConfigureAwait(false);
            }

            Task writeErrorsToConsoleTask = null;
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
                    return await this.InvocationEventQueue.ExecuteCommandOnIdleAsync<TResult>(
                        psCommand,
                        errorMessages,
                        executionOptions).ConfigureAwait(false);
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

                runspaceHandle = await this.GetRunspaceHandleAsync(executionOptions.IsReadLine).ConfigureAwait(false);
                if (executionOptions.WriteInputToHost)
                {
                    this.WriteOutput(
                        executionOptions.InputString ?? psCommand.Commands[0].CommandText,
                        includeNewLine: true);
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
                        logger.LogError(
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

                this.logger.LogTrace(
                    string.Format(
                        "Attempting to execute command(s):\r\n\r\n{0}",
                        GetStringForPSCommand(psCommand)));


                PowerShell shell = this.PromptNest.GetPowerShell(executionOptions.IsReadLine);

                // Due to the following PowerShell bug, we can't just assign shell.Commands to psCommand
                // because PowerShell strips out CommandInfo:
                // https://github.com/PowerShell/PowerShell/issues/12297
                shell.Commands.Clear();
                foreach (Command command in psCommand.Commands)
                {
                    shell.Commands.AddCommand(command);
                }

                // Don't change our SessionState for ReadLine.
                if (!executionOptions.IsReadLine)
                {
                    await this.sessionStateLock.AcquireForExecuteCommandAsync().ConfigureAwait(false);
                    shell.InvocationStateChanged += PowerShell_InvocationStateChanged;
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

                    // May need a cancellation token here
                    return await Task.Run<IEnumerable<TResult>>(() => shell.Invoke<TResult>(input: null, invocationSettings), CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    if (!executionOptions.IsReadLine)
                    {
                        shell.InvocationStateChanged -= PowerShell_InvocationStateChanged;
                        await this.sessionStateLock.ReleaseForExecuteCommand().ConfigureAwait(false);
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
                        this.logger.LogError(errorMessage);

                        hadErrors = true;
                    }
                    else
                    {
                        this.logger.LogTrace(
                            "Execution completed successfully.");
                    }
                }
            }
            catch (PSRemotingDataStructureException e)
            {
                this.logger.LogError(
                    "Pipeline stopped while executing command:\r\n\r\n" + e.ToString());

                errorMessages?.Append(e.Message);
            }
            catch (PipelineStoppedException e)
            {
                this.logger.LogError(
                    "Pipeline stopped while executing command:\r\n\r\n" + e.ToString());

                errorMessages?.Append(e.Message);
            }
            catch (RuntimeException e)
            {
                this.logger.LogWarning(
                    "Runtime exception occurred while executing command:\r\n\r\n" + e.ToString());

                hadErrors = true;
                errorMessages?.Append(e.Message);

                if (executionOptions.WriteErrorsToHost)
                {
                    // Write the error to the host
                    // We must await this after the runspace handle has been released or we will deadlock
                    writeErrorsToConsoleTask = this.WriteExceptionToHostAsync(e);
                }
            }
            catch (Exception)
            {
                this.OnExecutionStatusChanged(
                    ExecutionStatus.Failed,
                    executionOptions,
                    true);

                throw;
            }
            finally
            {
                // If the RunspaceAvailability is None, it means that the runspace we're in is dead.
                // If this is the case, we should abort the execution which will clean up the runspace
                // (and clean up the debugger) and then pop it off the stack.
                // An example of when this happens is when the "attach" debug config is used and the
                // process you're attached to dies randomly.
                if (this.CurrentRunspace.Runspace.RunspaceAvailability == RunspaceAvailability.None)
                {
                    this.AbortExecution(shouldAbortDebugSession: true);
                    this.PopRunspace();
                }

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
                            runspaceHandle = await this.GetRunspaceHandleAsync().ConfigureAwait(false);
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
                    if (writeErrorsToConsoleTask != null)
                    {
                        await writeErrorsToConsoleTask.ConfigureAwait(false);
                    }
                }

                this.OnExecutionStatusChanged(
                    ExecutionStatus.Completed,
                    executionOptions,
                    hadErrors);
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
        public Task ExecuteCommandAsync(PSCommand psCommand)
        {
            return this.ExecuteCommandAsync<object>(psCommand);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptStringAsync(
            string scriptString)
        {
            return this.ExecuteScriptStringAsync(scriptString, false, true);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <param name="errorMessages">Error messages from PowerShell will be written to the StringBuilder.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptStringAsync(
            string scriptString,
            StringBuilder errorMessages)
        {
            return this.ExecuteScriptStringAsync(scriptString, errorMessages, false, true, false);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <param name="writeInputToHost">If true, causes the script string to be written to the host.</param>
        /// <param name="writeOutputToHost">If true, causes the script output to be written to the host.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptStringAsync(
            string scriptString,
            bool writeInputToHost,
            bool writeOutputToHost)
        {
            return this.ExecuteScriptStringAsync(scriptString, null, writeInputToHost, writeOutputToHost, false);
        }

        /// <summary>
        /// Executes a script string in the session's runspace.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <param name="writeInputToHost">If true, causes the script string to be written to the host.</param>
        /// <param name="writeOutputToHost">If true, causes the script output to be written to the host.</param>
        /// <param name="addToHistory">If true, adds the command to the user's command history.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public Task<IEnumerable<object>> ExecuteScriptStringAsync(
            string scriptString,
            bool writeInputToHost,
            bool writeOutputToHost,
            bool addToHistory)
        {
            return this.ExecuteScriptStringAsync(scriptString, null, writeInputToHost, writeOutputToHost, addToHistory);
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
        public Task<IEnumerable<object>> ExecuteScriptStringAsync(
            string scriptString,
            StringBuilder errorMessages,
            bool writeInputToHost,
            bool writeOutputToHost,
            bool addToHistory)
        {
            Validate.IsNotNull(nameof(scriptString), scriptString);

            PSCommand command = null;
            if(CurrentRunspace.Runspace.SessionStateProxy.LanguageMode != PSLanguageMode.FullLanguage)
            {
                try
                {
                    var scriptBlock = ScriptBlock.Create(scriptString);
                    PowerShell ps = scriptBlock.GetPowerShell(isTrustedInput: false, null);
                    command = ps.Commands;
                }
                catch (Exception e)
                {
                    logger.LogException("Exception getting trusted/untrusted PSCommand.", e);
                }
            }

            // fall back to old behavior
            if(command == null)
            {
                command = new PSCommand().AddScript(scriptString.Trim());
            }

            return this.ExecuteCommandAsync<object>(
                command,
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
        public async Task ExecuteScriptWithArgsAsync(string script, string arguments = null, bool writeInputToHost = false)
        {
            Validate.IsNotNull(nameof(script), script);

            PSCommand command = new PSCommand();

            if (arguments != null)
            {
                // Need to determine If the script string is a path to a script file.
                string scriptAbsPath = string.Empty;
                try
                {
                    // Assume we can only debug scripts from the FileSystem provider
                    string workingDir = (await ExecuteCommandAsync<PathInfo>(
                        new PSCommand()
                            .AddCommand("Microsoft.PowerShell.Management\\Get-Location")
                            .AddParameter("PSProvider", "FileSystem"),
                            sendOutputToHost: false,
                            sendErrorToHost: false).ConfigureAwait(false))
                        .FirstOrDefault()
                        .ProviderPath;

                    workingDir = workingDir.TrimEnd(Path.DirectorySeparatorChar);
                    scriptAbsPath = workingDir + Path.DirectorySeparatorChar + script;
                }
                catch (System.Management.Automation.DriveNotFoundException e)
                {
                    this.logger.LogError(
                        "Could not determine current filesystem location:\r\n\r\n" + e.ToString());
                }

                var strBld = new StringBuilder();

                // The script parameter can refer to either a "script path" or a "command name".  If it is a
                // script path, we can determine that by seeing if the path exists.  If so, we always single
                // quote that path in case it includes special PowerShell characters like ', &, (, ), [, ] and
                // <space>.  Any embedded single quotes are escaped.
                // If the provided path is already quoted, then File.Exists will not find it.
                // This keeps us from quoting an already quoted path.
                // Related to issue #123.
                if (File.Exists(script) || File.Exists(scriptAbsPath))
                {
                    // Dot-source the launched script path and single quote the path in case it includes
                    strBld.Append(". ").Append(QuoteEscapeString(script));
                }
                else
                {
                    strBld.Append(script);
                }

                // Add arguments
                strBld.Append(' ').Append(arguments);

                var launchedScript = strBld.ToString();
                this.logger.LogTrace($"Launch script is: {launchedScript}");

                command.AddScript(launchedScript, false);
            }
            else
            {
                // AddCommand can handle script paths including those with special chars e.g.:
                // ".\foo & [bar]\foo.ps1" and it can handle arbitrary commands, like "Invoke-Pester"
                command.AddCommand(script, false);
            }


            await this.ExecuteCommandAsync<object>(
                    command,
                    errorMessages: null,
                    new ExecutionOptions
                    {
                        WriteInputToHost = true,
                        WriteOutputToHost = true,
                        WriteErrorsToHost = true,
                        AddToHistory = true,
                    }).ConfigureAwait(false);
        }

        /// <summary>
        /// Forces the <see cref="PromptContext" /> to trigger PowerShell event handling,
        /// reliquishing control of the pipeline thread during event processing.
        /// </summary>
        /// <remarks>
        /// This method is called automatically by <see cref="InvokeOnPipelineThreadAsync" /> and
        /// <see cref="ExecuteCommandAsync" />. Consider using them instead of this method directly when
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
        /// This method is called automatically by <see cref="ExecuteCommandAsync" />. Consider using
        /// that method instead of calling this directly when possible.
        /// </remarks>
        internal Task InvokeOnPipelineThreadAsync(Action<PowerShell> invocationAction)
        {
            if (this.PromptNest.IsReadLineBusy())
            {
                return this.InvocationEventQueue.InvokeOnPipelineThreadAsync(invocationAction);
            }

            // If this is invoked when ReadLine isn't busy then there shouldn't be any running
            // pipelines. Right now this method is only used by command completion which doesn't
            // actually require running on the pipeline thread, as long as nothing else is running.
            invocationAction.Invoke(this.PromptNest.GetPowerShell());
            return Task.CompletedTask;
        }

        internal async Task<string> InvokeReadLineAsync(bool isCommandLine, CancellationToken cancellationToken)
        {
            return await PromptContext.InvokeReadLineAsync(
                isCommandLine,
                cancellationToken).ConfigureAwait(false);
        }

        internal static TResult ExecuteScriptAndGetItem<TResult>(
            string scriptToExecute,
            Runspace runspace,
            TResult defaultValue = default,
            bool useLocalScope = false)
        {
            using (PowerShell pwsh = PowerShell.Create())
            {
                pwsh.Runspace = runspace;
                IEnumerable<TResult> results = pwsh.AddScript(scriptToExecute, useLocalScope).Invoke<TResult>();
                return results.DefaultIfEmpty(defaultValue).First();
            }
        }

        /// <summary>
        /// Loads PowerShell profiles for the host from the specified
        /// profile locations.  Only the profile paths which exist are
        /// loaded.
        /// </summary>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task LoadHostProfilesAsync()
        {
            if (this.profilePaths == null)
            {
                return;
            }

            // Load any of the profile paths that exist
            var command = new PSCommand();
            bool hasLoadablePath = false;
            foreach (var profilePath in GetLoadableProfilePaths(this.profilePaths))
            {
                hasLoadablePath = true;
                command.AddCommand(profilePath, false).AddStatement();
            }

            if (!hasLoadablePath)
            {
                return;
            }

            await ExecuteCommandAsync<object>(command, sendOutputToHost: true).ConfigureAwait(false);

            // Gather the session details (particularly the prompt) after
            // loading the user's profiles.
            await this.GetSessionDetailsInRunspaceAsync().ConfigureAwait(false);
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
            if (this.SessionState == PowerShellContextState.Aborting
                || this.SessionState == PowerShellContextState.Disposed)
            {
                this.logger.LogTrace(
                    string.Format(
                        $"Execution abort requested when already aborted (SessionState = {this.SessionState})"));
                return;
            }

            this.logger.LogTrace("Execution abort requested...");

            if (shouldAbortDebugSession)
            {
                this.ExitAllNestedPrompts();
            }

            if (this.PromptNest.IsInDebugger)
            {
                this.versionSpecificOperations.StopCommandInDebugger(this);
                if (shouldAbortDebugSession)
                {
                    this.ResumeDebugger(DebuggerResumeAction.Stop);
                }
            }
            else
            {
                this.PromptNest.GetPowerShell(isReadLine: false).BeginStop(null, null);
            }

            // TODO:
            // This lock and state reset are a temporary fix at best.
            // We need to investigate how the debugger should be interacting
            // with PowerShell in this cancellation scenario.
            //
            // Currently we try to acquire a lock on the execution status changed event.
            // If we can't, it's because a command is executing, so we shouldn't change the status.
            // If we can, we own the status and should fire the event.
            if (this.sessionStateLock.TryAcquireForDebuggerAbort())
            {
                try
                {
                    this.SessionState = PowerShellContextState.Aborting;
                    this.OnExecutionStatusChanged(
                        ExecutionStatus.Aborted,
                        null,
                        false);
                }
                finally
                {
                    this.SessionState = PowerShellContextState.Ready;
                    this.sessionStateLock.ReleaseForDebuggerAbort();
                }
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
                await this.PromptNest.WaitForCurrentFrameExitAsync(frame => this.ExitNestedPrompt()).ConfigureAwait(false);
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
            this.logger.LogTrace("Debugger break requested...");

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
                                this.ConsoleReader?.StopCommandLoop();
                                if (this.SessionState != PowerShellContextState.Ready)
                                {
                                    this.versionSpecificOperations.StopCommandInDebugger(this);
                                }
                            });
                    }
                    else
                    {
                        this.PromptNest.GetThreadController().StartThreadExit(resumeAction);
                        this.ConsoleReader?.StopCommandLoop();
                        if (this.SessionState != PowerShellContextState.Ready)
                        {
                            this.versionSpecificOperations.StopCommandInDebugger(this);
                        }
                    }
                }
                else
                {
                    this.logger.LogError(
                        $"Tried to resume debugger with action {resumeAction} but there was no debuggerStoppedTask.");
                }
            }
            finally
            {
                resumeRequestHandle.Release();
            }
        }

        /// <summary>
        /// Closes the runspace and any other resources being used
        /// by this PowerShellContext.
        /// </summary>
        public void Close()
        {
            logger.LogDebug("Closing PowerShellContextService...");
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

        private Task<RunspaceHandle> GetRunspaceHandleAsync(bool isReadLine)
        {
            return this.GetRunspaceHandleImplAsync(CancellationToken.None, isReadLine);
        }

        private Task<RunspaceHandle> GetRunspaceHandleImplAsync(CancellationToken cancellationToken, bool isReadLine)
        {
            return this.PromptNest.GetRunspaceHandleAsync(cancellationToken, isReadLine);
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
                    this.logger.LogError(
                        $"Caught {exitException.GetType().Name} while exiting {runspaceDetails.Location} runspace:\r\n{exitException.ToString()}");
                }
            }
        }

        internal void ReleaseRunspaceHandle(RunspaceHandle runspaceHandle)
        {
            Validate.IsNotNull("runspaceHandle", runspaceHandle);

            if (PromptNest.IsMainThreadBusy() || (runspaceHandle.IsReadLine && PromptNest.IsReadLineBusy()))
            {
                _ = PromptNest
                    .ReleaseRunspaceHandleAsync(runspaceHandle)
                    .ConfigureAwait(false);
            }
            else
            {
                // Write the situation to the log since this shouldn't happen
                this.logger.LogError(
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
            this.ConsoleReader?.StopCommandLoop();
            this.ConsoleReader?.StartCommandLoop();

            var localPipelineExecutionTask = localThreadController.TakeExecutionRequestAsync();
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
                    localPipelineExecutionTask = localThreadController.TakeExecutionRequestAsync();
                    localExecutionTask.ExecuteAsync().GetAwaiter().GetResult();
                    continue;
                }

                this.ConsoleReader?.StopCommandLoop();
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
                this.logger.LogError(
                    "ExitNestedPrompt was called outside of a nested prompt.");
                return;
            }

            // Stop the command input loop so PSReadLine isn't invoked between ExitNestedPrompt
            // being invoked and EnterNestedPrompt getting the message to exit.
            this.ConsoleReader?.StopCommandLoop();
            this.PromptNest.GetThreadController().StartThreadExit(DebuggerResumeAction.Stop);
        }

        /// <summary>
        /// Sets the current working directory of the powershell context.  The path should be
        /// unescaped before calling this method.
        /// </summary>
        /// <param name="path"></param>
        public Task SetWorkingDirectoryAsync(string path)
        {
            return this.SetWorkingDirectoryAsync(path, isPathAlreadyEscaped: true);
        }

        /// <summary>
        /// Sets the current working directory of the powershell context.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isPathAlreadyEscaped">Specify false to have the path escaped, otherwise specify true if the path has already been escaped.</param>
        public async Task SetWorkingDirectoryAsync(string path, bool isPathAlreadyEscaped)
        {
            Validate.IsNotNull(nameof(path), path);
            this.InitialWorkingDirectory = path;

            if (!isPathAlreadyEscaped)
            {
                path = WildcardEscapePath(path);
            }

            await ExecuteCommandAsync<PSObject>(
                new PSCommand().AddCommand("Set-Location").AddParameter("Path", path),
                errorMessages: null,
                sendOutputToHost: false,
                sendErrorToHost: false,
                addToHistory: false).ConfigureAwait(false);
        }

        /// <summary>
        /// Fully escape a given path for use in PowerShell script.
        /// Note: this will not work with PowerShell.AddParameter()
        /// </summary>
        /// <param name="path">The path to escape.</param>
        /// <returns>An escaped version of the path that can be embedded in PowerShell script.</returns>
        internal static string FullyPowerShellEscapePath(string path)
        {
            string wildcardEscapedPath = WildcardEscapePath(path);
            return QuoteEscapeString(wildcardEscapedPath);
        }

        /// <summary>
        /// Wrap a string in quotes to make it safe to use in scripts.
        /// </summary>
        /// <param name="escapedPath">The glob-escaped path to wrap in quotes.</param>
        /// <returns>The given path wrapped in quotes appropriately.</returns>
        internal static string QuoteEscapeString(string escapedPath)
        {
            var sb = new StringBuilder(escapedPath.Length + 2); // Length of string plus two quotes
            sb.Append('\'');
            if (!escapedPath.Contains('\''))
            {
                sb.Append(escapedPath);
            }
            else
            {
                foreach (char c in escapedPath)
                {
                    if (c == '\'')
                    {
                        sb.Append("''");
                        continue;
                    }

                    sb.Append(c);
                }
            }
            sb.Append('\'');
            return sb.ToString();
        }

        /// <summary>
        /// Return the given path with all PowerShell globbing characters escaped,
        /// plus optionally the whitespace.
        /// </summary>
        /// <param name="path">The path to process.</param>
        /// <param name="escapeSpaces">Specify True to escape spaces in the path, otherwise False.</param>
        /// <returns>The path with [ and ] escaped.</returns>
        internal static string WildcardEscapePath(string path, bool escapeSpaces = false)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < path.Length; i++)
            {
                char curr = path[i];
                switch (curr)
                {
                    // Escape '[', ']', '?' and '*' with '`'
                    case '[':
                    case ']':
                    case '*':
                    case '?':
                    case '`':
                        sb.Append('`').Append(curr);
                        break;

                    default:
                        // Escape whitespace if required
                        if (escapeSpaces && char.IsWhiteSpace(curr))
                        {
                            sb.Append('`').Append(curr);
                            break;
                        }
                        sb.Append(curr);
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the passed in path with the [ and ] characters escaped. Escaping spaces is optional.
        /// </summary>
        /// <param name="path">The path to process.</param>
        /// <param name="escapeSpaces">Specify True to escape spaces in the path, otherwise False.</param>
        /// <returns>The path with [ and ] escaped.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("This API is not meant for public usage and should not be used.")]
        public static string EscapePath(string path, bool escapeSpaces)
        {
            Validate.IsNotNull(nameof(path), path);

            return WildcardEscapePath(path, escapeSpaces);
        }

        internal static string UnescapeWildcardEscapedPath(string wildcardEscapedPath)
        {
            // Prevent relying on my implementation if we can help it
            if (!wildcardEscapedPath.Contains('`'))
            {
                return wildcardEscapedPath;
            }

            var sb = new StringBuilder(wildcardEscapedPath.Length);
            for (int i = 0; i < wildcardEscapedPath.Length; i++)
            {
                // If we see a backtick perform a lookahead
                char curr = wildcardEscapedPath[i];
                if (curr == '`' && i + 1 < wildcardEscapedPath.Length)
                {
                    // If the next char is an escapable one, don't add this backtick to the new string
                    char next = wildcardEscapedPath[i + 1];
                    switch (next)
                    {
                        case '[':
                        case ']':
                        case '?':
                        case '*':
                            continue;

                        default:
                            if (char.IsWhiteSpace(next))
                            {
                                continue;
                            }
                            break;
                    }
                }

                sb.Append(curr);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Unescapes any escaped [, ] or space characters. Typically use this before calling a
        /// .NET API that doesn't understand PowerShell escaped chars.
        /// </summary>
        /// <param name="path">The path to unescape.</param>
        /// <returns>The path with the ` character before [, ] and spaces removed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("This API is not meant for public usage and should not be used.")]
        public static string UnescapePath(string path)
        {
            Validate.IsNotNull(nameof(path), path);

            return UnescapeWildcardEscapedPath(path);
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
                this.logger.LogTrace(
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
                this.logger.LogWarning(
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

        private void PowerShellContext_RunspaceChangedAsync(object sender, RunspaceChangedEventArgs e)
        {
            _languageServer?.SendNotification(
                "powerShell/runspaceChanged",
                new MinifiedRunspaceDetails(e.NewRunspace));
        }


        // TODO: Refactor this, RunspaceDetails, PowerShellVersion, and PowerShellVersionDetails
        // It's crazy that this is 4 different types.
        // P.S. MinifiedRunspaceDetails use to be called RunspaceDetails... as in, there were 2 DIFFERENT
        // RunspaceDetails types in this codebase but I've changed it to be minified since the type is
        // slightly simpler than the other RunspaceDetails.
        internal class MinifiedRunspaceDetails
        {
            public PowerShellVersion PowerShellVersion { get; set; }

            public RunspaceLocation RunspaceType { get; set; }

            public string ConnectionString { get; set; }

            public MinifiedRunspaceDetails()
            {
            }

            public MinifiedRunspaceDetails(RunspaceDetails eventArgs)
            {
                Validate.IsNotNull(nameof(eventArgs), eventArgs);

                this.PowerShellVersion = new PowerShellVersion(eventArgs.PowerShellVersion);
                this.RunspaceType = eventArgs.Location;
                this.ConnectionString = eventArgs.ConnectionString;
            }
        }

        /// <summary>
        /// Event hook on the PowerShell context to listen for changes in script execution status
        /// </summary>
        /// <param name="sender">the PowerShell context sending the execution event</param>
        /// <param name="e">details of the execution status change</param>
        private void PowerShellContext_ExecutionStatusChangedAsync(object sender, ExecutionStatusChangedEventArgs e)
        {
            // The cancelling of the prompt (PSReadLine) causes an ExecutionStatus.Aborted to be sent after every
            // actual execution (ExecutionStatus.Running) on the pipeline. We ignore that event since it's counterintuitive to
            // the goal of this method which is to send updates when the pipeline is actually running something.
            // In the event that we don't know if it was a ReadLine cancel, we default to sending the notification.
            var options = e?.ExecutionOptions;
            if (options == null || !options.IsReadLine)
            {
                _languageServer?.SendNotification(
                    "powerShell/executionStatusChanged",
                    e);
            }
        }

        #endregion

        #region Private Methods

        private IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(PSCommand psCommand, bool sendOutputToHost)
        {
            this.logger.LogTrace(
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

        private Task WriteExceptionToHostAsync(RuntimeException e)
        {
            var psObject = PSObject.AsPSObject(e.ErrorRecord);

            // Used to write ErrorRecords to the Error stream so they are rendered in the console correctly.
            if (VersionUtils.IsPS7OrGreater)
            {
                s_writeStreamProperty.SetValue(psObject, s_errorStreamValue);
            }
            else
            {
                var note = new PSNoteProperty("writeErrorStream", true);
                psObject.Properties.Add(note);
            }

            return ExecuteCommandAsync(new PSCommand().AddCommand("Microsoft.PowerShell.Core\\Out-Default").AddParameter("InputObject", psObject));
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

        void PowerShell_InvocationStateChanged(object sender, PSInvocationStateChangedEventArgs e)
        {
            SessionStateChangedEventArgs eventArgs = TranslateInvocationStateInfo(e.InvocationStateInfo);
            this.OnSessionStateChanged(this, eventArgs);
        }

        private static SessionStateChangedEventArgs TranslateInvocationStateInfo(PSInvocationStateInfo invocationState)
        {
            PowerShellExecutionResult executionResult = PowerShellExecutionResult.NotFinished;

            PowerShellContextState newState;
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

        private void SetExecutionPolicy()
        {
            // We want to get the list hierarchy of execution policies
            // Calling the cmdlet is the simplest way to do that
            IReadOnlyList<PSObject> policies = this.powerShell
                .AddCommand("Microsoft.PowerShell.Security\\Get-ExecutionPolicy")
                    .AddParameter("-List")
                .Invoke();

            this.powerShell.Commands.Clear();

            // The policies come out in the following order:
            // - MachinePolicy
            // - UserPolicy
            // - Process
            // - CurrentUser
            // - LocalMachine
            // We want to ignore policy settings, since we'll already have those anyway.
            // Then we need to look at the CurrentUser setting, and then the LocalMachine setting.
            //
            // Get-ExecutionPolicy -List emits PSObjects with Scope and ExecutionPolicy note properties
            // set to expected values, so we must sift through those.

            ExecutionPolicy policyToSet = ExecutionPolicy.Bypass;
            var currentUserPolicy = (ExecutionPolicy)policies[policies.Count - 2].Members["ExecutionPolicy"].Value;
            if (currentUserPolicy != ExecutionPolicy.Undefined)
            {
                policyToSet = currentUserPolicy;
            }
            else
            {
                var localMachinePolicy = (ExecutionPolicy)policies[policies.Count - 1].Members["ExecutionPolicy"].Value;
                if (localMachinePolicy != ExecutionPolicy.Undefined)
                {
                    policyToSet = localMachinePolicy;
                }
            }

            // If there's nothing to do, save ourselves a PowerShell invocation
            if (policyToSet == ExecutionPolicy.Bypass)
            {
                this.logger.LogTrace("Execution policy already set to Bypass. Skipping execution policy set");
                return;
            }

            // Finally set the inherited execution policy
            this.logger.LogTrace("Setting execution policy to {Policy}", policyToSet);
            try
            {
                this.powerShell
                    .AddCommand("Microsoft.PowerShell.Security\\Set-ExecutionPolicy")
                    .AddParameter("Scope", ExecutionPolicyScope.Process)
                    .AddParameter("ExecutionPolicy", policyToSet)
                    .AddParameter("Force")
                    .Invoke();
            }
            catch (CmdletInvocationException e)
            {
                this.logger.LogError(e, "Error occurred calling 'Set-ExecutionPolicy -Scope Process -ExecutionPolicy {Policy} -Force'", policyToSet);
            }
            finally
            {
                this.powerShell.Commands.Clear();
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
                this.logger.LogTrace(
                    "Runtime exception occurred while gathering runspace info:\r\n\r\n" + e.ToString());
            }
            catch (ArgumentNullException)
            {
                this.logger.LogError(
                    "Could not retrieve session details but no exception was thrown.");
            }

            // TODO: Return a harmless object if necessary
            this.mostRecentSessionDetails = null;
            return this.mostRecentSessionDetails;
        }

        private async Task<SessionDetails> GetSessionDetailsInRunspaceAsync()
        {
            using (RunspaceHandle runspaceHandle = await this.GetRunspaceHandleAsync().ConfigureAwait(false))
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
            // We don't need to check what thread we're on here. If it's a local
            // nested pipeline then we will already be on the correct thread, and
            // non-debugger nested pipelines aren't supported in remote runspaces.
            return this.GetSessionDetails(
                command =>
                {
                    using (var localPwsh = PowerShell.Create(RunspaceMode.CurrentRunspace))
                    {
                        localPwsh.Commands = command;
                        return localPwsh.Invoke().FirstOrDefault();
                    }
                });
        }

        private void SetProfileVariableInCurrentRunspace(ProfilePathInfo profilePaths)
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

            this.logger.LogTrace(
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

        private static IEnumerable<string> GetLoadableProfilePaths(ProfilePathInfo profilePaths)
        {
            if (profilePaths == null)
            {
                yield break;
            }

            foreach (string path in new [] { profilePaths.AllUsersAllHosts, profilePaths.AllUsersCurrentHost, profilePaths.CurrentUserAllHosts, profilePaths.CurrentUserCurrentHost })
            {
                if (path != null && File.Exists(path))
                {
                    yield return path;
                }
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
            if (Interlocked.CompareExchange(ref this.isCommandLoopRestarterSet, 1, 1) == 1)
            {
                return;
            }

            void availabilityChangedHandler(object runspace, RunspaceAvailabilityEventArgs eventArgs)
            {
                if (eventArgs.RunspaceAvailability != RunspaceAvailability.Available ||
                    this.versionSpecificOperations.IsDebuggerStopped(this.PromptNest, (Runspace)runspace))
                {
                    return;
                }

                ((Runspace)runspace).AvailabilityChanged -= availabilityChangedHandler;
                Interlocked.Exchange(ref this.isCommandLoopRestarterSet, 0);
                this.ConsoleReader?.StartCommandLoop();
            }

            this.CurrentRunspace.Runspace.AvailabilityChanged += availabilityChangedHandler;
            Interlocked.Exchange(ref this.isCommandLoopRestarterSet, 1);
        }

        private void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            // We maintain the current stop event args so that we can use it in the DebugServer to fire the "stopped" event
            // when the DebugServer is fully started.
            CurrentDebuggerStopEventArgs = e;

            if (!IsDebugServerActive)
            {
                _languageServer.SendNotification("powerShell/startDebugger");
            }

            // We've hit a breakpoint so go to a new line so that the prompt can be rendered.
            this.WriteOutput("", includeNewLine: true);

            if (CurrentRunspace.Context == RunspaceContext.Original)
            {
                StartCommandLoopOnRunspaceAvailable();
            }

            this.logger.LogTrace("Debugger stopped execution.");

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
                this.logger.LogTrace(
                    "Attempting to get session details failed, most likely due to a running pipeline that is attempting to stop.");
            }

            if (!localThreadController.FrameExitTask.Task.IsCompleted)
            {
                // Push the current runspace if the session has changed
                this.UpdateRunspaceDetailsIfSessionChanged(sessionDetails, isDebuggerStop: true);

                // Raise the event for the debugger service
                this.DebuggerStop?.Invoke(sender, e);
            }

            this.logger.LogTrace("Starting pipeline thread message loop...");

            Task<IPipelineExecutionRequest> localPipelineExecutionTask =
                localThreadController.TakeExecutionRequestAsync();
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
                    this.logger.LogTrace("Received debugger resume action " + e.ResumeAction.ToString());

                    // Since we are no longer at a breakpoint, we set this to null.
                    CurrentDebuggerStopEventArgs = null;

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
                    this.logger.LogTrace("Received pipeline thread execution request.");

                    IPipelineExecutionRequest executionRequest = localPipelineExecutionTask.Result;
                    localPipelineExecutionTask = localThreadController.TakeExecutionRequestAsync();
                    executionRequest.ExecuteAsync().GetAwaiter().GetResult();

                    this.logger.LogTrace("Pipeline thread execution completed.");

                    if (!this.versionSpecificOperations.IsDebuggerStopped(
                        this.PromptNest,
                        this.CurrentRunspace.Runspace))
                    {
                        // Since we are no longer at a breakpoint, we set this to null.
                        CurrentDebuggerStopEventArgs = null;

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
            this.logger.LogTrace(
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

                    this.logger.LogTrace(
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
                    this.logger.LogError(
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

        /// <summary>
        /// Encapsulates the locking semantics hacked together for debugging to work.
        /// This allows ExecuteCommandAsync locking to work "re-entrantly",
        /// while making sure that a debug abort won't corrupt state.
        /// </summary>
        private class SessionStateLock
        {
            /// <summary>
            /// The actual lock to acquire to modify the session state of the PowerShellContextService.
            /// </summary>
            private readonly SemaphoreSlim _sessionStateLock;

            /// <summary>
            /// A lock used by this class to ensure that count incrementing and session state locking happens atomically.
            /// </summary>
            private readonly SemaphoreSlim _internalLock;

            /// <summary>
            /// A count of how re-entrant the current execute command lock call is,
            /// so we can effectively use it as a two-way semaphore.
            /// </summary>
            private int _executeCommandLockCount;

            public SessionStateLock()
            {
                _sessionStateLock = AsyncUtils.CreateSimpleLockingSemaphore();
                _internalLock = AsyncUtils.CreateSimpleLockingSemaphore();
                _executeCommandLockCount = 0;
            }

            public async Task AcquireForExecuteCommandAsync()
            {
                // Algorithm here is:
                // - Acquire the internal lock to keep operations atomic
                // - Increment the number of lock holders
                // - If we're the only one, acquire the lock
                // - Release the internal lock

                await _internalLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (_executeCommandLockCount++ == 0)
                    {
                        await _sessionStateLock.WaitAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    _internalLock.Release();
                }
            }

            public bool TryAcquireForDebuggerAbort()
            {
                return _sessionStateLock.Wait(0);
            }

            public async Task ReleaseForExecuteCommand()
            {
                // Algorithm here is the opposite of the acquisition algorithm:
                // - Acquire the internal lock to ensure the operation is atomic
                // - Decrement the lock holder count
                // - If we were the last ones, release the lock
                // - Release the internal lock

                await _internalLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (--_executeCommandLockCount == 0)
                    {
                        _sessionStateLock.Release();
                    }
                }
                finally
                {
                    _internalLock.Release();
                }
            }

            public void ReleaseForDebuggerAbort()
            {
                _sessionStateLock.Release();
            }
        }
    }
}
