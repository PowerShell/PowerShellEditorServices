//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal class ProtocolPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        #region Private Fields

        private readonly ILanguageServer _languageServer;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHostUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="powerShellContext"></param>
        public ProtocolPSHostUserInterface(
            ILanguageServer languageServer,
            PowerShellContextService powerShellContext,
            ILogger logger)
            : base (
                powerShellContext,
                new SimplePSHostRawUserInterface(logger),
                logger)
        {
            _languageServer = languageServer;
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
            // TODO: Invoke the "output" notification!
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
            // TODO: Send a new message.
        }

        protected override Task<string> ReadCommandLineAsync(CancellationToken cancellationToken)
        {
            // This currently does nothing because the "evaluate" request
            // will cancel the current prompt and execute the user's
            // script selection.
            return new TaskCompletionSource<string>().Task;
        }

        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            return new ProtocolInputPromptHandler(_languageServer, this, this, this.Logger);
        }

        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            return new ProtocolChoicePromptHandler(_languageServer, this, this, this.Logger);
        }
    }
}
