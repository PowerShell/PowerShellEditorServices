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

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    internal class ProtocolPromptHandlerContext : IPromptHandlerContext
    {
        private IMessageSender messageSender;
        private ConsoleService consoleService;

        public ProtocolPromptHandlerContext(
            IMessageSender messageSender,
            ConsoleService consoleService)
        {
            this.messageSender = messageSender;
            this.consoleService = consoleService;
        }

        public ChoicePromptHandler GetChoicePromptHandler()
        {
            return new ProtocolChoicePromptHandler(
                this.messageSender,
                this.consoleService);
        }

        public InputPromptHandler GetInputPromptHandler()
        {
            return new ProtocolInputPromptHandler(
                this.messageSender,
                this.consoleService);
        }
    }

    internal class ProtocolChoicePromptHandler : ConsoleChoicePromptHandler
    {
        private IMessageSender messageSender;
        private ConsoleService consoleService;

        public ProtocolChoicePromptHandler(
            IMessageSender messageSender,
            ConsoleService consoleService)
                : base(consoleService)
        {
            this.messageSender = messageSender;
            this.consoleService = consoleService;
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

        private void HandlePromptResponse(
            Task<ShowChoicePromptResponse> responseTask)
        {
            if (responseTask.IsCompleted)
            {
                ShowChoicePromptResponse response = responseTask.Result;

                if (!response.PromptCancelled)
                {
                    this.consoleService.ReceivePromptResponse(
                        response.ResponseText,
                        false);
                }
                else
                {
                    // Cancel the current prompt
                    this.consoleService.SendControlC();
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
                this.consoleService.SendControlC();
            }
        }
    }

    internal class ProtocolInputPromptHandler : ConsoleInputPromptHandler
    {
        private IMessageSender messageSender;
        private ConsoleService consoleService;

        public ProtocolInputPromptHandler(
            IMessageSender messageSender,
            ConsoleService consoleService)
                : base(consoleService)
        {
            this.messageSender = messageSender;
            this.consoleService = consoleService;
        }

        protected override void ShowErrorMessage(Exception e)
        {
            // Use default behavior for writing the error message
            base.ShowErrorMessage(e);
        }

        protected override void ShowPromptMessage(string caption, string message)
        {
            // Use default behavior for writing the prompt message
            base.ShowPromptMessage(caption, message);
        }

        protected override void ShowFieldPrompt(FieldDetails fieldDetails)
        {
            // Write the prompt to the console first so that there's a record
            // of it occurring
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

        private void HandlePromptResponse(
            Task<ShowInputPromptResponse> responseTask)
        {
            if (responseTask.IsCompleted)
            {
                ShowInputPromptResponse response = responseTask.Result;

                if (!response.PromptCancelled)
                {
                    this.consoleService.ReceivePromptResponse(
                        response.ResponseText,
                        true);
                }
                else
                {
                    // Cancel the current prompt
                    this.consoleService.SendControlC();
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
                this.consoleService.SendControlC();
            }
        }
    }
}

