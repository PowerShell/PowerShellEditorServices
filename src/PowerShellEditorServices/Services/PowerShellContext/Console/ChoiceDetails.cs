//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Contains the details about a choice that should be displayed
    /// to the user.  This class is meant to be serializable to the
    /// user's UI.
    /// </summary>
    internal class ChoiceDetails
    {
        #region Private Fields

        private string hotKeyString;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the label for the choice.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets the index of the hot key character for the choice.
        /// </summary>
        public int HotKeyIndex { get; set; }

        /// <summary>
        /// Gets the hot key character.
        /// </summary>
        public char? HotKeyCharacter { get; set; }

        /// <summary>
        /// Gets the help string that describes the choice.
        /// </summary>
        public string HelpMessage { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ChoiceDetails class with
        /// the provided details.
        /// </summary>
        public ChoiceDetails()
        {
            // Parameterless constructor for deserialization.
        }

        /// <summary>
        /// Creates an instance of the ChoiceDetails class with
        /// the provided details.
        /// </summary>
        /// <param name="label">
        /// The label of the choice.  An ampersand '&amp;' may be inserted
        /// before the character that will used as a hot key for the
        /// choice.
        /// </param>
        /// <param name="helpMessage">
        /// A help message that describes the purpose of the choice.
        /// </param>
        public ChoiceDetails(string label, string helpMessage)
        {
            this.HelpMessage = helpMessage;

            this.HotKeyIndex = label.IndexOf('&');
            if (this.HotKeyIndex >= 0)
            {
                this.Label = label.Remove(this.HotKeyIndex, 1);

                if (this.HotKeyIndex < this.Label.Length)
                {
                    this.hotKeyString = this.Label[this.HotKeyIndex].ToString().ToUpper();
                    this.HotKeyCharacter = this.hotKeyString[0];
                }
            }
            else
            {
                this.Label = label;
            }
        }

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
            return new ChoiceDetails(
                choiceDescription.Label,
                choiceDescription.HelpMessage);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Compares an input string to this choice to determine
        /// whether the input string is a match.
        /// </summary>
        /// <param name="inputString">
        /// The input string to compare to the choice.
        /// </param>
        /// <returns>True if the input string is a match for the choice.</returns>
        public bool MatchesInput(string inputString)
        {
            // Make sure the input string is trimmed of whitespace
            inputString = inputString.Trim();

            // Is it the hotkey?
            return
                string.Equals(inputString, this.hotKeyString, StringComparison.CurrentCultureIgnoreCase) ||
                string.Equals(inputString, this.Label, StringComparison.CurrentCultureIgnoreCase);
        }

        #endregion
    }
}
