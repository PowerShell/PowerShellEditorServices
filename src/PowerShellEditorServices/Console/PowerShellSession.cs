using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    using Nito.AsyncEx;
    using System.Management.Automation;
    using System.Management.Automation.Host;
    using System.Management.Automation.Runspaces;
    using System.Threading;

    /// <summary>
    /// Enumerates the possible states for a PowerShellSession.
    /// </summary>
    public enum PowerShellSessionState
    {
        /// <summary>
        /// Indicates an unknown, potentially uninitialized state.
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Indicates the state where the session is starting but 
        /// not yet fully initialized.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Indicates that the session is ready to accept commands
        /// for execution.
        /// </summary>
        Ready,
        
        /// <summary>
        /// Indicates that the session is currently running a command.
        /// </summary>
        Running,

        /// <summary>
        /// Indicates that the session is aborting the current execution.
        /// </summary>
        Aborting,

        /// <summary>
        /// Indicates that the session is already disposed and cannot
        /// accept further execution requests.
        /// </summary>
        Disposed
    }

    public enum PowerShellExecutionResult
    {
        NotFinished,

        Failed,

        Aborted,

        Stopped,

        Completed
    }

    /// <summary>
    /// Manages the lifetime and usage of a PowerShell session.
    /// Handles nested PowerShell prompts and also manages execution of 
    /// commands whether inside or outside of the debugger.
    /// </summary>
    public class PowerShellSession : IDisposable, IConsoleHost
    {
        #region Fields

        private PowerShell powerShell;
        private bool ownsInitialRunspace;
        private Runspace initialRunspace;
        private Runspace currentRunspace;
        private InitialSessionState initialSessionState;
        private int pipelineThreadId;

        private bool isStopping;
        private TaskCompletionSource<DebuggerResumeAction> debuggerStoppedTask;
        private TaskCompletionSource<IPipelineExecutionRequest> pipelineExecutionTask;
        private TaskCompletionSource<IPipelineExecutionRequest> pipelineResultTask;

        private object runspaceMutex = new object();
        private RunspaceHandle currentRunspaceHandle;
        private IAsyncWaitQueue<RunspaceHandle> runspaceWaitQueue = new DefaultAsyncWaitQueue<RunspaceHandle>();

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

        public PowerShellSessionState SessionState
        {
            get;
            private set;
        }

        #endregion

        #region Constructors

        public PowerShellSession()
        {
            this.initialSessionState = InitialSessionState.CreateDefault2();

            PSHost psHost = new ConsoleServicePSHost(this);
            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, this.initialSessionState);
            runspace.ApartmentState = ApartmentState.STA;
            runspace.ThreadOptions = PSThreadOptions.ReuseThread;
            runspace.Open();

            this.ownsInitialRunspace = true;

            this.Initialize(runspace);
        }

        public PowerShellSession(Runspace initialRunspace)
        {
            this.Initialize(initialRunspace);
        }

        private void Initialize(Runspace initialRunspace)
        {
            Validate.IsNotNull("initialRunspace", initialRunspace);

            this.SessionState = PowerShellSessionState.NotStarted;

            this.initialRunspace = initialRunspace;
            this.currentRunspace = initialRunspace;
            this.currentRunspace.Debugger.SetDebugMode(DebugModes.LocalScript | DebugModes.RemoteScript);
            this.currentRunspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            this.currentRunspace.Debugger.DebuggerStop += OnDebuggerStop;

            this.powerShell = PowerShell.Create();
            this.powerShell.InvocationStateChanged += powerShell_InvocationStateChanged;
            this.powerShell.Runspace = this.currentRunspace;

            this.SessionState = PowerShellSessionState.Ready;
        }

        #endregion

        #region Public Methods

        public string GetPromptString()
        {
            return "NO PROMPT YET";
        }

        public Task<RunspaceHandle> GetRunspaceHandle()
        {
            lock (this.runspaceMutex)
            {
                if (this.currentRunspaceHandle == null)
                {
                    this.currentRunspaceHandle = new RunspaceHandle(this.currentRunspace, this);
                    TaskCompletionSource<RunspaceHandle> tcs = new TaskCompletionSource<RunspaceHandle>();
                    tcs.SetResult(this.currentRunspaceHandle);
                    return tcs.Task;
                }
                else
                {
                    // TODO: Use CancellationToken?
                    return this.runspaceWaitQueue.Enqueue();
                }
            }
        }

        public async Task<IEnumerable<TResult>> ExecuteCommand<TResult>(
            PSCommand psCommand, 
            bool sendOutputToHost = false)
        {
            // If the debugger is active and the caller isn't on the pipeline 
            // thread, send the command over to that thread to be executed.
            if (Thread.CurrentThread.ManagedThreadId != this.pipelineThreadId &&
                this.pipelineExecutionTask != null)
            {
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

                try
                {
                    if (this.currentRunspace.RunspaceAvailability == RunspaceAvailability.AvailableForNestedCommand ||
                        this.debuggerStoppedTask != null)
                    {
                        using (Pipeline pipeline = this.currentRunspace.CreateNestedPipeline())
                        {
                            foreach (var command in psCommand.Commands)
                            {
                                pipeline.Commands.Add(command);
                            }

                            IEnumerable<TResult> result =
                                pipeline
                                    .Invoke()
                                    .Select(pso => pso.BaseObject)
                                    .Cast<TResult>();

                            return result;
                        }
                    }
                    else
                    {
                        // Set the runspace
                        var runspaceHandle = await this.GetRunspaceHandle();
                        if (runspaceHandle.Runspace.RunspaceAvailability != RunspaceAvailability.AvailableForNestedCommand)
                        {
                            this.powerShell.Runspace = runspaceHandle.Runspace;
                        }

                        // Invoke the pipeline on a background thread
                        // TODO: Use built-in async invocation!
                        var taskResult =
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

                        runspaceHandle.Dispose();

                        bool hadErrors = this.powerShell.HadErrors;
                        return taskResult;
                    }
                }
                catch (RuntimeException e)
                {
                    // TODO: Return an error
                    string boo = e.Message;
                }
            }

            // TODO: Better result
            return null;
        }

        public Task ExecuteCommand(PSCommand psCommand)
        {
            return this.ExecuteCommand<object>(psCommand);
        }

        /// <summary>
        /// Executes a command or script string in the session.
        /// </summary>
        /// <param name="scriptString">The script string to execute.</param>
        /// <returns>A Task that can be awaited for the script completion.</returns>
        public async Task ExecuteScript(string scriptString)
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddScript(scriptString);

            await this.ExecuteCommand<object>(psCommand, true);
        }

        public async Task ExecuteScriptAtPath(string scriptPath)
        {
            PSCommand command = new PSCommand();
            command.AddCommand(scriptPath);

            await this.ExecuteCommand<object>(command);
        }

        public void AbortExecution()
        {
            // TODO: Verify this behavior
            this.powerShell.BeginStop(null, null);
            this.ResumeDebugger(DebuggerResumeAction.Stop);
        }

        public void BreakExecution()
        {
            this.currentRunspace.Debugger.SetDebuggerStepMode(true);
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

        public void Dispose()
        {
            this.powerShell.InvocationStateChanged -= this.powerShell_InvocationStateChanged;
            this.powerShell.Dispose();
            this.SessionState = PowerShellSessionState.Disposed;

            if (this.ownsInitialRunspace)
            {
                // TODO: Detach from events
                this.initialRunspace.Dispose();
            }
        }

        internal void ReleaseRunspaceHandle(RunspaceHandle runspaceHandle)
        {
            Validate.IsNotNull("runspaceHandle", runspaceHandle);

            IDisposable dequeuedTask = null;

            lock (this.runspaceMutex)
            {
                if (runspaceHandle != this.currentRunspaceHandle)
                {
                    throw new InvalidOperationException("Released runspace handle was not the current handle.");
                }

                this.currentRunspaceHandle = null;

                if (!this.runspaceWaitQueue.IsEmpty)
                {
                    this.currentRunspaceHandle = new RunspaceHandle(this.currentRunspace, this);
                    dequeuedTask = 
                        this.runspaceWaitQueue.Dequeue(
                            this.currentRunspaceHandle);
                }
            }

            // If a queued task was dequeued, call Dispose to cause it to be executed.
            if (dequeuedTask != null)
            {
                dequeuedTask.Dispose();
            }
        }

        #endregion

        #region Events

        public event EventHandler<SessionStateChangedEventArgs> SessionStateChanged;

        private void OnSessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            this.SessionState = e.NewSessionState; 

            if (this.SessionStateChanged != null)
            {
                this.SessionStateChanged(sender, e);
            }
        }

        #endregion

        #region Private Methods

        void powerShell_InvocationStateChanged(object sender, PSInvocationStateChangedEventArgs e)
        {
            SessionStateChangedEventArgs eventArgs = TranslateInvocationStateInfo(e.InvocationStateInfo);
            this.OnSessionStateChanged(this, eventArgs);
        }

        private static SessionStateChangedEventArgs TranslateInvocationStateInfo(PSInvocationStateInfo invocationState)
        {
            PowerShellSessionState newState = PowerShellSessionState.Unknown;
            PowerShellExecutionResult executionResult = PowerShellExecutionResult.NotFinished;

            switch (invocationState.State)
            {
                case PSInvocationState.NotStarted:
                    newState = PowerShellSessionState.NotStarted;
                    break;

                case PSInvocationState.Failed:
                    newState = PowerShellSessionState.Ready;
                    executionResult = PowerShellExecutionResult.Failed;
                    break;

                case PSInvocationState.Disconnected:
                    // TODO: Any extra work to do in this case?
                    // TODO: Is this a unique state that can be re-connected?
                    newState = PowerShellSessionState.Disposed;
                    executionResult = PowerShellExecutionResult.Stopped;
                    break;

                case PSInvocationState.Running:
                    newState = PowerShellSessionState.Running;
                    break;

                case PSInvocationState.Completed:
                    newState = PowerShellSessionState.Ready;
                    executionResult = PowerShellExecutionResult.Completed;
                    break;

                case PSInvocationState.Stopping:
                    // TODO: Collapse this so that the result shows that execution was aborted
                    newState = PowerShellSessionState.Aborting;
                    break;

                case PSInvocationState.Stopped:
                    newState = PowerShellSessionState.Ready;
                    executionResult = PowerShellExecutionResult.Aborted;
                    break;

                default:
                    newState = PowerShellSessionState.Unknown;
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

        #endregion

        #region Events

        internal event EventHandler<DebuggerStopEventArgs> DebuggerStop;

        private void OnDebuggerStop(object sender, DebuggerStopEventArgs e)
        {
            if (!this.isStopping)
            {
                // Set the task so a result can be set
                this.debuggerStoppedTask =
                    new TaskCompletionSource<DebuggerResumeAction>();

                // Save the pipeline thread ID and create the pipeline execution task
                this.pipelineThreadId = Thread.CurrentThread.ManagedThreadId;
                this.pipelineExecutionTask = new TaskCompletionSource<IPipelineExecutionRequest>();

                // Update the session state
                this.OnSessionStateChanged(this, new SessionStateChangedEventArgs(PowerShellSessionState.Ready, PowerShellExecutionResult.Stopped, null));

                // Raise the event for the debugger service
                if (this.DebuggerStop != null)
                {
                    this.DebuggerStop(sender, e);
                }

                while (true)
                {
                    int taskIndex =
                        Task.WaitAny(
                            this.debuggerStoppedTask.Task,
                            this.pipelineExecutionTask.Task);

                    if (taskIndex == 0)
                    {
                        e.ResumeAction = this.debuggerStoppedTask.Task.Result;
                        break;
                    }
                    else if (taskIndex == 1)
                    {
                        IPipelineExecutionRequest executionRequest =
                            this.pipelineExecutionTask.Task.Result;

                        this.pipelineExecutionTask = new TaskCompletionSource<IPipelineExecutionRequest>();

                        executionRequest.Execute().Wait();

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
            else
            {
                e.ResumeAction = DebuggerResumeAction.Stop;
            }
        }

        public event EventHandler<BreakpointUpdatedEventArgs> BreakpointUpdated;

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            if (this.BreakpointUpdated != null)
            {
                this.BreakpointUpdated(sender, e);
            }
        }

        public event EventHandler<OutputWrittenEventArgs> OutputWritten;

        #endregion

        #region IConsoleHost Implementation

        void IConsoleHost.WriteOutput(string outputString, bool includeNewLine, OutputType outputType, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            if (this.OutputWritten != null)
            {
                this.OutputWritten(
                    this,
                    new OutputWrittenEventArgs(
                        outputString,
                        includeNewLine,
                        outputType,
                        foregroundColor,
                        backgroundColor));
            }
        }

        Task<int> IConsoleHost.PromptForChoice(string promptCaption, string promptMessage, IEnumerable<ChoiceDetails> choices, int defaultChoice)
        {
            throw new NotImplementedException();
        }

        void IConsoleHost.PromptForChoiceResult(int promptId, int choiceResult)
        {
            //throw new NotImplementedException();
        }

        void IConsoleHost.UpdateProgress(long sourceId, ProgressDetails progressDetails)
        {
            //throw new NotImplementedException();
        }

        void IConsoleHost.ExitSession(int exitCode)
        {
            throw new NotImplementedException();
        }

        #endregion

        private interface IPipelineExecutionRequest
        {
            Task Execute();
        }

        private class PipelineExecutionRequest<TResult> : IPipelineExecutionRequest
        {
            PowerShellSession powerShellSession;
            PSCommand psCommand;
            bool sendOutputToHost;

            public IEnumerable<TResult> Results { get; private set; }

            public PipelineExecutionRequest(
                PowerShellSession powerShellSession,
                PSCommand psCommand,
                bool sendOutputToHost)
            {
                this.powerShellSession = powerShellSession;
                this.psCommand = psCommand;
                this.sendOutputToHost = sendOutputToHost;
            }

            public async Task Execute()
            {
                this.Results =
                    await this.powerShellSession.ExecuteCommand<TResult>(
                        psCommand,
                        sendOutputToHost);

                // TODO: Deal with errors?
            }
        }
    }

    public class SessionStateChangedEventArgs
    {
        public PowerShellSessionState NewSessionState { get; private set; }

        public PowerShellExecutionResult ExecutionResult { get; private set; }

        public Exception ErrorException { get; private set; }

        public SessionStateChangedEventArgs(
            PowerShellSessionState newSessionState,
            PowerShellExecutionResult executionResult,
            Exception errorException)
        {
            this.NewSessionState = newSessionState;
            this.ExecutionResult = executionResult;
            this.ErrorException = errorException;
        }
    }

    public class OutputWrittenEventArgs
    {
        public string OutputText { get; private set; }

        public OutputType OutputType { get; private set; }

        public bool IncludeNewLine { get; private set; }

        public ConsoleColor ForegroundColor { get; private set; }

        public ConsoleColor BackgroundColor { get; private set; }

        public OutputWrittenEventArgs(string outputText, bool includeNewLine, OutputType outputType, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            this.OutputText = outputText;
            this.IncludeNewLine = includeNewLine;
            this.OutputType = outputType;
            this.ForegroundColor = foregroundColor;
            this.BackgroundColor = backgroundColor;
        }
    }

    public class RunspaceHandle : IDisposable
    {
        PowerShellSession powerShellSession;

        public Runspace Runspace { get; private set; }

        public RunspaceHandle(Runspace runspace, PowerShellSession powerShellSession)
        {
            this.Runspace = runspace;
            this.powerShellSession = powerShellSession;
        }

        public void Dispose()
        {
            this.powerShellSession.ReleaseRunspaceHandle(this);
        }
    }
}
