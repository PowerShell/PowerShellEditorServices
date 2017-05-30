//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Console
{
    using Microsoft.PowerShell.EditorServices.Session;
    using System;
    using System.Globalization;
    using System.Management.Automation;
    using System.Security;

    /// <summary>
    /// Provides a high-level service for exposing an interactive
    /// PowerShell console (REPL) to the user.
    /// </summary>
    public class ConsoleService : IConsoleHost
    {
        #region Fields

        private bool isReadLoopStarted;
        private ConsoleReadLine consoleReadLine;
        private PowerShellContext powerShellContext;

        CancellationTokenSource readLineCancellationToken;

        private PromptHandler activePromptHandler;
        private Stack<IPromptHandlerContext> promptHandlerContextStack =
            new Stack<IPromptHandlerContext>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a boolean determining whether the console (terminal)
        /// REPL should be used in this session.
        /// </summary>
        public bool EnableConsoleRepl { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleService class.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext that will be used for executing commands
        /// against a runspace.
        /// </param>
        public ConsoleService(PowerShellContext powerShellContext)
            : this(powerShellContext, null)
        {
        }

        /// <summary>
        /// Creates a new instance of the ConsoleService class.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext that will be used for executing commands
        /// against a runspace.
        /// </param>
        /// <param name="defaultPromptHandlerContext">
        /// The default IPromptHandlerContext implementation to use for
        /// displaying prompts to the user.
        /// </param>
        public ConsoleService(
            PowerShellContext powerShellContext,
            IPromptHandlerContext defaultPromptHandlerContext)
        {
            // Register this instance as the IConsoleHost for the PowerShellContext
            this.powerShellContext = powerShellContext;
            this.powerShellContext.DebuggerStop += PowerShellContext_DebuggerStop;
            this.powerShellContext.DebuggerResumed += PowerShellContext_DebuggerResumed;
            this.powerShellContext.ExecutionStatusChanged += PowerShellContext_ExecutionStatusChanged;

            // Set the default prompt handler factory or create
            // a default if one is not provided
            if (defaultPromptHandlerContext == null)
            {
                defaultPromptHandlerContext =
                    new ConsolePromptHandlerContext(this);
            }

            this.promptHandlerContextStack.Push(
                defaultPromptHandlerContext);

            this.consoleReadLine = new ConsoleReadLine(powerShellContext);

        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts a terminal-based interactive console loop in the current process.
        /// </summary>
        public void StartReadLoop()
        {
            if (this.EnableConsoleRepl)
            {
                this.isReadLoopStarted = true;
                this.InnerStartReadLoop();
            }
        }

        /// <summary>
        /// Cancels an active read loop.
        /// </summary>
        public void CancelReadLoop()
        {
            this.isReadLoopStarted = false;
            this.InnerCancelReadLoop();
        }

        /// <summary>
        /// Executes a script file at the specified path.
        /// </summary>
        /// <param name="scriptPath">The path to the script file to execute.</param>
        /// <param name="arguments">Arguments to pass to the script.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task ExecuteScriptAtPath(string scriptPath, string arguments = null)
        {
            this.InnerCancelReadLoop();

            // If we don't escape wildcard characters in the script path, the script can
            // fail to execute if say the script name was foo][.ps1.
            // Related to issue #123.
            string escapedScriptPath = PowerShellContext.EscapePath(scriptPath, escapeSpaces: true);

            await this.powerShellContext.ExecuteScriptString(
                $"{escapedScriptPath} {arguments}",
                true,
                true,
                false);

            this.InnerStartReadLoop();
        }

        /// <summary>
        /// Pushes a new IPromptHandlerContext onto the stack.  This
        /// is used when a prompt handler context is only needed for
        /// a short series of command executions.
        /// </summary>
        /// <param name="promptHandlerContext">
        /// The IPromptHandlerContext instance to push onto the stack.
        /// </param>
        public void PushPromptHandlerContext(IPromptHandlerContext promptHandlerContext)
        {
            // Push a new prompt handler factory for future prompts
            this.promptHandlerContextStack.Push(promptHandlerContext);
        }

        /// <summary>
        /// Pops the most recent IPromptHandlerContext from the stack.
        /// This is called when execution requiring a specific type of
        /// prompt has completed and the previous prompt handler context
        /// should be restored.
        /// </summary>
        public void PopPromptHandlerContext()
        {
            // The last item on the stack is the default handler, never pop it
            if (this.promptHandlerContextStack.Count > 1)
            {
                this.promptHandlerContextStack.Pop();
            }
        }

        /// <summary>
        /// Cancels the currently executing command or prompt.
        /// </summary>
        public void SendControlC()
        {
            if (this.activePromptHandler != null)
            {
                this.activePromptHandler.CancelPrompt();
            }
            else
            {
                // Cancel the current execution
                this.powerShellContext.AbortExecution();
            }
        }

        /// <summary>
        /// Reads an input string from the user.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the prompt.</param>
        /// <returns>A Task that can be awaited to get the user's response.</returns>
        public async Task<string> ReadSimpleLine(CancellationToken cancellationToken)
        {
            string inputLine = await this.consoleReadLine.ReadSimpleLine(cancellationToken);
            this.WriteOutput(string.Empty, true);
            return inputLine;
        }

        /// <summary>
        /// Reads a SecureString from the user.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the prompt.</param>
        /// <returns>A Task that can be awaited to get the user's response.</returns>
        public async Task<SecureString> ReadSecureLine(CancellationToken cancellationToken)
        {
            SecureString secureString = await this.consoleReadLine.ReadSecureLine(cancellationToken);
            this.WriteOutput(string.Empty, true);
            return secureString;
        }

        #endregion

        #region Private Methods

        private void WritePromptStringToHost()
        {
            string promptString = this.powerShellContext.PromptString;

            // Update the stored prompt string if the session is remote
            if (this.powerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
            {
                promptString =
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "[{0}]: {1}",
                        this.powerShellContext.CurrentRunspace.Runspace.ConnectionInfo != null
                            ? this.powerShellContext.CurrentRunspace.Runspace.ConnectionInfo.ComputerName
                            : this.powerShellContext.CurrentRunspace.SessionDetails.ComputerName,
                        promptString);
            }

            // Write the prompt string
            this.WriteOutput(promptString, false);
        }

        private void WriteDebuggerBanner(DebuggerStopEventArgs eventArgs)
        {
            // TODO: What do we display when we don't know why we stopped?

            if (eventArgs.Breakpoints.Count > 0)
            {
                // The breakpoint classes have nice ToString output so use that
                this.WriteOutput(
                    Environment.NewLine + $"Hit {eventArgs.Breakpoints[0].ToString()}\n",
                    true,
                    OutputType.Normal,
                    ConsoleColor.Blue);
            }
        }

        private void InnerStartReadLoop()
        {
            if (this.EnableConsoleRepl)
            {
                if (this.readLineCancellationToken == null)
                {
                    this.readLineCancellationToken = new CancellationTokenSource();

                    var terminalThreadTask =
                        Task.Factory.StartNew(
                            async () =>
                            {
                                await this.StartReplLoop(this.readLineCancellationToken.Token);
                            });
                }
                else
                {
                    Logger.Write(LogLevel.Verbose, "InnerStartReadLoop called while read loop is already running");
                }
            }
        }

        private void InnerCancelReadLoop()
        {
            if (this.readLineCancellationToken != null)
            {
                // Set this to false so that Ctrl+C isn't trapped by any
                // lingering ReadKey
                Console.TreatControlCAsInput = false;

                this.readLineCancellationToken.Cancel();
                this.readLineCancellationToken = null;
            }
        }

        private async Task StartReplLoop(CancellationToken cancellationToken)
        {
            do
            {
                string commandString = null;

                this.WritePromptStringToHost();

                try
                {
                    commandString =
                        await this.consoleReadLine.ReadCommandLine(
                            cancellationToken);
                }
                catch (PipelineStoppedException)
                {
                    this.WriteOutput(
                        "^C",
                        true,
                        OutputType.Normal,
                        foregroundColor: ConsoleColor.Red);
                }
                catch (TaskCanceledException)
                {
                    // Do nothing here, the while loop condition will exit.
                }
                catch (Exception e) // Narrow this if possible
                {
                    this.WriteOutput(
                        $"\n\nAn error occurred while reading input:\n\n{e.ToString()}\n",
                        true,
                        OutputType.Error);

                    Logger.WriteException("Caught exception while reading command line", e);
                }

                if (commandString != null)
                {
                    Console.Write(Environment.NewLine);

                    if (!string.IsNullOrWhiteSpace(commandString))
                    {
                        var unusedTask =
                            this.powerShellContext
                                .ExecuteScriptString(
                                    commandString,
                                    false,
                                    true,
                                    true)
                                .ConfigureAwait(false);

                        break;
                    }
                }
            }
            while (!cancellationToken.IsCancellationRequested);
        }

        #endregion

        #region Events

        /// <summary>
        /// An event that is raised when textual output of any type is
        /// written to the session.
        /// </summary>
        public event EventHandler<OutputWrittenEventArgs> OutputWritten;

        #endregion

        #region IConsoleHost Implementation

        void IConsoleHost.WriteOutput(string outputString, bool includeNewLine, OutputType outputType, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            if (this.EnableConsoleRepl)
            {
                ConsoleColor oldForegroundColor = Console.ForegroundColor;
                ConsoleColor oldBackgroundColor = Console.BackgroundColor;

                Console.ForegroundColor = foregroundColor;
                Console.BackgroundColor = backgroundColor;

                Console.Write(outputString + (includeNewLine ? Environment.NewLine : ""));

                Console.ForegroundColor = oldForegroundColor;
                Console.BackgroundColor = oldBackgroundColor;
            }
            else
            {
                this.OutputWritten?.Invoke(
                    this,
                    new OutputWrittenEventArgs(
                        outputString,
                        includeNewLine,
                        outputType,
                        foregroundColor,
                        backgroundColor));
            }
        }

        void IConsoleHost.UpdateProgress(long sourceId, ProgressDetails progressDetails)
        {
            //throw new NotImplementedException();
        }

        void IConsoleHost.ExitSession(int exitCode)
        {
            //throw new NotImplementedException();
        }

        ChoicePromptHandler IConsoleHost.GetChoicePromptHandler()
        {
            return this.GetPromptHandler(
                factory => factory.GetChoicePromptHandler());
        }

        InputPromptHandler IConsoleHost.GetInputPromptHandler()
        {
            return this.GetPromptHandler(
                factory => factory.GetInputPromptHandler());
        }

        private TPromptHandler GetPromptHandler<TPromptHandler>(
            Func<IPromptHandlerContext, TPromptHandler> factoryInvoker)
                where TPromptHandler : PromptHandler
        {
            if (this.activePromptHandler != null)
            {
                Logger.Write(
                    LogLevel.Error,
                    "Prompt handler requested while another prompt is already active.");
            }

            // Get the topmost prompt handler factory
            IPromptHandlerContext promptHandlerContext =
                this.promptHandlerContextStack.Peek();

            TPromptHandler promptHandler = factoryInvoker(promptHandlerContext);
            this.activePromptHandler = promptHandler;
            this.activePromptHandler.PromptCancelled += activePromptHandler_PromptCancelled;

            return promptHandler;
        }

        #endregion

        #region Event Handlers

        private void activePromptHandler_PromptCancelled(object sender, EventArgs e)
        {
            // Clean up the existing prompt
            this.activePromptHandler.PromptCancelled -= activePromptHandler_PromptCancelled;
            this.activePromptHandler = null;
        }

        private void PowerShellContext_DebuggerStop(object sender, System.Management.Automation.DebuggerStopEventArgs e)
        {
            // Cancel any existing prompt first
            this.InnerCancelReadLoop();

            this.WriteDebuggerBanner(e);
            this.InnerStartReadLoop();
        }

        private void PowerShellContext_DebuggerResumed(object sender, System.Management.Automation.DebuggerResumeAction e)
        {
            this.InnerCancelReadLoop();
        }

        private void PowerShellContext_ExecutionStatusChanged(object sender, ExecutionStatusChangedEventArgs eventArgs)
        {
            if (this.EnableConsoleRepl && this.isReadLoopStarted)
            {
                if (eventArgs.ExecutionStatus == ExecutionStatus.Aborted)
                {
                    // When aborted, cancel any lingering prompts
                    if (this.activePromptHandler != null)
                    {
                        this.activePromptHandler.CancelPrompt();
                        this.WriteOutput(string.Empty);
                    }
                }
                else if (
                    eventArgs.ExecutionOptions.WriteOutputToHost ||
                    eventArgs.ExecutionOptions.InterruptCommandPrompt)
                {
                    // Any command which writes output to the host will affect
                    // the display of the prompt
                    if (eventArgs.ExecutionStatus != ExecutionStatus.Running)
                    {
                        // Execution has completed, start the input prompt
                        this.InnerStartReadLoop();
                    }
                    else
                    {
                        // A new command was started, cancel the input prompt
                        this.InnerCancelReadLoop();
                        this.WriteOutput(string.Empty);
                    }
                }
                else if (
                    eventArgs.ExecutionOptions.WriteErrorsToHost &&
                    (eventArgs.ExecutionStatus == ExecutionStatus.Failed ||
                     eventArgs.HadErrors))
                {
                    this.InnerCancelReadLoop();
                    this.WriteOutput(string.Empty);
                    this.InnerStartReadLoop();
                }
            }
        }

        #endregion
    }
}

