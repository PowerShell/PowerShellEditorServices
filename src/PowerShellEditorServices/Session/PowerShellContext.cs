//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections;
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
    using System.Management.Automation.Runspaces;
    using System.Reflection;

    /// <summary>
    /// Manages the lifetime and usage of a PowerShell session.
    /// Handles nested PowerShell prompts and also manages execution of 
    /// commands whether inside or outside of the debugger.
    /// </summary>
    public class PowerShellContext : IDisposable
    {
        #region Fields

        private PowerShell powerShell;
        private IConsoleHost consoleHost;
        private bool ownsInitialRunspace;
        private Runspace initialRunspace;
        private Runspace currentRunspace;
        private ConsoleServicePSHost psHost;
        private InitialSessionState initialSessionState;
        private IVersionSpecificOperations versionSpecificOperations;
        private int pipelineThreadId;

        private TaskCompletionSource<DebuggerResumeAction> debuggerStoppedTask;
        private TaskCompletionSource<IPipelineExecutionRequest> pipelineExecutionTask;
        private TaskCompletionSource<IPipelineExecutionRequest> pipelineResultTask;

        private object runspaceMutex = new object();
        private AsyncQueue<RunspaceHandle> runspaceWaitQueue = new AsyncQueue<RunspaceHandle>();

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
                return this.debuggerStoppedTask != null;
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
        /// PowerShell Version of the current runspace.
        /// </summary>
        public Version PowerShellVersion
        {
            get; private set;
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

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class and
        /// opens a runspace to be used for the session.
        /// </summary>
        public PowerShellContext()
        {
            this.psHost = new ConsoleServicePSHost();
            this.initialSessionState = InitialSessionState.CreateDefault2();

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, this.initialSessionState);
            runspace.ApartmentState = ApartmentState.STA;
            runspace.ThreadOptions = PSThreadOptions.ReuseThread;
            runspace.Open();

            this.ownsInitialRunspace = true;

            this.Initialize(runspace);

            // Use reflection to execute ConsoleVisibility.AlwaysCaptureApplicationIO = true;
            Type consoleVisibilityType =
                Type.GetType(
                    "System.Management.Automation.ConsoleVisibility, System.Management.Automation, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

            if (consoleVisibilityType != null)
            {
                PropertyInfo propertyInfo =
                    consoleVisibilityType.GetProperty(
                        "AlwaysCaptureApplicationIO",
                        BindingFlags.Static | BindingFlags.Public);

                if (propertyInfo != null)
                {
                    propertyInfo.SetValue(null, true);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the PowerShellContext class using
        /// an existing runspace for the session.
        /// </summary>
        /// <param name="initialRunspace"></param>
        public PowerShellContext(Runspace initialRunspace)
        {
            this.Initialize(initialRunspace);
        }

        private void Initialize(Runspace initialRunspace)
        {
            Validate.IsNotNull("initialRunspace", initialRunspace);

            this.SessionState = PowerShellContextState.NotStarted;

            this.initialRunspace = initialRunspace;
            this.currentRunspace = initialRunspace;

            this.currentRunspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            this.currentRunspace.Debugger.DebuggerStop += OnDebuggerStop;

            this.powerShell = PowerShell.Create();
            this.powerShell.InvocationStateChanged += powerShell_InvocationStateChanged;
            this.powerShell.Runspace = this.currentRunspace;

            // TODO: Should this be configurable?
            this.SetExecutionPolicy(ExecutionPolicy.RemoteSigned);

            // Get the PowerShell runtime version
            this.PowerShellVersion = GetPowerShellVersion();

            // Write out the PowerShell version for tracking purposes
            Logger.Write(
                LogLevel.Normal,
                string.Format(
                    "PowerShell runtime version: {0}",
                    this.PowerShellVersion));

            if (PowerShellVersion >= new Version(5,0))
            {
                this.versionSpecificOperations = new PowerShell5Operations();
            }
            else if (PowerShellVersion.Major == 4)
            {
                this.versionSpecificOperations = new PowerShell4Operations();
            }
            else if (PowerShellVersion.Major == 3)
            {
                this.versionSpecificOperations = new PowerShell3Operations();
            }
            else
            {
                throw new NotSupportedException(
                    "This computer has an unsupported version of PowerShell installed: " +
                    PowerShellVersion.ToString());
            }

            // Configure the runspace's debugger
            this.versionSpecificOperations.ConfigureDebugger(
                this.currentRunspace);

            this.SessionState = PowerShellContextState.Ready;

            // Now that the runspace is ready, enqueue it for first use
            RunspaceHandle runspaceHandle = new RunspaceHandle(this.currentRunspace, this);
            this.runspaceWaitQueue.EnqueueAsync(runspaceHandle).Wait();
        }

        private Version GetPowerShellVersion()
        {
            try
            {
                var psVersionTable = this.currentRunspace.SessionStateProxy.GetVariable("PSVersionTable") as Hashtable;
                if (psVersionTable != null)
                {
                    var version = psVersionTable["PSVersion"] as Version;
                    if (version == null) return new Version(5, 0);
                    return version;
                }
            }
            catch (Exception ex)
            {
                Logger.Write(LogLevel.Warning, "Failed to look up PowerShell version. Defaulting to version 5. " + ex.Message);
            }

            return new Version(5, 0);
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
            RunspaceHandle runspaceHandle = null;
            IEnumerable<TResult> executionResult = null;

            // If the debugger is active and the caller isn't on the pipeline 
            // thread, send the command over to that thread to be executed.
            if (Thread.CurrentThread.ManagedThreadId != this.pipelineThreadId &&
                this.pipelineExecutionTask != null)
            {
                Logger.Write(LogLevel.Verbose, "Passing command execution to pipeline thread.");

                PipelineExecutionRequest<TResult> executionRequest =
                    new PipelineExecutionRequest<TResult>(
                        this, psCommand, sendOutputToHost);

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
                    if (sendOutputToHost)
                    {
                        psCommand.Commands[0].MergeMyResults(
                            PipelineResultTypes.Error,
                            PipelineResultTypes.Output);

                        psCommand.Commands.Add(
                            this.GetOutputCommand(
                                endOfStatement: false));
                    }

                    if (this.currentRunspace.RunspaceAvailability == RunspaceAvailability.AvailableForNestedCommand ||
                        this.debuggerStoppedTask != null)
                    {
                        Logger.Write(
                            LogLevel.Verbose,
                            string.Format(
                                "Attempting to execute nested pipeline command(s):\r\n\r\n{0}",
                                GetStringForPSCommand(psCommand)));

                        executionResult =
                            this.ExecuteCommandInDebugger<TResult>(
                                psCommand,
                                sendOutputToHost);
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
                                    this.powerShell.Commands = psCommand;
                                    Collection<TResult> result = this.powerShell.Invoke<TResult>();
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

                            Logger.Write(LogLevel.Error, errorMessage);
                        }
                        else
                        {
                            Logger.Write(
                                LogLevel.Verbose,
                                "Execution completed successfully.");
                        }

                        return executionResult;
                    }
                }
                catch (RuntimeException e)
                {
                    Logger.Write(
                        LogLevel.Error,
                        "Runtime exception occurred while executing command:\r\n\r\n" + e.ToString());

                    if (sendErrorToHost)
                    {
                        // Write the error to the host
                        this.WriteExceptionToHost(e);
                    }
                }
                finally
                {
                    // Get the new prompt before releasing the runspace handle
                    if (sendOutputToHost)
                    {
                        // Write the prompt
                        if (runspaceHandle != null)
                        {
                            this.WritePromptWithRunspace(runspaceHandle.Runspace);
                        }
                        else
                        {
                            this.WritePromptWithNestedPipeline();
                        }
                    }

                    // Dispose of the execution context
                    if (runspaceHandle != null)
                    {
                        runspaceHandle.Dispose();
                    }
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
        /// <param name="writeInputToHost">If true, causes the script string to be written to the host.</param>
        /// <param name="writeOutputToHost">If true, causes the script output to be written to the host.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public async Task<IEnumerable<object>> ExecuteScriptString(
            string scriptString,
            bool writeInputToHost,
            bool writeOutputToHost)
        {
            if (writeInputToHost)
            {
                this.WriteOutput(
                    scriptString + Environment.NewLine,
                    true);
            }

            PSCommand psCommand = new PSCommand();
            psCommand.AddScript(scriptString);

            return await this.ExecuteCommand<object>(psCommand, writeOutputToHost);
        }

        /// <summary>
        /// Executes a script file at the specified path.
        /// </summary>
        /// <param name="scriptPath">The path to the script file to execute.</param>
        /// <param name="arguments">Arguments to pass to the script.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task ExecuteScriptAtPath(string scriptPath, string arguments = null)
        {
            PSCommand command = new PSCommand();

            if (arguments != null)
            {
                // If we don't escape wildcard characters in the script path, the script can
                // fail to execute if say the script name was foo][.ps1.
                // Related to issue #123.
                string escapedScriptPath = EscapePath(scriptPath, escapeSpaces: true);
                string scriptWithArgs = escapedScriptPath + " " + arguments;

                command.AddScript(scriptWithArgs);
            }
            else
            {
                command.AddCommand(scriptPath);
            }

            await this.ExecuteCommand<object>(command, true);
        }

        /// <summary>
        /// Causes the current execution to be aborted no matter what state
        /// it is currently in.
        /// </summary>
        public void AbortExecution()
        {
            if (this.SessionState != PowerShellContextState.Aborting)
            {
                Logger.Write(LogLevel.Verbose, "Execution abort requested...");

                this.powerShell.BeginStop(null, null);
                this.SessionState = PowerShellContextState.Aborting;

                if (this.IsDebuggerStopped)
                {
                    this.ResumeDebugger(DebuggerResumeAction.Stop);
                }
            }
            else
            {
                Logger.Write(
                    LogLevel.Verbose,
                    "Execution abort requested while already aborting");
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
                this.currentRunspace);
        }

        internal void ResumeDebugger(DebuggerResumeAction resumeAction)
        {
            if (this.debuggerStoppedTask != null)
            {
                // Set the result so that the execution thread resumes.
                // The execution thread will clean up the task.
                this.debuggerStoppedTask.SetResult(resumeAction);
            }
            else
            {
                // TODO: Throw InvalidOperationException?
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

            if (this.ownsInitialRunspace && this.initialRunspace != null)
            {
                // TODO: Detach from events
                this.initialRunspace.Close();
                this.initialRunspace.Dispose();
                this.initialRunspace = null;
            }
        }

        internal void ReleaseRunspaceHandle(RunspaceHandle runspaceHandle)
        {
            Validate.IsNotNull("runspaceHandle", runspaceHandle);

            if (this.runspaceWaitQueue.IsEmpty)
            {
                var newRunspaceHandle = new RunspaceHandle(this.currentRunspace, this);
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
        public void SetWorkingDirectory(string path)
        {
            this.currentRunspace.SessionStateProxy.Path.SetLocation(path);
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
            Logger.Write(
                LogLevel.Verbose,
                string.Format(
                    "Session state changed --\r\n\r\n    Old state: {0}\r\n    New state: {1}\r\n    Result: {2}",
                    this.SessionState.ToString(),
                    e.NewSessionState.ToString(),
                    e.ExecutionResult));

            this.SessionState = e.NewSessionState;

            if (this.SessionStateChanged != null)
            {
                this.SessionStateChanged(sender, e);
            }
        }

        #endregion

        #region Private Methods

        private IEnumerable<TResult> ExecuteCommandInDebugger<TResult>(PSCommand psCommand, bool sendOutputToHost)
        {
            return this.versionSpecificOperations.ExecuteCommandInDebugger<TResult>(
                this,
                this.currentRunspace,
                psCommand,
                sendOutputToHost);
        }

        internal void WriteOutput(string outputString, bool includeNewLine)
        {
            if (this.ConsoleHost != null)
            {
                this.ConsoleHost.WriteOutput(
                    outputString,
                    includeNewLine,
                    OutputType.Normal);
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
                    // TODO: Collapse this so that the result shows that execution was aborted
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

                this.powerShell.Invoke();
                this.powerShell.Commands.Clear();

                // TODO: Ensure there were no errors?
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

        private void WritePromptToHost(Func<PSCommand, string> invokeAction)
        {
            string promptString = null;

            try
            {
                promptString = 
                    invokeAction(
                        new PSCommand().AddCommand("prompt"));
            }
            catch(RuntimeException e)
            {
                Logger.Write(
                    LogLevel.Verbose,
                    "Runtime exception occurred while executing prompt command:\r\n\r\n" + e.ToString());
            }
            finally
            {
                promptString = promptString ?? "PS >";
            }

            this.WriteOutput(
                Environment.NewLine,
                false);

            // Trim the '>' off the end of the prompt string to reduce
            // user confusion about where they can type.
            // TODO: Eventually put this behind a setting, #133
            promptString = promptString.TrimEnd(' ', '>', '\r', '\n');

            // Write the prompt string
            this.WriteOutput(
                promptString,
                true);
        }

        private void WritePromptWithRunspace(Runspace runspace)
        {
            this.WritePromptToHost(
                command =>
                {
                    this.powerShell.Commands = command;

                    return
                        this.powerShell
                            .Invoke<string>()
                            .FirstOrDefault();
                });
        }

        private void WritePromptWithNestedPipeline()
        {
            using (var pipeline = this.currentRunspace.CreateNestedPipeline())
            {
                this.WritePromptToHost(
                    command =>
                    {
                        pipeline.Commands.Clear();
                        pipeline.Commands.Add(command.Commands[0]);

                        return
                            pipeline
                                .Invoke()
                                .Select(pso => pso.BaseObject)
                                .Cast<string>()
                                .FirstOrDefault();
                    });
            }
        }
        
        #endregion

        #region Events

        // NOTE: This event is 'internal' because the DebugService provides
        //       the publicly consumable event.
        internal event EventHandler<DebuggerStopEventArgs> DebuggerStop;

        private void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            Logger.Write(LogLevel.Verbose, "Debugger stopped execution.");

            // Set the task so a result can be set
            this.debuggerStoppedTask =
                new TaskCompletionSource<DebuggerResumeAction>();

            // Save the pipeline thread ID and create the pipeline execution task
            this.pipelineThreadId = Thread.CurrentThread.ManagedThreadId;
            this.pipelineExecutionTask = new TaskCompletionSource<IPipelineExecutionRequest>();

            // Update the session state
            this.OnSessionStateChanged(
                this,
                new SessionStateChangedEventArgs(
                    PowerShellContextState.Ready,
                    PowerShellExecutionResult.Stopped,
                    null));

            // Write out the debugger prompt
            // TODO: Eventually re-enable this and put it behind a setting, #133
            //this.WritePromptWithNestedPipeline();

            // Raise the event for the debugger service
            if (this.DebuggerStop != null)
            {
                this.DebuggerStop(sender, e);
            }

            Logger.Write(LogLevel.Verbose, "Starting pipeline thread message loop...");

            while (true)
            {
                int taskIndex =
                    Task.WaitAny(
                        this.debuggerStoppedTask.Task,
                        this.pipelineExecutionTask.Task);

                if (taskIndex == 0)
                {
                    // Write a new output line before continuing
                    // TODO: Re-enable this with fix for #133
                    //this.WriteOutput("", true);

                    e.ResumeAction = this.debuggerStoppedTask.Task.Result;
                    Logger.Write(LogLevel.Verbose, "Received debugger resume action " + e.ResumeAction.ToString());

                    break;
                }
                else if (taskIndex == 1)
                {
                    Logger.Write(LogLevel.Verbose, "Received pipeline thread execution request.");

                    IPipelineExecutionRequest executionRequest =
                        this.pipelineExecutionTask.Task.Result;

                    this.pipelineExecutionTask = new TaskCompletionSource<IPipelineExecutionRequest>();

                    executionRequest.Execute().Wait();

                    Logger.Write(LogLevel.Verbose, "Pipeline thread execution completed.");

                    this.pipelineResultTask.SetResult(executionRequest);
                }
                else
                {
                    // TODO: How to handle this?
                }
            }

            // Clear the task so that it won't be used again
            this.debuggerStoppedTask = null;
        }

        // NOTE: This event is 'internal' because the DebugService provides
        //       the publicly consumable event.
        internal event EventHandler<BreakpointUpdatedEventArgs> BreakpointUpdated;

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            if (this.BreakpointUpdated != null)
            {
                this.BreakpointUpdated(sender, e);
            }
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
            bool sendOutputToHost;

            public IEnumerable<TResult> Results { get; private set; }

            public PipelineExecutionRequest(
                PowerShellContext powerShellContext,
                PSCommand psCommand,
                bool sendOutputToHost)
            {
                this.powerShellContext = powerShellContext;
                this.psCommand = psCommand;
                this.sendOutputToHost = sendOutputToHost;
            }

            public async Task Execute()
            {
                this.Results =
                    await this.powerShellContext.ExecuteCommand<TResult>(
                        psCommand,
                        sendOutputToHost);

                // TODO: Deal with errors?
            }
        }

        #endregion
    }
}

