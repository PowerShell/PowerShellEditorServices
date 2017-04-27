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
            RequestType<ShowChoicePromptRequest, ShowChoicePromptResponse, object, object> Type =
            RequestType<ShowChoicePromptRequest, ShowChoicePromptResponse, object, object>.Create("powerShell/showChoicePrompt");

        public bool IsMultiChoice { get; set; }

        public string Caption { get; set; }

        public string Message { get; set; }

        public ChoiceDetails[] Choices { get; set; }

        public int[] DefaultChoices { get; set; }
    }

    public class ShowChoicePromptResponse
    {
        public bool PromptCancelled { get; set; }

        public string ResponseText { get; set; }
    }

    public class ShowInputPromptRequest
    {
        public static readonly
            RequestType<ShowInputPromptRequest, ShowInputPromptResponse, object, object> Type =
            RequestType<ShowInputPromptRequest, ShowInputPromptResponse, object, object>.Create("powerShell/showInputPrompt");

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

