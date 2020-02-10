//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides a handle to the runspace that is managed by
    /// a PowerShellContext.  The holder of this handle.
    /// </summary>
    internal class RunspaceHandle : IDisposable
    {
        private PowerShellContextService powerShellContext;

        /// <summary>
        /// Gets the runspace that is held by this handle.
        /// </summary>
        public Runspace Runspace
        {
            get
            {
                return ((IHostSupportsInteractiveSession)this.powerShellContext).Runspace;
            }
        }

        internal bool IsReadLine { get; }

        /// <summary>
        /// Initializes a new instance of the RunspaceHandle class using the
        /// given runspace.
        /// </summary>
        /// <param name="powerShellContext">The PowerShellContext instance which manages the runspace.</param>
        public RunspaceHandle(PowerShellContextService powerShellContext)
            : this(powerShellContext, false)
        { }

        internal RunspaceHandle(PowerShellContextService powerShellContext, bool isReadLine)
        {
            this.powerShellContext = powerShellContext;
            this.IsReadLine = isReadLine;
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
        }
    }
}

