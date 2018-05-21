//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Host
{
    internal class ProtocolPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        #region Private Fields

        private IMessageSender messageSender;
        private OutputDebouncer outputDebouncer;

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
            PsesLogger logger)
            : base(powerShellContext, new SimplePSHostRawUserInterface(logger), logger)
        {
            this.messageSender = messageSender;
            this.outputDebouncer = new OutputDebouncer(messageSender);
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

        protected override Task<string> ReadCommandLine(CancellationToken cancellationToken)
        {
            // This currently does nothing because the "evaluate" request
            // will cancel the current prompt and execute the user's
            // script selection.
            return new TaskCompletionSource<string>().Task;
        }

        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            return new ProtocolInputPromptHandler(this.messageSender, this, this, this.Logger);
        }

        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            return new ProtocolChoicePromptHandler(this.messageSender, this, this, this.Logger);
        }
    }
}
