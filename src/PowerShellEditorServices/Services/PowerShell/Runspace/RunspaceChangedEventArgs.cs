// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    /// <summary>
    /// Defines the set of actions that will cause the runspace to be changed.
    /// </summary>
    internal enum RunspaceChangeAction
    {
        /// <summary>
        /// The runspace change was caused by entering a new session.
        /// </summary>
        Enter,

        /// <summary>
        /// The runspace change was caused by exiting the current session.
        /// </summary>
        Exit,

        /// <summary>
        /// The runspace change was caused by shutting down the service.
        /// </summary>
        Shutdown
    }

    /// <summary>
    /// Provides arguments for the PowerShellContext.RunspaceChanged event.
    /// </summary>
    internal class RunspaceChangedEventArgs
    {
        /// <summary>
        /// Creates a new instance of the RunspaceChangedEventArgs class.
        /// </summary>
        /// <param name="changeAction">The action which caused the runspace to change.</param>
        /// <param name="previousRunspace">The previously active runspace.</param>
        /// <param name="newRunspace">The newly active runspace.</param>
        public RunspaceChangedEventArgs(
            RunspaceChangeAction changeAction,
            IRunspaceInfo previousRunspace,
            IRunspaceInfo newRunspace)
        {
            Validate.IsNotNull(nameof(previousRunspace), previousRunspace);

            ChangeAction = changeAction;
            PreviousRunspace = previousRunspace;
            NewRunspace = newRunspace;
        }

        /// <summary>
        /// Gets the RunspaceChangeAction which caused this event.
        /// </summary>
        public RunspaceChangeAction ChangeAction { get; }

        /// <summary>
        /// Gets a RunspaceDetails object describing the previous runspace.
        /// </summary>
        public IRunspaceInfo PreviousRunspace { get; }

        /// <summary>
        /// Gets a RunspaceDetails object describing the new runspace.
        /// </summary>
        public IRunspaceInfo NewRunspace { get; }
    }
}
