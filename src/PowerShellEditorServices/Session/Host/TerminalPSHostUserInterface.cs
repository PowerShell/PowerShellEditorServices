//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;

namespace Microsoft.PowerShell.EditorServices
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.PowerShell.EditorServices.Utility;

    /// <summary>
    /// Provides an EditorServicesPSHostUserInterface implementation
    /// that integrates with the user's terminal UI.
    /// </summary>
    public class TerminalPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        #region Private Fields

        private ConsoleReadLine consoleReadLine;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHostUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="powerShellContext">The PowerShellContext to use for executing commands.</param>
        /// <param name="logger">An ILogger implementation to use for this host.</param>
        public TerminalPSHostUserInterface(
            PowerShellContext powerShellContext,
            ILogger logger)
            : base(
                powerShellContext,
                new TerminalPSHostRawUserInterface(logger),
                logger)
        {
            this.consoleReadLine = new ConsoleReadLine(powerShellContext);

            // Set the output encoding to UTF-8 so that special
            // characters are written to the console correctly
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;

            System.Console.CancelKeyPress +=
                (obj, args) =>
                {
                    if (!this.IsNativeApplicationRunning)
                    {
                        // We'll handle Ctrl+C
                        args.Cancel = true;
                        this.SendControlC();
                    }
                };
        }

        #endregion

        /// <summary>
        /// Requests that the HostUI implementation read a command line
        /// from the user to be executed in the integrated console command
        /// loop.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken used to cancel the command line request.
        /// </param>
        /// <returns>A Task that can be awaited for the resulting input string.</returns>
        protected override Task<string> ReadCommandLine(CancellationToken cancellationToken)
        {
            return this.consoleReadLine.ReadCommandLine(cancellationToken);
        }

        /// <summary>
        /// Creates an InputPrompt handle to use for displaying input
        /// prompts to the user.
        /// </summary>
        /// <returns>A new InputPromptHandler instance.</returns>
        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            return new TerminalInputPromptHandler(
                this.consoleReadLine,
                this,
                this.Logger);
        }

        /// <summary>
        /// Creates a ChoicePromptHandler to use for displaying a
        /// choice prompt to the user.
        /// </summary>
        /// <returns>A new ChoicePromptHandler instance.</returns>
        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            return new TerminalChoicePromptHandler(
                this.consoleReadLine,
                this,
                this.Logger);
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
            System.Console.BackgroundColor = backgroundColor;

            System.Console.Write(outputString + (includeNewLine ? Environment.NewLine : ""));

            System.Console.ForegroundColor = oldForegroundColor;
            System.Console.BackgroundColor = oldBackgroundColor;
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
