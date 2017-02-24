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
    using System;
    using System.Management.Automation;

    /// <summary>
    /// Provides a high-level service for exposing an interactive
    /// PowerShell console (REPL) to the user.
    /// </summary>
    public class ConsoleService : IConsoleHost
    {
        #region Fields

        private ConsoleReadLine consoleReadLine;
        private PowerShellContext powerShellContext;

        CancellationTokenSource readLineCancellationToken;

        private PromptHandler activePromptHandler;
        private Stack<IPromptHandlerContext> promptHandlerContextStack =
            new Stack<IPromptHandlerContext>();

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
            this.powerShellContext.ConsoleHost = this;
            this.powerShellContext.DebuggerStop += PowerShellContext_DebuggerStop;
            this.powerShellContext.DebuggerResumed += PowerShellContext_DebuggerResumed;

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

            // TODO: Move this to the PSHost implementation

            Console.CancelKeyPress +=
                (obj, args) =>
                {
                    // TODO: Don't abort if we're in a native app, let the native app
                    // handle the ctrl+c

                    // We'll handle Ctrl+C
                    args.Cancel = true;
                    this.powerShellContext.AbortExecution();
                };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts a terminal-based interactive console loop in the current process.
        /// </summary>
        public void StartReadLoop()
        {
            if (this.readLineCancellationToken == null)
            {
                this.readLineCancellationToken = new CancellationTokenSource();

                var terminalThreadTask =
                    Task.Factory.StartNew(
                        async () =>
                        {
                            // Set the thread's name to help with debugging
                            Thread.CurrentThread.Name = "Terminal Input Loop Thread";

                            await this.StartReplLoop(this.readLineCancellationToken.Token);
                        },
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);
            }
            else
            {
                Logger.Write(LogLevel.Verbose, "StartReadLoop called while read loop is already running");
            }
        }

        /// <summary>
        /// Cancels an active read loop.
        /// </summary>
        public void CancelReadLoop()
        {
            if (this.readLineCancellationToken != null)
            {
                this.readLineCancellationToken.Cancel();
                this.readLineCancellationToken = null;
            }
        }

        /// <summary>
        /// Stops the current terminal-based interactive console.
        /// </summary>
        public void StopInteractiveConsole()
        {
            if (this.readLineCancellationToken != null)
            {
                this.readLineCancellationToken.Cancel();
                this.readLineCancellationToken = null;
            }
        }

        /// <summary>
        /// Called when a command string is received from the user.
        /// If a prompt is currently active, the prompt handler is
        /// asked to handle the string.  Otherwise the string is
        /// executed in the PowerShellContext.
        /// </summary>
        /// <param name="inputString">The input string to evaluate.</param>
        /// <param name="echoToConsole">If true, the input will be echoed to the console.</param>
        public void ExecuteCommand(string inputString, bool echoToConsole)
        {
            this.CancelReadLoop();

            if (this.activePromptHandler != null)
            {
                if (echoToConsole)
                {
                    this.WriteOutput(inputString, true);
                }

                if (this.activePromptHandler.HandleResponse(inputString))
                {
                    // If the prompt handler is finished, clear it for
                    // future input events
                    this.activePromptHandler = null;
                }
            }
            else
            {
                // Execute the script string but don't wait for completion
                var executeTask =
                    this.powerShellContext
                        .ExecuteScriptString(
                            inputString,
                            echoToConsole,
                            true)
                        .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Executes a script file at the specified path.
        /// </summary>
        /// <param name="scriptPath">The path to the script file to execute.</param>
        /// <param name="arguments">Arguments to pass to the script.</param>
        /// <returns>A Task that can be awaited for completion.</returns>
        public async Task ExecuteScriptAtPath(string scriptPath, string arguments = null)
        {
            this.CancelReadLoop();

            // If we don't escape wildcard characters in the script path, the script can
            // fail to execute if say the script name was foo][.ps1.
            // Related to issue #123.
            string escapedScriptPath = PowerShellContext.EscapePath(scriptPath, escapeSpaces: true);

            await this.powerShellContext.ExecuteScriptString(
                $"{escapedScriptPath} {arguments}",
                true,
                true,
                false);

            this.StartReadLoop();
        }

        /// <summary>
        /// Provides a direct path for a caller that just wants to provide
        /// user response to a prompt without executing a command if there
        /// is no active prompt.
        /// </summary>
        /// <param name="promptResponse">The user's response to the active prompt.</param>
        /// <param name="echoToConsole">If true, the input will be echoed to the console.</param>
        /// <returns>True if there was a prompt, false otherwise.</returns>
        public bool ReceivePromptResponse(string promptResponse, bool echoToConsole)
        {
            if (this.activePromptHandler != null)
            {
                this.ExecuteCommand(promptResponse, echoToConsole);
                return true;
            }

            return false;
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
        /// Cancels the currently executing command.
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

        #endregion

        #region Private Methods

        private void WritePromptStringToHost()
        {
            // Write the prompt string
            this.WriteOutput(
                this.powerShellContext.PromptString,
                false);
        }

        private void WriteDebuggerBanner(DebuggerStopEventArgs eventArgs)
        {
            // TODO: What do we display when we don't know why we stopped?

            if (eventArgs.Breakpoints.Count > 0)
            {
                // The breakpoint classes have nice ToString output so use that
                this.WriteOutput(
                    $"Hit {eventArgs.Breakpoints[0].ToString()}\n",
                    true,
                    OutputType.Normal,
                    ConsoleColor.Blue);
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
                catch (Exception e) // Narrow this if possible
                {
                    this.WriteOutput(
                        $"\n\nAn error occurred while accepting input:\n\n{e.ToString()}\n",
                        true,
                        OutputType.Error);

                    Logger.WriteException("Caught exception while reading command line", e);
                }

                if (commandString != null)
                {
                    Console.Write(Environment.NewLine);

                    await this.powerShellContext.ExecuteScriptString(
                        commandString,
                        false,
                        true,
                        true);
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
            ConsoleColor oldForegroundColor = Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = Console.BackgroundColor;

            Console.ForegroundColor = foregroundColor;
            Console.BackgroundColor = backgroundColor;

            Console.Write(outputString + (includeNewLine ? Environment.NewLine : ""));

            Console.ForegroundColor = oldForegroundColor;
            Console.BackgroundColor = oldBackgroundColor;

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
            this.CancelReadLoop();

            this.WriteDebuggerBanner(e);
            this.StartReadLoop();
        }

        private void PowerShellContext_DebuggerResumed(object sender, System.Management.Automation.DebuggerResumeAction e)
        {
            this.CancelReadLoop();
        }

        #endregion
    }
}

