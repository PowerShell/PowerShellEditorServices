//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;

namespace Microsoft.PowerShell.EditorServices
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    /// <summary>
    /// Provides utility methods for working with PowerShell commands.
    /// </summary>
    public class CommandHelpers
    {
        /// <summary>
        /// Gets the CommandInfo instance for a command with a particular name.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="runspace">The Runspace to use for running Get-Command.</param>
        /// <returns>A CommandInfo object with details about the specified command.</returns>
        public static CommandInfo GetCommandInfo(
            string commandName, 
            Runspace runspace)
        {
            CommandInfo commandInfo = null;

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.Runspace = runspace;
                powerShell.AddCommand("Get-Command");
                powerShell.AddArgument(commandName);
                commandInfo = powerShell.Invoke<CommandInfo>().FirstOrDefault();
            }

            return commandInfo;
        }

        /// <summary>
        /// Gets the command's "Synopsis" documentation section.
        /// </summary>
        /// <param name="commandInfo">The CommandInfo instance for the command.</param>
        /// <param name="runspace">The Runspace to use for getting command documentation.</param>
        /// <returns></returns>
        public static string GetCommandSynopsis(
            CommandInfo commandInfo, 
            Runspace runspace)
        {
            string synopsisString = string.Empty;

            PSObject helpObject = null;

            if (commandInfo != null &&
                (commandInfo.CommandType == CommandTypes.Cmdlet ||
                 commandInfo.CommandType == CommandTypes.Function ||
                 commandInfo.CommandType == CommandTypes.Filter))
            {
                using (PowerShell powerShell = PowerShell.Create())
                {
                    powerShell.Runspace = runspace;
                    powerShell.AddCommand("Get-Help");
                    powerShell.AddArgument(commandInfo);
                    helpObject = powerShell.Invoke<PSObject>().FirstOrDefault();
                }

                if (helpObject != null)
                {
                    // Extract the synopsis string from the object
                    synopsisString =
                        (string)helpObject.Properties["synopsis"].Value ??
                        string.Empty;

                    // Ignore the placeholder value for this field
                    if (string.Equals(synopsisString, "SHORT DESCRIPTION", System.StringComparison.InvariantCultureIgnoreCase))
                    {
                        synopsisString = string.Empty;
                    }
                }
            }

            return synopsisString;
        }
    }
}

