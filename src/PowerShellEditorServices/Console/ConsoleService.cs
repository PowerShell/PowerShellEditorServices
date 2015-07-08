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

        public void ReceiveChoicePromptResult(
            int promptId, 
            int choiceResult)
        {
            // TODO: Any validation or error handling?
            this.consoleHost.PromptForChoiceResult(promptId, choiceResult);
        }

        public void SendControlC()
        {
            // TODO: Cancel the current pipeline execution
        }

        #endregion

        #region IDisposable Implementation

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
