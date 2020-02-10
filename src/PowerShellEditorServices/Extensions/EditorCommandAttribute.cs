using System;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides an attribute that can be used to target PowerShell
    /// commands for import as editor commands.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EditorCommandAttribute : Attribute
    {

        #region Properties

        /// <summary>
        /// Gets or sets the name which uniquely identifies the command.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the display name for the command.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this command's output
        /// should be suppressed.
        /// </summary>
        public bool SuppressOutput { get; set; }

        #endregion
    }
}
