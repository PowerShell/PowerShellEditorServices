//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    /// <summary>
    /// Provides a high-level service for managing an active
    /// interactive console session.
    /// </summary>
    public class ConsoleService : IDisposable
    {
        #region Private Fields

        private IConsoleHost consoleHost;
        private Runspace currentRunspace;
        private InitialSessionState initialSessionState;
        private ConsoleServicePSHost consoleServicePSHost;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ConsoleService class using the
        /// given IConsoleHost implementation to invoke host operations
        /// on behalf of the ConsolePSHost.  An InitialSessionState may
        /// be provided to create the console runspace using a particular
        /// configuraiton.
        /// </summary>
        /// <param name="consoleHost">
        /// An IConsoleHost implementation which handles host operations.
        /// </param>
        /// <param name="initialSessionState">
        /// An optional InitialSessionState to use in creating the console runspace.
        /// </param>
        public ConsoleService(
            IConsoleHost consoleHost, 
            InitialSessionState initialSessionState = null)
        {
            Validate.IsNotNull("consoleHost", consoleHost);

            // If no InitialSessionState is provided, create one from defaults
            this.initialSessionState = initialSessionState;
            if (this.initialSessionState == null)
            {
                this.initialSessionState = InitialSessionState.CreateDefault2();
            }

            this.consoleHost = consoleHost;
            this.consoleServicePSHost = new ConsoleServicePSHost(consoleHost);

            this.currentRunspace = RunspaceFactory.CreateRunspace(consoleServicePSHost, this.initialSessionState);
            this.currentRunspace.ApartmentState = ApartmentState.STA;
            this.currentRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
            this.currentRunspace.Open();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes a command in the console runspace.
        /// </summary>
        /// <param name="commandString">The command string to execute.</param>
        /// <returns>A Task that can be awaited for the command completion.</returns>
        public async Task ExecuteCommand(string commandString)
        {
            PowerShell powerShell = PowerShell.Create();

            try
            {
                // Set the runspace
                powerShell.Runspace = this.currentRunspace;

                // Add the command to the pipeline
                powerShell.AddScript(commandString);

                // Instruct PowerShell to send output and errors to the host
                powerShell.Commands.Commands[0].MergeMyResults(
                    PipelineResultTypes.Error, 
                    PipelineResultTypes.Output);
                powerShell.AddCommand("out-default");

                // Invoke the pipeline on a background thread
                await Task.Factory.StartNew(
                    () => 
                        {

                            var output = powerShell.Invoke();
                            var count = output.Count;
                        },
                        CancellationToken.None, // Might need a cancellation token
                        TaskCreationOptions.None, 
                        TaskScheduler.Default
                );
            }
            catch (RuntimeException e)
            {
                // TODO: Return an error
                string boo = e.Message;
            }
            finally
            {
                if (powerShell != null)
                {
                    powerShell.Dispose();
                }
            }
        }

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

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the runspace in use by the ConsoleService.
        /// </summary>
        public void Dispose()
        {
            if (this.currentRunspace != null)
            {
                this.currentRunspace.Dispose();
                this.currentRunspace = null;
            }
        }

        #endregion
    }
}
