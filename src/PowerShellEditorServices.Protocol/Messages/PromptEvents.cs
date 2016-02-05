//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.Messages
{
    public class ShowChoicePromptRequest
    {
        public static readonly
            RequestType<ShowChoicePromptRequest, ShowChoicePromptResponse> Type =
            RequestType<ShowChoicePromptRequest, ShowChoicePromptResponse>.Create("powerShell/showChoicePrompt");

        public string Caption { get; set; }

        public string Message { get; set; }

        public ChoiceDetails[] Choices { get; set; }

        public int DefaultChoice { get; set; }
    }

    public class ShowChoicePromptResponse
    {
        public bool PromptCancelled { get; set; }

        public string ChosenItem { get; set; }
    }

    public class ShowInputPromptRequest
    {
        public static readonly
            RequestType<ShowInputPromptRequest, ShowInputPromptResponse> Type =
            RequestType<ShowInputPromptRequest, ShowInputPromptResponse>.Create("powerShell/showInputPrompt");

        /// <summary>
        /// Gets or sets the name of the field.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the descriptive label for the field.
        /// </summary>
        public string Label { get; set; }
    }

    public class ShowInputPromptResponse
    {
        public bool PromptCancelled { get; set; }

        public string ResponseText { get; set; }
    }
}

