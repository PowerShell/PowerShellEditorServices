//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Protocol.Messages;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Threading.Tasks;
using System.Threading;
using System.Security;

namespace Microsoft.PowerShell.EditorServices.Host
{
    internal class ProtocolChoicePromptHandler : ConsoleChoicePromptHandler
    {
        private IHostInput hostInput;
        private IMessageSender messageSender;
        private TaskCompletionSource<string> readLineTask;

        public ProtocolChoicePromptHandler(
            IMessageSender messageSender,
            IHostInput hostInput,
            IHostOutput hostOutput,
            IPsesLogger logger)
                : base(hostOutput, logger)
        {
            this.hostInput = hostInput;
            this.hostOutput = hostOutput;
            this.messageSender = messageSender;
        }

        protected override void ShowPrompt(PromptStyle promptStyle)
        {
            base.ShowPrompt(promptStyle);

            messageSender
                .SendRequest(
                    ShowChoicePromptRequest.Type,
                    new ShowChoicePromptRequest
                    {
                        IsMultiChoice = this.IsMultiChoice,
                        Caption = this.Caption,
                        Message = this.Message,
                        Choices = this.Choices,
                        DefaultChoices = this.DefaultChoices
                    }, true)
                .ContinueWith(HandlePromptResponse)
                .ConfigureAwait(false);
        }

        protected override Task<string> ReadInputString(CancellationToken cancellationToken)
        {
            this.readLineTask = new TaskCompletionSource<string>();
            return this.readLineTask.Task;
        }

        private void HandlePromptResponse(
            Task<ShowChoicePromptResponse> responseTask)
        {
            if (responseTask.IsCompleted)
            {
                ShowChoicePromptResponse response = responseTask.Result;

                if (!response.PromptCancelled)
                {
                    this.hostOutput.WriteOutput(
                        response.ResponseText,
                        OutputType.Normal);

                    this.readLineTask.TrySetResult(response.ResponseText);
                }
                else
                {
                    // Cancel the current prompt
                    this.hostInput.SendControlC();
                }
            }
            else
            {
                if (responseTask.IsFaulted)
                {
                    // Log the error
                    Logger.Write(
                        LogLevel.Error,
                        "ShowChoicePrompt request failed with error:\r\n{0}",
                        responseTask.Exception.ToString());
                }

                // Cancel the current prompt
                this.hostInput.SendControlC();
            }

            this.readLineTask = null;
        }
    }

    internal class ProtocolInputPromptHandler : ConsoleInputPromptHandler
    {
        private IHostInput hostInput;
        private IMessageSender messageSender;
        private TaskCompletionSource<string> readLineTask;

        public ProtocolInputPromptHandler(
            IMessageSender messageSender,
            IHostInput hostInput,
            IHostOutput hostOutput,
            IPsesLogger logger)
                : base(hostOutput, logger)
        {
            this.hostInput = hostInput;
            this.hostOutput = hostOutput;
            this.messageSender = messageSender;
        }

        protected override void ShowFieldPrompt(FieldDetails fieldDetails)
        {
            base.ShowFieldPrompt(fieldDetails);

            messageSender
                .SendRequest(
                    ShowInputPromptRequest.Type,
                    new ShowInputPromptRequest
                    {
                        Name = fieldDetails.Name,
                        Label = fieldDetails.Label
                    }, true)
                .ContinueWith(HandlePromptResponse)
                .ConfigureAwait(false);
        }

        protected override Task<string> ReadInputString(CancellationToken cancellationToken)
        {
            this.readLineTask = new TaskCompletionSource<string>();
            return this.readLineTask.Task;
        }

        private void HandlePromptResponse(
            Task<ShowInputPromptResponse> responseTask)
        {
            if (responseTask.IsCompleted)
            {
                ShowInputPromptResponse response = responseTask.Result;

                if (!response.PromptCancelled)
                {
                    this.hostOutput.WriteOutput(
                        response.ResponseText,
                        OutputType.Normal);

                    this.readLineTask.TrySetResult(response.ResponseText);
                }
                else
                {
                    // Cancel the current prompt
                    this.hostInput.SendControlC();
                }
            }
            else
            {
                if (responseTask.IsFaulted)
                {
                    // Log the error
                    Logger.Write(
                        LogLevel.Error,
                        "ShowInputPrompt request failed with error:\r\n{0}",
                        responseTask.Exception.ToString());
                }

                // Cancel the current prompt
                this.hostInput.SendControlC();
            }

            this.readLineTask = null;
        }

        protected override Task<SecureString> ReadSecureString(CancellationToken cancellationToken)
        {
            // TODO: Write a message to the console
            throw new NotImplementedException();
        }
    }
}
