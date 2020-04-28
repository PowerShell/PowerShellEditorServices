//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Management.Automation.Runspaces;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Specifies the possible types of a runspace.
    /// </summary>
    internal enum RunspaceLocation
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
    internal enum RunspaceContext
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
    internal class RunspaceDetails
    {
        #region Private Fields

        private Dictionary<Type, IRunspaceCapability> capabilities =
            new Dictionary<Type, IRunspaceCapability>();

        #endregion

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

        internal void AddCapability<TCapability>(TCapability capability)
            where TCapability : IRunspaceCapability
        {
            this.capabilities.Add(typeof(TCapability), capability);
        }

        internal TCapability GetCapability<TCapability>()
            where TCapability : IRunspaceCapability
        {
            TCapability capability = default(TCapability);
            this.TryGetCapability<TCapability>(out capability);
            return capability;
        }

        internal bool TryGetCapability<TCapability>(out TCapability capability)
            where TCapability : IRunspaceCapability
        {
            IRunspaceCapability capabilityAsInterface = default(TCapability);
            if (this.capabilities.TryGetValue(typeof(TCapability), out capabilityAsInterface))
            {
                capability = (TCapability)capabilityAsInterface;
                return true;
            }

            capability = default(TCapability);
            return false;
        }

        internal bool HasCapability<TCapability>()
        {
            return this.capabilities.ContainsKey(typeof(TCapability));
        }

        /// <summary>
        /// Creates and populates a new RunspaceDetails instance for the given runspace.
        /// </summary>
        /// <param name="runspace">
        /// The runspace for which details will be gathered.
        /// </param>
        /// <param name="sessionDetails">
        /// The SessionDetails for the runspace.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <returns>A new RunspaceDetails instance.</returns>
        internal static RunspaceDetails CreateFromRunspace(
            Runspace runspace,
            SessionDetails sessionDetails,
            ILogger logger)
        {
            Validate.IsNotNull(nameof(runspace), runspace);
            Validate.IsNotNull(nameof(sessionDetails), sessionDetails);

            var runspaceLocation = RunspaceLocation.Local;
            var runspaceContext = RunspaceContext.Original;
            var versionDetails = PowerShellVersionDetails.GetVersionDetails(runspace, logger);

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
                        runspaceContext = RunspaceContext.EnteredProcess;
                    }
                }
                catch (RuntimeBinderException)
                {
                    // ProcessId property isn't on the object, move on.
                }

                // Grab the $host.name which will tell us if we're in a PSRP session or not
                string hostName =
                        PowerShellContextService.ExecuteScriptAndGetItem<string>(
                            "$Host.Name",
                            runspace,
                            defaultValue: string.Empty,
                            useLocalScope: true);

                // hostname is 'ServerRemoteHost' when the user enters a session.
                // ex. Enter-PSSession
                // Attaching to process currently needs to be marked as a local session
                // so we skip this if block if the runspace is from Enter-PSHostProcess
                if (hostName.Equals("ServerRemoteHost", StringComparison.Ordinal)
                    && runspace.OriginalConnectionInfo?.GetType().ToString() != "System.Management.Automation.Runspaces.NamedPipeConnectionInfo")
                {
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
                    runspaceContext,
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
