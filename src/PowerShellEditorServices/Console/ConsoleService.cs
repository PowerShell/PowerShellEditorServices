//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Provides a high-level service for exposing an interactive
    /// PowerShell console (REPL) to the user.
    /// </summary>
    public class ConsoleService : IConsoleHost
    {
        #region Fields

        private PowerShellContext powerShellContext;

        private IPromptHandler activePromptHandler;
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

            // Set the default prompt handler factory or create
            // a default if one is not provided
            if (defaultPromptHandlerContext == null)
            {
                defaultPromptHandlerContext =
                    new ConsolePromptHandlerContext(this);
            }

            this.promptHandlerContextStack.Push(
                defaultPromptHandlerContext);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called when an input string is received from the user.
        /// If a prompt is currently active, the prompt handler is
        /// asked to handle the string.  Otherwise the string is
        /// executed in the PowerShellContext.
        /// </summary>
        /// <param name="inputString">The input string to evaluate.</param>
        /// <param name="echoToConsole">If true, the input will be echoed to the console.</param>
        public void ReceiveInputString(string inputString, bool echoToConsole)
        {
            if (this.activePromptHandler != null)
            {
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
            throw new NotImplementedException();
        }

        ChoicePromptHandler IConsoleHost.GetChoicePromptHandler()
        {
            return this.GetPromptHandler(
                factory => factory.GetChoicePromptHandler());
        }

        private TPromptHandler GetPromptHandler<TPromptHandler>(
            Func<IPromptHandlerContext, TPromptHandler> factoryInvoker)
                where TPromptHandler : IPromptHandler
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

            return promptHandler;
        }

        #endregion
    }
}

