//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Provides a standard implementation of InputPromptHandler
    /// for use in the interactive console (REPL).
    /// </summary>
    internal class TerminalInputPromptHandler : ConsoleInputPromptHandler
    {
        #region Private Fields

        private ConsoleReadLine consoleReadLine;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ConsoleInputPromptHandler class.
        /// </summary>
        /// <param name="consoleReadLine">
        /// The ConsoleReadLine instance to use for interacting with the terminal.
        /// </param>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for writing to the
        /// console.
        /// </param>
        /// <param name="logger">Logger used for writing log messages.</param>
        public TerminalInputPromptHandler(
            ConsoleReadLine consoleReadLine,
            IHostOutput hostOutput,
            IPsesLogger logger)
                : base(hostOutput, logger)
        {
            this.consoleReadLine = consoleReadLine;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reads an input string from the user.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the prompt.</param>
        /// <returns>A Task that can be awaited to get the user's response.</returns>
        protected override async Task<string> ReadInputString(CancellationToken cancellationToken)
        {
            string inputString = await this.consoleReadLine.ReadSimpleLine(cancellationToken);
            this.hostOutput.WriteOutput(string.Empty);

            return inputString;
        }

        /// <summary>
        /// Reads a SecureString from the user.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the prompt.</param>
        /// <returns>A Task that can be awaited to get the user's response.</returns>
        protected override async Task<SecureString> ReadSecureString(CancellationToken cancellationToken)
        {
            SecureString secureString = await this.consoleReadLine.ReadSecureLine(cancellationToken);
            this.hostOutput.WriteOutput(string.Empty);

            return secureString;
        }

        #endregion
    }
}
