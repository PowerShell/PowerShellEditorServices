//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.CSharp.RuntimeBinder;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Specifies the possible types of a runspace.
    /// </summary>
    public enum RunspaceLocation
    {
        /// <summary>
        /// A runspace in the current process on the same machine.
        /// </summary>
        Local,

        /// <summary>
        /// A runspace in a different process on the same machine.
        /// </summary>
        LocalProcess,

        /// <summary>
        /// A runspace on a different machine.
        /// </summary>
        Remote,

        // NOTE: We don't have a RemoteProcess variable here because there's
        // no reliable way to know when the user has used Enter-PSHostProcess
        // to jump into a different PowerShell process on the remote machine.
        // Even if we check the PID every time the prompt gets written, there's
        // still a chance the user is running a script that contains the
        // Enter-PSHostProcess command and it won't be caught until after the
        // script finishes.
    }

    /// <summary>
    /// Provides details about a runspace being used in the current
    /// editing session.
    /// </summary>
    public class RunspaceDetails
    {
        #region Properties

        /// <summary>
        /// Gets the id of the underlying Runspace object.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Gets the Runspace instance for which this class contains details.
        /// </summary>
        internal Runspace Runspace { get; private set; }

        /// <summary>
        /// Gets the PowerShell version of the new runspace.
        /// </summary>
        public PowerShellVersionDetails PowerShellVersion { get; private set; }

        /// <summary>
        /// Gets the runspace location, either Local or Remote.
        /// </summary>
        public RunspaceLocation Location { get; private set; }

        /// <summary>
        /// Gets a boolean which indicates whether this runspace is the result
        /// of attaching with Debug-Runspace.
        /// </summary>
        public bool IsAttached { get; private set; }

        /// <summary>
        /// Gets the "connection string" for the runspace, generally the
        /// ComputerName for a remote runspace or the ProcessId of an
        /// "Attach" runspace.
        /// </summary>
        public string ConnectionString { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the RunspaceDetails class.
        /// </summary>
        /// <param name="runspace">
        /// The runspace for which this instance contains details.
        /// </param>
        /// <param name="powerShellVersion">
        /// The PowerShellVersionDetails of the runspace.
        /// </param>
        /// <param name="runspaceLocation">
        /// The RunspaceLocale of the runspace.
        /// </param>
        /// <param name="connectionString">
        /// The connection string of the runspace.
        /// </param>
        public RunspaceDetails(
            Runspace runspace,
            PowerShellVersionDetails powerShellVersion,
            RunspaceLocation runspaceLocation,
            string connectionString)
                : this(
                      runspace.InstanceId,
                      runspace,
                      powerShellVersion,
                      runspaceLocation,
                      connectionString)
        {
        }

        /// <summary>
        /// Creates a new instance of the RunspaceDetails class.
        /// </summary>
        /// <param name="instanceId">
        /// The InstanceId Guid for the runspace.
        /// </param>
        /// <param name="runspace">
        /// The runspace for which this instance contains details.
        /// </param>
        /// <param name="powerShellVersion">
        /// The PowerShellVersionDetails of the runspace.
        /// </param>
        /// <param name="runspaceLocation">
        /// The RunspaceLocale of the runspace.
        /// </param>
        /// <param name="connectionString">
        /// The connection string of the runspace.
        /// </param>
        public RunspaceDetails(
            Guid instanceId,
            Runspace runspace,
            PowerShellVersionDetails powerShellVersion,
            RunspaceLocation runspaceLocation,
            string connectionString)
        {
            this.Id = instanceId;
            this.Runspace = runspace;
            this.PowerShellVersion = powerShellVersion;
            this.Location = runspaceLocation;
            this.ConnectionString = connectionString;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates and populates a new RunspaceDetails instance for the given runspace.
        /// </summary>
        /// <param name="runspace">The runspace for which details will be gathered.</param>
        /// <returns>A new RunspaceDetails instance.</returns>
        public static RunspaceDetails Create(Runspace runspace)
        {
            Validate.IsNotNull(nameof(runspace), runspace);

            var runspaceId = runspace.InstanceId;
            var runspaceLocation = RunspaceLocation.Local;
            var versionDetails = PowerShellVersionDetails.GetVersionDetails(runspace);

            string connectionString = null;

            if (runspace.ConnectionInfo != null)
            {
                // Use 'dynamic' to avoid missing NamedPipeRunspaceConnectionInfo
                // on PS v3 and v4
                try
                {
                    dynamic connectionInfo = runspace.ConnectionInfo;
                    if (connectionInfo.ProcessId != null)
                    {
                        runspaceLocation = RunspaceLocation.LocalProcess;
                        connectionString = connectionInfo.ProcessId.ToString();
                    }
                }
                catch (RuntimeBinderException)
                {
                    // ProcessId property isn't on the object, move on.
                }

                if (runspace.ConnectionInfo.ComputerName != "localhost")
                {
                    runspaceId =
                        PowerShellContext.ExecuteScriptAndGetItem<Guid>(
                            "$host.Runspace.InstanceId",
                            runspace);

                    runspaceLocation = RunspaceLocation.Remote;
                    connectionString =
                        runspace.ConnectionInfo.ComputerName +
                        (connectionString != null ? $"-{connectionString}" : string.Empty);
                }
            }

            return
                new RunspaceDetails(
                    runspaceId,
                    runspace,
                    versionDetails,
                    runspaceLocation,
                    connectionString);
        }

        /// <summary>
        /// Creates a clone of the given runspace through which another
        /// runspace was attached.  Sets the IsAttached property of the
        /// resulting RunspaceDetails object to true.
        /// </summary>
        /// <param name="runspaceDetails">
        /// The RunspaceDetails object which the new object based.
        /// </param>
        /// <param name="attachedRunspaceId">
        /// The id of the runspace that has been attached.
        /// </param>
        /// <returns>
        /// A new RunspaceDetails instance for the attached runspace.
        /// </returns>
        public static RunspaceDetails CreateAttached(
            RunspaceDetails runspaceDetails,
            Guid attachedRunspaceId)
        {
            RunspaceDetails newRunspace =
                new RunspaceDetails(
                    attachedRunspaceId,
                    runspaceDetails.Runspace,
                    runspaceDetails.PowerShellVersion,
                    runspaceDetails.Location,
                    runspaceDetails.ConnectionString);

            // Since this is an attached runspace, set the IsAttached
            // property and carry forward the ID of the attached runspace
            newRunspace.IsAttached = true;
            newRunspace.Id = attachedRunspaceId;

            return newRunspace;
        }

        #endregion
    }
}
