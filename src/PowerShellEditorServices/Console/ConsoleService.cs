//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    using Microsoft.PowerShell.EditorServices.Session;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    /// <summary>
    /// Provides a high-level service for managing an active
    /// interactive console session.
    /// </summary>
    public class ConsoleService 
    {
        #region Private Fields

        private IConsoleHost consoleHost;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ConsoleService class using the
        /// given IConsoleHost implementation to invoke host operations
        /// on behalf of the ConsolePSHost.  An InitialSessionState may
        /// be provided to create the console runspace using a particular
        /// configuraiton.
        /// </summary>
        /// <param name="initialSessionState">
        /// An optional InitialSessionState to use in creating the console runspace.
        /// </param>
        public ConsoleService(PowerShellSession powerShellSession)
        {
            Validate.IsNotNull("powerShellSession", powerShellSession);

            //this.powerShellSession.SessionStateChanged -- NEEDED?
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sends a user's prompt choice response back to the specified prompt ID.
        /// </summary>
        /// <param name="promptId">
        /// The ID of the prompt to which the user is responding.
        /// </param>
        /// <param name="choiceResult">
        /// The index of the choice that the user selected.
        /// </param>
        public void ReceiveChoicePromptResult(
            int promptId, 
            int choiceResult)
        {
            // TODO: Any validation or error handling?
            this.consoleHost.PromptForChoiceResult(promptId, choiceResult);
        }

        /// <summary>
        /// Sends a CTRL+C signal to the console to halt execution of
        /// the current command.
        /// </summary>
        public void SendControlC()
        {
            // TODO: Cancel the current pipeline execution
        }


        #endregion
    }
}
