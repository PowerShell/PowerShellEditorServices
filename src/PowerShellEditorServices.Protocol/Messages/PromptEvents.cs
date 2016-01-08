//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.Messages
{
    public class ShowChoicePromptNotification
    {
        public static readonly
            EventType<ShowChoicePromptNotification> Type =
            EventType<ShowChoicePromptNotification>.Create("powerShell/showChoicePrompt");

        public string Caption { get; set; }

        public string Message { get; set; }

        public ChoiceDetails[] Choices { get; set; }

        public int DefaultChoice { get; set; }
    }

    public class CompleteChoicePromptNotification
    {
        public static readonly
            EventType<CompleteChoicePromptNotification> Type =
            EventType<CompleteChoicePromptNotification>.Create("powerShell/completeChoicePrompt");

        public bool PromptCancelled { get; set; }

        public string ChosenItem { get; set; }
    }
}

