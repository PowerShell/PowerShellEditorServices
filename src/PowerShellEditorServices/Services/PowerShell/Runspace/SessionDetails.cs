// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    using System.Management.Automation;

    /// <summary>
    /// Provides details about the current PowerShell session.
    /// </summary>
    internal class SessionDetails
    {
        private const string Property_ComputerName = "computerName";
        private const string Property_ProcessId = "processId";
        private const string Property_InstanceId = "instanceId";

        /// <summary>
        /// Runs a PowerShell command to gather details about the current session.
        /// </summary>
        /// <returns>A data object containing details about the PowerShell session.</returns>
        public static SessionDetails GetFromPowerShell(PowerShell pwsh)
        {
            Hashtable detailsObject = pwsh
                .AddScript(
                    $"@{{ '{Property_ComputerName}' = if ([Environment]::MachineName) {{[Environment]::MachineName}}  else {{'localhost'}}; '{Property_ProcessId}' = $PID; '{Property_InstanceId}' = $host.InstanceId }}",
                    useLocalScope: true)
                .InvokeAndClear<Hashtable>()
                .FirstOrDefault();

            return new SessionDetails(
                (int)detailsObject[Property_ProcessId],
                (string)detailsObject[Property_ComputerName],
                (Guid?)detailsObject[Property_InstanceId]);
        }

        /// <summary>
        /// Creates an instance of SessionDetails using the information
        /// contained in the PSObject which was obtained using the
        /// PSCommand returned by GetDetailsCommand.
        /// </summary>
        public SessionDetails(
            int processId,
            string computerName,
            Guid? instanceId)
        {
            ProcessId = processId;
            ComputerName = computerName;
            InstanceId = instanceId;
        }

        /// <summary>
        /// Gets the process ID of the current process.
        /// </summary>
        public int? ProcessId { get; }

        /// <summary>
        /// Gets the name of the current computer.
        /// </summary>
        public string ComputerName { get; }

        /// <summary>
        /// Gets the current PSHost instance ID.
        /// </summary>
        public Guid? InstanceId { get; }
    }
}
