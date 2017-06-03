//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Provides a standard IPromptHandlerContext implementation for
    /// use in the interactive console (REPL).
    /// </summary>
    public class ConsolePromptHandlerContext : IPromptHandlerContext
    {
        #region Private Fields

        private ILogger logger;
        private IConsoleHost consoleHost;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsolePromptHandlerContext
        /// class.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost implementation to use for writing to the
        /// console.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public ConsolePromptHandlerContext(
            IConsoleHost consoleHost,
            ILogger logger)
        {
            this.consoleHost = consoleHost;
            this.logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new ChoicePromptHandler instance so that
        /// the caller can display a choice prompt to the user.
        /// </summary>
        /// <returns>
        /// A new ChoicePromptHandler instance.
        /// </returns>
        public ChoicePromptHandler GetChoicePromptHandler()
        {
            return new ConsoleChoicePromptHandler(this.consoleHost, this.logger);
        }

        /// <summary>
        /// Creates a new InputPromptHandler instance so that
        /// the caller can display an input prompt to the user.
        /// </summary>
        /// <returns>
        /// A new InputPromptHandler instance.
        /// </returns>
        public InputPromptHandler GetInputPromptHandler()
        {
            return new ConsoleInputPromptHandler(this.consoleHost, this.logger);
        }

        #endregion
    }
}

