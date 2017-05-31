//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Host
{
    internal class ProtocolPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        #region Private Fields

        private IMessageSender messageSender;
        private OutputDebouncer outputDebouncer;
        private TaskCompletionSource<string> commandLineInputTask;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHostUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="powerShellContext"></param>
        public ProtocolPSHostUserInterface(
            PowerShellContext powerShellContext,
            IMessageSender messageSender,
            IMessageHandlers messageHandlers,
            ILogger logger)
            : base(powerShellContext, new SimplePSHostRawUserInterface(logger), logger)
        {
            this.messageSender = messageSender;
            this.outputDebouncer = new OutputDebouncer(messageSender);

            messageHandlers.SetRequestHandler(
                EvaluateRequest.Type,
                this.HandleEvaluateRequest);
        }

        public void Dispose()
        {
            // TODO: Need a clear API path for this

            // Make sure remaining output is flushed before exiting
            if (this.outputDebouncer != null)
            {
                this.outputDebouncer.Flush().Wait();
                this.outputDebouncer = null;
            }
        }

        #endregion

        /// <summary>
        /// Writes output of the given type to the user interface with
        /// the given foreground and background colors.  Also includes
        /// a newline if requested.
        /// </summary>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="includeNewLine">
        /// If true, a newline should be appended to the output's contents.
        /// </param>
        /// <param name="outputType">
        /// Specifies the type of output to be written.
        /// </param>
        /// <param name="foregroundColor">
        /// Specifies the foreground color of the output to be written.
        /// </param>
        /// <param name="backgroundColor">
        /// Specifies the background color of the output to be written.
        /// </param>
        public override void WriteOutput(
            string outputString,
            bool includeNewLine,
            OutputType outputType,
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor)
        {
            // TODO: This should use a synchronous method!
            this.outputDebouncer.Invoke(
                new OutputWrittenEventArgs(
                    outputString,
                    includeNewLine,
                    outputType,
                    foregroundColor,
                    backgroundColor)).Wait();
        }

        /// <summary>
        /// Sends a progress update event to the user.
        /// </summary>
        /// <param name="sourceId">The source ID of the progress event.</param>
        /// <param name="progressDetails">The details of the activity's current progress.</param>
        protected override void UpdateProgress(
            long sourceId,
            ProgressDetails progressDetails)
        {
        }

        protected override async Task<string> ReadCommandLine(CancellationToken cancellationToken)
        {
            this.commandLineInputTask = new TaskCompletionSource<string>();
            return await this.commandLineInputTask.Task;
        }

        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            return new ProtocolInputPromptHandler(this.messageSender, this, this, this.Logger);
        }

        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            return new ProtocolChoicePromptHandler(this.messageSender, this, this, this.Logger);
        }

        protected async Task HandleEvaluateRequest(
            EvaluateRequestArguments evaluateParams,
            RequestContext<EvaluateResponseBody> requestContext)
        {
            // TODO: This needs to respect debug mode!

            var evaluateResponse =
                new EvaluateResponseBody
                {
                    Result = "",
                    VariablesReference = 0
                };

            if (this.commandLineInputTask != null)
            {
                this.commandLineInputTask.SetResult(evaluateParams.Expression);
                await requestContext.SendResult(evaluateResponse);
            }
            else
            {
                // Check for special commands
                if (string.Equals("!ctrlc", evaluateParams.Expression, StringComparison.CurrentCultureIgnoreCase))
                {
                    this.powerShellContext.AbortExecution();
                    await requestContext.SendResult(evaluateResponse);
                }
                else if (string.Equals("!break", evaluateParams.Expression, StringComparison.CurrentCultureIgnoreCase))
                {
                    // TODO: Need debugger commands interface
                    //editorSession.DebugService.Break();
                    await requestContext.SendResult(evaluateResponse);
                }
                else
                {
                    // We don't await the result of the execution here because we want
                    // to be able to receive further messages while the current script
                    // is executing.  This important in cases where the pipeline thread
                    // gets blocked by something in the script like a prompt to the user.
                    var executeTask =
                        this.powerShellContext.ExecuteScriptString(
                            evaluateParams.Expression,
                            writeInputToHost: true,
                            writeOutputToHost: true,
                            addToHistory: true);

                    // Return the execution result after the task completes so that the
                    // caller knows when command execution completed.
                    Task unusedTask =
                        executeTask.ContinueWith(
                            (task) =>
                            {
                                // Return an empty result since the result value is irrelevant
                                // for this request in the LanguageServer
                                return
                                    requestContext.SendResult(
                                        evaluateResponse);
                            });
                }
            }
        }
    }
}
