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
        /// A runspace on the local machine.
        /// </summary>
        Local,

        /// <summary>
        /// A runspace on a different machine.
        /// </summary>
        Remote
    }

    /// <summary>
    /// Specifies the context in which the runspace was encountered.
    /// </summary>
    public enum RunspaceContext
    {
        /// <summary>
        /// The original runspace in a local or remote session.
        /// </summary>
        Original,

        /// <summary>
        /// A runspace in a process that was entered with Enter-PSHostProcess.
        /// </summary>
        EnteredProcess,

        /// <summary>
        /// A runspace that is being debugged with Debug-Runspace.
        /// </summary>
        DebuggedRunspace
    }

    /// <summary>
    /// Provides details about a runspace being used in the current
    /// editing session.
    /// </summary>
    public class RunspaceDetails
    {
        #region Properties

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
        /// Gets the context in which the runspace was encountered.
        /// </summary>
        public RunspaceContext Context { get; private set; }

        /// <summary>
        /// Gets the "connection string" for the runspace, generally the
        /// ComputerName for a remote runspace or the ProcessId of an
        /// "Attach" runspace.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Gets the details of the runspace's session at the time this
        /// RunspaceDetails object was created.
        /// </summary>
        public SessionDetails SessionDetails { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the RunspaceDetails class.
        /// </summary>
        /// <param name="runspace">
        /// The runspace for which this instance contains details.
        /// </param>
        /// <param name="sessionDetails">
        /// The SessionDetails for the runspace.
        /// </param>
        /// <param name="powerShellVersion">
        /// The PowerShellVersionDetails of the runspace.
        /// </param>
        /// <param name="runspaceLocation">
        /// The RunspaceLocation of the runspace.
        /// </param>
        /// <param name="runspaceContext">
        /// The RunspaceContext of the runspace.
        /// </param>
        /// <param name="connectionString">
        /// The connection string of the runspace.
        /// </param>
        public RunspaceDetails(
            Runspace runspace,
            SessionDetails sessionDetails,
            PowerShellVersionDetails powerShellVersion,
            RunspaceLocation runspaceLocation,
            RunspaceContext runspaceContext,
            string connectionString)
        {
            this.Runspace = runspace;
            this.SessionDetails = sessionDetails;
            this.PowerShellVersion = powerShellVersion;
            this.Location = runspaceLocation;
            this.Context = runspaceContext;
            this.ConnectionString = connectionString;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates and populates a new RunspaceDetails instance for the given runspace.
        /// </summary>
        /// <param name="runspace">
        /// The runspace for which details will be gathered.
        /// </param>
        /// <param name="sessionDetails">
        /// The SessionDetails for the runspace.
        /// </param>
        /// <returns>A new RunspaceDetails instance.</returns>
        internal static RunspaceDetails CreateFromRunspace(
            Runspace runspace,
            SessionDetails sessionDetails)
        {
            Validate.IsNotNull(nameof(runspace), runspace);
            Validate.IsNotNull(nameof(sessionDetails), sessionDetails);

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
                    runspace,
                    sessionDetails,
                    versionDetails,
                    runspaceLocation,
                    RunspaceContext.Original,
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
        /// <param name="runspaceContext">
        /// The RunspaceContext of the runspace.
        /// </param>
        /// <param name="sessionDetails">
        /// The SessionDetails for the runspace.
        /// </param>
        /// <returns>
        /// A new RunspaceDetails instance for the attached runspace.
        /// </returns>
        public static RunspaceDetails CreateFromContext(
            RunspaceDetails runspaceDetails,
            RunspaceContext runspaceContext,
            SessionDetails sessionDetails)
        {
            return
                new RunspaceDetails(
                    runspaceDetails.Runspace,
                    sessionDetails,
                    runspaceDetails.PowerShellVersion,
                    runspaceDetails.Location,
                    runspaceContext,
                    runspaceDetails.ConnectionString);
        }

        /// <summary>
        /// Creates a new RunspaceDetails object from a remote
        /// debugging session.
        /// </summary>
        /// <param name="runspaceDetails">
        /// The RunspaceDetails object which the new object based.
        /// </param>
        /// <param name="runspaceLocation">
        /// The RunspaceLocation of the runspace.
        /// </param>
        /// <param name="runspaceContext">
        /// The RunspaceContext of the runspace.
        /// </param>
        /// <param name="sessionDetails">
        /// The SessionDetails for the runspace.
        /// </param>
        /// <returns>
        /// A new RunspaceDetails instance for the attached runspace.
        /// </returns>
        public static RunspaceDetails CreateFromDebugger(
            RunspaceDetails runspaceDetails,
            RunspaceLocation runspaceLocation,
            RunspaceContext runspaceContext,
            SessionDetails sessionDetails)
        {
            // TODO: Get the PowerShellVersion correctly!
            return
                new RunspaceDetails(
                    runspaceDetails.Runspace,
                    sessionDetails,
                    runspaceDetails.PowerShellVersion,
                    runspaceLocation,
                    runspaceContext,
                    runspaceDetails.ConnectionString);
        }

        #endregion
    }

}
