// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Services.Extension
{
    internal class ShowChoicePromptRequest
    {
        public bool IsMultiChoice { get; set; }

        public string Caption { get; set; }

        public string Message { get; set; }

        public ChoiceDetails[] Choices { get; set; }

        public int[] DefaultChoices { get; set; }
    }

    internal class ShowChoicePromptResponse
    {
        public bool PromptCancelled { get; set; }

        public string ResponseText { get; set; }
    }

    internal class ShowInputPromptRequest
    {
        /// <summary>
        /// Gets or sets the name of the field.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the descriptive label for the field.
        /// </summary>
        public string Label { get; set; }
    }

    internal class ShowInputPromptResponse
    {
        public bool PromptCancelled { get; set; }

        public string ResponseText { get; set; }
    }
}
