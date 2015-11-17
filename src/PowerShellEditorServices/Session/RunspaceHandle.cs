//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a handle to the runspace that is managed by
    /// a PowerShellContext.  The holder of this handle.
    /// </summary>
    public class RunspaceHandle : IDisposable
    {
        private PowerShellContext powerShellContext;

        /// <summary>
        /// Gets the runspace that is held by this handle.
        /// </summary>
        public Runspace Runspace { get; private set; }

        /// <summary>
        /// Initializes a new instance of the RunspaceHandle class using the
        /// given runspace.
        /// </summary>
        /// <param name="runspace">The runspace instance which is temporarily owned by this handle.</param>
        /// <param name="powerShellContext">The PowerShellContext instance which manages the runspace.</param>
        public RunspaceHandle(Runspace runspace, PowerShellContext powerShellContext)
        {
            this.Runspace = runspace;
            this.powerShellContext = powerShellContext;
        }

        /// <summary>
        /// Disposes the RunspaceHandle once the holder is done using it.
        /// Causes the handle to be released back to the PowerShellContext.
        /// </summary>
        public void Dispose()
        {
            // Release the handle and clear the runspace so that
            // no further operations can be performed on it.
            this.powerShellContext.ReleaseRunspaceHandle(this);
            this.Runspace = null;
        }
    }
}

