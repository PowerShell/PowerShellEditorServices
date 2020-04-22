//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using System.Collections;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides details about the current PowerShell session.
    /// </summary>
    internal class SessionDetails
    {
        /// <summary>
        /// Gets the process ID of the current process.
        /// </summary>
        public int? ProcessId { get; private set; }

        /// <summary>
        /// Gets the name of the current computer.
        /// </summary>
        public string ComputerName { get; private set; }

        /// <summary>
        /// Gets the current PSHost instance ID.
        /// </summary>
        public Guid? InstanceId { get; private set; }

        /// <summary>
        /// Creates an instance of SessionDetails using the information
        /// contained in the PSObject which was obtained using the
        /// PSCommand returned by GetDetailsCommand.
        /// </summary>
        /// <param name="detailsObject"></param>
        public SessionDetails(PSObject detailsObject)
        {
            Validate.IsNotNull(nameof(detailsObject), detailsObject);

            Hashtable innerHashtable = detailsObject.BaseObject as Hashtable;

            this.ProcessId = (int)innerHashtable["processId"] as int?;
            this.ComputerName = innerHashtable["computerName"] as string;
            this.InstanceId = innerHashtable["instanceId"] as Guid?;
        }

        /// <summary>
        /// Gets the PSCommand that gathers details from the
        /// current session.
        /// </summary>
        /// <returns>A PSCommand used to gather session details.</returns>
        public static PSCommand GetDetailsCommand()
        {
            PSCommand infoCommand = new PSCommand();
            infoCommand.AddScript(
                "@{ 'computerName' = if ([Environment]::MachineName) {[Environment]::MachineName}  else {'localhost'}; 'processId' = $PID; 'instanceId' = $host.InstanceId }",
                useLocalScope: true);

            return infoCommand;
        }
    }
}
