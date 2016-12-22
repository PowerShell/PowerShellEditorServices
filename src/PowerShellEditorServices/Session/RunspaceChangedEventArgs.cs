//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Specifies the possible types of a runspace.
    /// </summary>
    public enum RunspaceType
    {
        /// <summary>
        /// A local runspace in the current process.
        /// </summary>
        Local,

        /// <summary>
        /// A local runspace in a different process.
        /// </summary>
        Process,

        /// <summary>
        /// A runspace in a process on a another machine.
        /// </summary>
        Remote,
    }

    /// <summary>
    /// Provides arguments for the PowerShellContext.RunspaceChanged
    /// event.
    /// </summary>
    public class RunspaceChangedEventArgs
    {
        /// <summary>
        /// Gets the PowerShell version of the new runspace.
        /// </summary>
        public PowerShellVersionDetails RunspaceVersion { get; private set; }

        /// <summary>
        /// Gets the runspace type.
        /// </summary>
        public RunspaceType RunspaceType { get; private set; }

        /// <summary>
        /// Gets the "connection string" for the runspace, generally the
        /// ComputerName for a remote runspace or the ProcessId of an
        /// "Attach" runspace.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Creates a new instance of the RunspaceChangedEventArgs
        /// class.
        /// </summary>
        /// <param name="runspaceVersion">
        /// The PowerShellVersionDetails of the new runspace.
        /// </param>
        /// <param name="runspaceType">
        /// The RunspaceType of the new runspace.
        /// </param>
        /// <param name="connectionString">
        /// The connection string of the new runspace.
        /// </param>
        public RunspaceChangedEventArgs(
            PowerShellVersionDetails runspaceVersion,
            RunspaceType runspaceType,
            string connectionString)
        {
            this.RunspaceVersion = runspaceVersion;
            this.RunspaceType = runspaceType;
            this.ConnectionString = connectionString;
        }
    }
}
