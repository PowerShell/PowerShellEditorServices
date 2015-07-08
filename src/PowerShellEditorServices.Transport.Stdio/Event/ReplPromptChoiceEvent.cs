//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    public class ReplPromptChoiceEvent : EventBase<ReplPromptChoiceEventBody>
    {
        public ReplPromptChoiceEvent()
        {
            this.EventType = "replPromptChoice";
        }
    }

    public class ReplPromptChoiceEventBody
    {
        public int Seq { get; set; }

        public string Caption { get; set; }

        public string Message { get; set; }

        public ReplPromptChoiceDetails[] Choices { get; set; }

        public int DefaultChoice { get; set; }
    }

    public class ReplPromptChoiceDetails
    {
        public string HelpMessage { get; set; }

        public string Label { get; set; }

        public static ReplPromptChoiceDetails FromChoiceDescription(
            ChoiceDescription choiceDescription)
        {
            return new ReplPromptChoiceDetails
            {
                Label = choiceDescription.Label,
                HelpMessage = choiceDescription.HelpMessage
            };
        }
    }

}
