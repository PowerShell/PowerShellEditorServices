//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Console;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides an implementation of the PSHostUserInterface class
    /// for the ConsoleService and routes its calls to an IConsoleHost
    /// implementation.
    /// </summary>
    internal class ConsoleServicePSHostUserInterface : PSHostUserInterface
    {
        #region Private Fields

        private IConsoleHost consoleHost;
        private ConsoleServicePSHostRawUserInterface rawUserInterface;

        #endregion

        #region Properties

        internal IConsoleHost ConsoleHost
        {
            get { return this.consoleHost; }
            set
            {
                this.consoleHost = value;
                this.rawUserInterface.ConsoleHost = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ConsoleServicePSHostUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        public ConsoleServicePSHostUserInterface()
        {
            this.rawUserInterface = new ConsoleServicePSHostRawUserInterface();
        }

        #endregion

        #region PSHostUserInterface Implementation

        public override Dictionary<string, PSObject> Prompt(
            string promptCaption,
            string promptMessage,
            Collection<FieldDescription> fieldDescriptions)
        {
            if (this.consoleHost != null)
            {
                FieldDetails[] fields =
                    fieldDescriptions
                        .Select(FieldDetails.Create)
                        .ToArray();

                Task<Dictionary<string, object>> promptTask =
                    this.consoleHost
                        .GetInputPromptHandler()
                        .PromptForInput(
                            promptCaption,
                            promptMessage,
                            fields);

                // This will synchronously block on the async PromptForInput
                // method which gets run on another thread
                promptTask.Wait();

                if (promptTask.IsFaulted)
                {
                    // Rethrow the exception
                    throw new Exception(
                        "PromptForInput failed, check inner exception for details", 
                        promptTask.Exception);
                }
                else if (promptTask.IsCanceled)
                {
                    // Stop the pipeline if the prompt was cancelled
                    throw new PipelineStoppedException();
                }

                // Convert all values to PSObjects
                var psObjectDict = new Dictionary<string, PSObject>();
                foreach (var keyValuePair in promptTask.Result)
                {
                    psObjectDict.Add(
                        keyValuePair.Key,
                        PSObject.AsPSObject(keyValuePair.Value));
                }

                // Return the result
                return psObjectDict;
            }
            else
            {
                // Notify the caller that there's no implementation
                throw new NotImplementedException();
            }
        }

        public override int PromptForChoice(
            string promptCaption, 
            string promptMessage, 
            Collection<ChoiceDescription> choiceDescriptions, 
            int defaultChoice)
        {
            if (this.consoleHost != null)
            {
                ChoiceDetails[] choices =
                    choiceDescriptions
                        .Select(ChoiceDetails.Create)
                        .ToArray();

                Task<int> promptTask =
                    this.consoleHost
                        .GetChoicePromptHandler()
                        .PromptForChoice(
                            promptCaption,
                            promptMessage,
                            choices,
                            defaultChoice);

                // This will synchronously block on the async PromptForChoice
                // method which gets run on another thread
                promptTask.Wait();

                if (promptTask.IsFaulted)
                {
                    // Rethrow the exception
                    throw new Exception(
                        "PromptForChoice failed, check inner exception for details", 
                        promptTask.Exception);
                }
                else if (promptTask.IsCanceled)
                {
                    // Stop the pipeline if the prompt was cancelled
                    throw new PipelineStoppedException();
                }

                // Return the result
                return promptTask.Result;
            }
            else
            {
                // Notify the caller that there's no implementation
                throw new NotImplementedException();
            }
        }

        public override PSCredential PromptForCredential(
            string caption, 
            string message, 
            string userName, 
            string targetName, 
            PSCredentialTypes allowedCredentialTypes, 
            PSCredentialUIOptions options)
        {
            throw new NotImplementedException();
        }

        public override PSCredential PromptForCredential(
            string caption, 
            string message, 
            string userName, 
            string targetName)
        {
            throw new NotImplementedException();
        }

        public override PSHostRawUserInterface RawUI
        {
            get { return this.rawUserInterface; }
        }

        public override string ReadLine()
        {
            if (this.consoleHost != null)
            {
                Task<string> promptTask =
                    this.consoleHost
                        .GetInputPromptHandler()
                        .PromptForInput();

                // This will synchronously block on the async PromptForInput
                // method which gets run on another thread
                promptTask.Wait();

                if (promptTask.IsFaulted)
                {
                    // Rethrow the exception
                    throw new Exception(
                        "ReadLine failed, check inner exception for details",
                        promptTask.Exception);
                }
                else if (promptTask.IsCanceled)
                {
                    // Stop the pipeline if the prompt was cancelled
                    throw new PipelineStoppedException();
                }

                return promptTask.Result;
            }
            else
            {
                // Notify the caller that there's no implementation
                throw new NotImplementedException();
            }
        }

        public override SecureString ReadLineAsSecureString()
        {
            throw new NotImplementedException();
        }

        public override void Write(
            ConsoleColor foregroundColor, 
            ConsoleColor backgroundColor, 
            string value)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.WriteOutput(
                    value,
                    false,
                    OutputType.Normal,
                    foregroundColor,
                    backgroundColor);
            }
        }

        public override void Write(string value)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.WriteOutput(
                    value,
                    false,
                    OutputType.Normal,
                    this.rawUserInterface.ForegroundColor,
                    this.rawUserInterface.BackgroundColor);
            }
        }

        public override void WriteLine(string value)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.WriteOutput(
                    value,
                    true,
                    OutputType.Normal,
                    this.rawUserInterface.ForegroundColor,
                    this.rawUserInterface.BackgroundColor);
            }
        }

        public override void WriteDebugLine(string message)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.WriteOutput(
                    message,
                    true,
                    OutputType.Debug);
            }
        }

        public override void WriteVerboseLine(string message)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.WriteOutput(
                    message,
                    true,
                    OutputType.Verbose);
            }
        }

        public override void WriteWarningLine(string message)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.WriteOutput(
                    message,
                    true,
                    OutputType.Warning);
            }
        }

        public override void WriteErrorLine(string value)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.WriteOutput(
                    value, 
                    true,
                    OutputType.Error,
                    ConsoleColor.Red);
            }
        }

        public override void WriteProgress(
            long sourceId, 
            ProgressRecord record)
        {
            if (this.consoleHost != null)
            {
                this.consoleHost.UpdateProgress(
                    sourceId,
                    ProgressDetails.Create(record));
            }
        }

        #endregion
    }
}
