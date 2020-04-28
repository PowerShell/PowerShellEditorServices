//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation.Host;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    using System.Management.Automation;

    /// <summary>
    /// Provides an EditorServicesPSHostUserInterface implementation
    /// that integrates with the user's terminal UI.
    /// </summary>
    internal class TerminalPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        #region Private Fields

        private readonly PSHostUserInterface internalHostUI;
        private readonly PSObject _internalHostPrivateData;
        private readonly ConsoleReadLine _consoleReadLine;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHostUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="powerShellContext">The PowerShellContext to use for executing commands.</param>
        /// <param name="logger">An ILogger implementation to use for this host.</param>
        /// <param name="internalHost">The InternalHost instance from the origin runspace.</param>
        public TerminalPSHostUserInterface(
            PowerShellContextService powerShellContext,
            PSHost internalHost,
            ILogger logger)
            : base (
                powerShellContext,
                new TerminalPSHostRawUserInterface(logger, internalHost),
                logger)
        {
            internalHostUI = internalHost.UI;
            _internalHostPrivateData = internalHost.PrivateData;
            _consoleReadLine = new ConsoleReadLine(powerShellContext);

            // Set the output encoding to UTF-8 so that special
            // characters are written to the console correctly
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;

            System.Console.CancelKeyPress +=
                (obj, args) =>
                {
                    if (!IsNativeApplicationRunning)
                    {
                        // We'll handle Ctrl+C
                        args.Cancel = true;
                        SendControlC();
                    }
                };
        }

        #endregion

        /// <summary>
        /// Returns true if the host supports VT100 output codes.
        /// </summary>
        public override bool SupportsVirtualTerminal => internalHostUI.SupportsVirtualTerminal;

        /// <summary>
        /// Gets a value indicating whether writing progress is supported.
        /// </summary>
        internal protected override bool SupportsWriteProgress => true;

        /// <summary>
        /// Gets and sets the value of progress foreground from the internal host since Progress is handled there.
        /// </summary>
        internal override ConsoleColor ProgressForegroundColor
        {
            get => (ConsoleColor)_internalHostPrivateData.Properties["ProgressForegroundColor"].Value;
            set => _internalHostPrivateData.Properties["ProgressForegroundColor"].Value = value;
        }

        /// <summary>
        /// Gets and sets the value of progress background from the internal host since Progress is handled there.
        /// </summary>
        internal override ConsoleColor ProgressBackgroundColor
        {
            get => (ConsoleColor)_internalHostPrivateData.Properties["ProgressBackgroundColor"].Value;
            set => _internalHostPrivateData.Properties["ProgressBackgroundColor"].Value = value;
        }

        /// <summary>
        /// Requests that the HostUI implementation read a command line
        /// from the user to be executed in the integrated console command
        /// loop.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken used to cancel the command line request.
        /// </param>
        /// <returns>A Task that can be awaited for the resulting input string.</returns>
        protected override Task<string> ReadCommandLineAsync(CancellationToken cancellationToken)
        {
            return _consoleReadLine.ReadCommandLineAsync(cancellationToken);
        }

        /// <summary>
        /// Creates an InputPrompt handle to use for displaying input
        /// prompts to the user.
        /// </summary>
        /// <returns>A new InputPromptHandler instance.</returns>
        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            return new TerminalInputPromptHandler(
                _consoleReadLine,
                this,
                Logger);
        }

        /// <summary>
        /// Creates a ChoicePromptHandler to use for displaying a
        /// choice prompt to the user.
        /// </summary>
        /// <returns>A new ChoicePromptHandler instance.</returns>
        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            return new TerminalChoicePromptHandler(
                _consoleReadLine,
                this,
                Logger);
        }

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
            ConsoleColor oldForegroundColor = System.Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = System.Console.BackgroundColor;

            System.Console.ForegroundColor = foregroundColor;
            System.Console.BackgroundColor = ((int)backgroundColor != -1) ? backgroundColor : oldBackgroundColor;

            System.Console.Write(outputString + (includeNewLine ? Environment.NewLine : ""));

            System.Console.ForegroundColor = oldForegroundColor;
            System.Console.BackgroundColor = oldBackgroundColor;
        }

        /// <summary>
        /// Invoked by <see cref="Cmdlet.WriteProgress(ProgressRecord)" /> to display a progress record.
        /// </summary>
        /// <param name="sourceId">
        /// Unique identifier of the source of the record. An int64 is used because typically,
        /// the 'this' pointer of the command from whence the record is originating is used, and
        /// that may be from a remote Runspace on a 64-bit machine.
        /// </param>
        /// <param name="record">
        /// The record being reported to the host.
        /// </param>
        protected override void WriteProgressImpl(long sourceId, ProgressRecord record)
        {
            internalHostUI.WriteProgress(sourceId, record);
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
    }
}
