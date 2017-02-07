//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Management.Automation;
using System.Collections;

namespace Microsoft.PowerShell.EditorServices.Session
{
    /// <summary>
    /// Provides details about the current PowerShell session.
    /// </summary>
    public class SessionDetails
    {
        /// <summary>
        /// Gets the current prompt string.
        /// </summary>
        public string PromptString { get; internal set; }

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

            this.PromptString = innerHashtable["prompt"] as string ?? "PS> ";
            this.ProcessId = (int)innerHashtable["processId"] as int?;
            this.ComputerName = innerHashtable["computerName"] as string;
            this.InstanceId = innerHashtable["instanceId"] as Guid?;

            // Trim the '>' off the end of the prompt string to reduce
            // user confusion about where they can type.
            // TODO: Eventually put this behind a setting, #133
            this.PromptString = this.PromptString.TrimEnd(' ', '>', '\r', '\n');
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
                "@{ 'prompt' = prompt; 'computerName' = $env:ComputerName; 'processId' = $PID; 'instanceId' = $host.InstanceId }");

            return infoCommand;
        }
    }
}
