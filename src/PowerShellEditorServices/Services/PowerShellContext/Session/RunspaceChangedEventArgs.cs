//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
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
        /// Gets the RunspaceChangeAction which caused this event.
        /// </summary>
        public RunspaceChangeAction ChangeAction { get; private set; }

        /// <summary>
        /// Gets a RunspaceDetails object describing the previous runspace.
        /// </summary>
        public RunspaceDetails PreviousRunspace { get; private set; }

        /// <summary>
        /// Gets a RunspaceDetails object describing the new runspace.
        /// </summary>
        public RunspaceDetails NewRunspace { get; private set; }

        /// <summary>
        /// Creates a new instance of the RunspaceChangedEventArgs class.
        /// </summary>
        /// <param name="changeAction">The action which caused the runspace to change.</param>
        /// <param name="previousRunspace">The previously active runspace.</param>
        /// <param name="newRunspace">The newly active runspace.</param>
        public RunspaceChangedEventArgs(
            RunspaceChangeAction changeAction,
            RunspaceDetails previousRunspace,
            RunspaceDetails newRunspace)
        {
            Validate.IsNotNull(nameof(previousRunspace), previousRunspace);

            this.ChangeAction = changeAction;
            this.PreviousRunspace = previousRunspace;
            this.NewRunspace = newRunspace;
        }
    }
}
