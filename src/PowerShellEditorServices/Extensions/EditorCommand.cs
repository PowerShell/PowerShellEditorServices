// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides details about a command that has been registered
    /// with the editor.
    /// </summary>
    public sealed class EditorCommand
    {
        #region Properties

        /// <summary>
        /// Gets the name which uniquely identifies the command.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the display name for the command.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the boolean which determines whether this command's
        /// output should be suppressed.
        /// </summary>
        public bool SuppressOutput { get; }

        /// <summary>
        /// Gets the ScriptBlock which can be used to execute the command.
        /// </summary>
        public ScriptBlock ScriptBlock { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new EditorCommand instance that invokes a cmdlet or
        /// function by name.
        /// </summary>
        /// <param name="commandName">The unique identifier name for the command.</param>
        /// <param name="displayName">The display name for the command.</param>
        /// <param name="suppressOutput">If true, causes output to be suppressed for this command.</param>
        /// <param name="cmdletName">The name of the cmdlet or function which will be invoked by this command.</param>
        public EditorCommand(
            string commandName,
            string displayName,
            bool suppressOutput,
            string cmdletName)
            : this(
                  commandName,
                  displayName,
                  suppressOutput,
                  ScriptBlock.Create(
                      string.Format(
                          "param($context) {0} $context",
                          cmdletName)))
        {
        }

        /// <summary>
        /// Creates a new EditorCommand instance that invokes a ScriptBlock.
        /// </summary>
        /// <param name="commandName">The unique identifier name for the command.</param>
        /// <param name="displayName">The display name for the command.</param>
        /// <param name="suppressOutput">If true, causes output to be suppressed for this command.</param>
        /// <param name="scriptBlock">The ScriptBlock which will be invoked by this command.</param>
        public EditorCommand(
            string commandName,
            string displayName,
            bool suppressOutput,
            ScriptBlock scriptBlock)
        {
            Name = commandName;
            DisplayName = displayName;
            SuppressOutput = suppressOutput;
            ScriptBlock = scriptBlock;
        }

        #endregion
    }
}
