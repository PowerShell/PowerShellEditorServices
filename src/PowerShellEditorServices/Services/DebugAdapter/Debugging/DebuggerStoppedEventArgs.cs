//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Provides event arguments for the DebugService.DebuggerStopped event.
    /// </summary>
    internal class DebuggerStoppedEventArgs
    {
        #region Properties

        /// <summary>
        /// Gets the path of the script where the debugger has stopped execution.
        /// If 'IsRemoteSession' returns true, this path will be a local filesystem
        /// path containing the contents of the script that is executing remotely.
        /// </summary>
        public string ScriptPath { get; private set; }

        /// <summary>
        /// Returns true if the breakpoint was raised from a remote debugging session.
        /// </summary>
        public bool IsRemoteSession
        {
            get { return this.RunspaceDetails.Location == RunspaceLocation.Remote; }
        }

        /// <summary>
        /// Gets the original script path if 'IsRemoteSession' returns true.
        /// </summary>
        public string RemoteScriptPath { get; private set; }

        /// <summary>
        /// Gets the RunspaceDetails for the current runspace.
        /// </summary>
        public RunspaceDetails RunspaceDetails { get; private set; }

        /// <summary>
        /// Gets the line number at which the debugger stopped execution.
        /// </summary>
        public int LineNumber
        {
            get
            {
                return this.OriginalEvent.InvocationInfo.ScriptLineNumber;
            }
        }

        /// <summary>
        /// Gets the column number at which the debugger stopped execution.
        /// </summary>
        public int ColumnNumber
        {
            get
            {
                return this.OriginalEvent.InvocationInfo.OffsetInLine;
            }
        }

        /// <summary>
        /// Gets the original DebuggerStopEventArgs from the PowerShell engine.
        /// </summary>
        public DebuggerStopEventArgs OriginalEvent { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the DebuggerStoppedEventArgs class.
        /// </summary>
        /// <param name="originalEvent">The original DebuggerStopEventArgs instance from which this instance is based.</param>
        /// <param name="runspaceDetails">The RunspaceDetails of the runspace which raised this event.</param>
        public DebuggerStoppedEventArgs(
            DebuggerStopEventArgs originalEvent,
            RunspaceDetails runspaceDetails)
            : this(originalEvent, runspaceDetails, null)
        {
        }

        /// <summary>
        /// Creates a new instance of the DebuggerStoppedEventArgs class.
        /// </summary>
        /// <param name="originalEvent">The original DebuggerStopEventArgs instance from which this instance is based.</param>
        /// <param name="runspaceDetails">The RunspaceDetails of the runspace which raised this event.</param>
        /// <param name="localScriptPath">The local path of the remote script being debugged.</param>
        public DebuggerStoppedEventArgs(
            DebuggerStopEventArgs originalEvent,
            RunspaceDetails runspaceDetails,
            string localScriptPath)
        {
            Validate.IsNotNull(nameof(originalEvent), originalEvent);
            Validate.IsNotNull(nameof(runspaceDetails), runspaceDetails);

            if (!string.IsNullOrEmpty(localScriptPath))
            {
                this.ScriptPath = localScriptPath;
                this.RemoteScriptPath = originalEvent.InvocationInfo.ScriptName;
            }
            else
            {
                this.ScriptPath = originalEvent.InvocationInfo.ScriptName;
            }

            this.OriginalEvent = originalEvent;
            this.RunspaceDetails = runspaceDetails;
        }

        #endregion
    }
}
