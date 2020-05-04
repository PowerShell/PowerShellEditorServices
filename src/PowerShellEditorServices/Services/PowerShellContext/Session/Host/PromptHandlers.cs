//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using System.Threading;
using System.Security;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal class ProtocolChoicePromptHandler : ConsoleChoicePromptHandler
    {
        private readonly ILanguageServer _languageServer;
        private readonly IHostInput _hostInput;
        private TaskCompletionSource<string> _readLineTask;

        public ProtocolChoicePromptHandler(
            ILanguageServer languageServer,
            IHostInput hostInput,
            IHostOutput hostOutput,
            ILogger logger)
                : base(hostOutput, logger)
        {
            _languageServer = languageServer;
            this._hostInput = hostInput;
            this.hostOutput = hostOutput;
        }

        protected override void ShowPrompt(PromptStyle promptStyle)
        {
            base.ShowPrompt(promptStyle);

            _languageServer.SendRequest<ShowChoicePromptRequest>(
                "powerShell/showChoicePrompt",
                new ShowChoicePromptRequest
                {
                    IsMultiChoice = this.IsMultiChoice,
                    Caption = this.Caption,
                    Message = this.Message,
                    Choices = this.Choices,
                    DefaultChoices = this.DefaultChoices
                })
                .Returning<ShowChoicePromptResponse>(CancellationToken.None)
                .ContinueWith(HandlePromptResponse)
                .ConfigureAwait(false);
        }

        protected override Task<string> ReadInputStringAsync(CancellationToken cancellationToken)
        {
            this._readLineTask = new TaskCompletionSource<string>();
            return this._readLineTask.Task;
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

                    this._readLineTask.TrySetResult(response.ResponseText);
                }
                else
                {
                    // Cancel the current prompt
                    this._hostInput.SendControlC();
                }
            }
            else
            {
                if (responseTask.IsFaulted)
                {
                    // Log the error
                    Logger.LogError(
                        "ShowChoicePrompt request failed with error:\r\n{0}",
                        responseTask.Exception.ToString());
                }

                // Cancel the current prompt
                this._hostInput.SendControlC();
            }

            this._readLineTask = null;
        }
    }

    internal class ProtocolInputPromptHandler : ConsoleInputPromptHandler
    {
        private readonly ILanguageServer _languageServer;
        private readonly IHostInput hostInput;
        private TaskCompletionSource<string> readLineTask;

        public ProtocolInputPromptHandler(
            ILanguageServer languageServer,
            IHostInput hostInput,
            IHostOutput hostOutput,
            ILogger logger)
                : base(hostOutput, logger)
        {
            _languageServer = languageServer;
            this.hostInput = hostInput;
            this.hostOutput = hostOutput;
        }

        protected override void ShowFieldPrompt(FieldDetails fieldDetails)
        {
            base.ShowFieldPrompt(fieldDetails);

            _languageServer.SendRequest<ShowInputPromptRequest>(
                "powerShell/showInputPrompt",
                new ShowInputPromptRequest
                {
                    Name = fieldDetails.Name,
                    Label = fieldDetails.Label
                }).Returning<ShowInputPromptResponse>(CancellationToken.None)
                .ContinueWith(HandlePromptResponse)
                .ConfigureAwait(false);
        }

        protected override Task<string> ReadInputStringAsync(CancellationToken cancellationToken)
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
                    Logger.LogError(
                        "ShowInputPrompt request failed with error:\r\n{0}",
                        responseTask.Exception.ToString());
                }

                // Cancel the current prompt
                this.hostInput.SendControlC();
            }

            this.readLineTask = null;
        }

        protected override Task<SecureString> ReadSecureStringAsync(CancellationToken cancellationToken)
        {
            // TODO: Write a message to the console
            throw new NotImplementedException();
        }
    }
}
