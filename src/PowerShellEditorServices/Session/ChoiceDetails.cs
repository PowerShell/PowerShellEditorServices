//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Contains the details about a choice that should be displayed
    /// to the user.  This class is meant to be serializable to the 
    /// user's UI.
    /// </summary>
    public class ChoiceDetails
    {
        #region Properties

        /// <summary>
        /// Gets or sets the label for the choice.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the help string that describes the choice.
        /// </summary>
        public string HelpMessage { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the ChoicePromptDetails class
        /// based on a ChoiceDescription from the PowerShell layer.
        /// </summary>
        /// <param name="choiceDescription">
        /// A ChoiceDescription on which this instance will be based.
        /// </param>
        /// <returns>A new ChoicePromptDetails instance.</returns>
        public static ChoiceDetails Create(ChoiceDescription choiceDescription)
        {
            return new ChoiceDetails
            {
                Label = choiceDescription.Label,
                HelpMessage = choiceDescription.HelpMessage
            };
        }

        #endregion
    }
}
