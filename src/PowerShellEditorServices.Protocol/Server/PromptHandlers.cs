//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Protocol.Messages;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    internal class ProtocolPromptHandlerContext : IPromptHandlerContext
    {
        private IEventWriter eventWriter;

        public ProtocolPromptHandlerContext(IEventWriter eventWriter)
        {
            this.eventWriter = eventWriter;
        }

        public ChoicePromptHandler GetChoicePromptHandler()
        {
            return new ProtocolChoicePromptHandler(this.eventWriter);
        }

        public InputPromptHandler GetInputPromptHandler()
        {
            throw new NotImplementedException();
        }
    }

    internal class ProtocolChoicePromptHandler : ChoicePromptHandler
    {
        private IEventWriter eventWriter;

        public ProtocolChoicePromptHandler(IEventWriter eventWriter)
        {
            this.eventWriter = eventWriter;
        }

        protected override void ShowPrompt(PromptStyle promptStyle)
        {
            eventWriter.SendEvent(
                ShowChoicePromptNotification.Type,
                new ShowChoicePromptNotification
                {
                    Caption = this.Caption,
                    Message = this.Message,
                    Choices = this.Choices,
                    DefaultChoice = this.DefaultChoice
                }).ConfigureAwait(false);
        }
    }
}

