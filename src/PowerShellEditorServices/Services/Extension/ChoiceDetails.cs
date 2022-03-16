// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Services.Extension
{
    /// <summary>
    /// Contains the details about a choice that should be displayed
    /// to the user.  This class is meant to be serializable to the
    /// user's UI.
    /// </summary>
    internal class ChoiceDetails
    {
        #region Private Fields

        private readonly string hotKeyString;

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
            HelpMessage = helpMessage;

            HotKeyIndex = label.IndexOf('&');
            if (HotKeyIndex >= 0)
            {
                Label = label.Remove(HotKeyIndex, 1);

                if (HotKeyIndex < Label.Length)
                {
                    hotKeyString = Label[HotKeyIndex].ToString().ToUpper();
                    HotKeyCharacter = hotKeyString[0];
                }
            }
            else
            {
                Label = label;
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
                string.Equals(inputString, hotKeyString, StringComparison.CurrentCultureIgnoreCase) ||
                string.Equals(inputString, Label, StringComparison.CurrentCultureIgnoreCase);
        }

        #endregion
    }
}
