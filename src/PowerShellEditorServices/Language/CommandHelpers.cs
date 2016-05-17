//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides utility methods for working with PowerShell commands.
    /// </summary>
    public class CommandHelpers
    {
        /// <summary>
        /// Gets the CommandInfo instance for a command with a particular name.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="powerShellContext">The PowerShellContext to use for running Get-Command.</param>
        /// <returns>A CommandInfo object with details about the specified command.</returns>
        public static async Task<CommandInfo> GetCommandInfo(
            string commandName, 
            PowerShellContext powerShellContext)
        {
            PSCommand command = new PSCommand();
            command.AddCommand(@"Microsoft.PowerShell.Core\Get-Command");
            command.AddArgument(commandName);

            var results = await powerShellContext.ExecuteCommand<CommandInfo>(command, false, false);
            return results.FirstOrDefault();
        }

        /// <summary>
        /// Gets the command's "Synopsis" documentation section.
        /// </summary>
        /// <param name="commandInfo">The CommandInfo instance for the command.</param>
        /// <param name="powerShellContext">The PowerShellContext to use for getting command documentation.</param>
        /// <returns></returns>
        public static async Task<string> GetCommandSynopsis(
            CommandInfo commandInfo, 
            PowerShellContext powerShellContext)
        {
            string synopsisString = string.Empty;

            PSObject helpObject = null;

            if (commandInfo != null &&
                (commandInfo.CommandType == CommandTypes.Cmdlet ||
                 commandInfo.CommandType == CommandTypes.Function ||
                 commandInfo.CommandType == CommandTypes.Filter))
            {
                PSCommand command = new PSCommand();
                command.AddCommand(@"Microsoft.PowerShell.Core\Get-Help");
                command.AddArgument(commandInfo);

                var results = await powerShellContext.ExecuteCommand<PSObject>(command, false, false);
                helpObject = results.FirstOrDefault();

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

