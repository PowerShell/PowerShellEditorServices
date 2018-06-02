//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Generic;
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
        private static HashSet<string> NounBlackList =
            new HashSet<string>
            {
                "Module",
                "Script",
                "Package",
                "PackageProvider",
                "PackageSource",
                "InstalledModule",
                "InstalledScript",
                "ScriptFileInfo",
                "PSRepository"
            };

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
            Validate.IsNotNull(nameof(commandName), commandName);

            // Make sure the command's noun isn't blacklisted.  This is
            // currently necessary to make sure that Get-Command doesn't
            // load PackageManagement or PowerShellGet because they cause
            // a major slowdown in IntelliSense.
            var commandParts = commandName.Split('-');
            if (commandParts.Length == 2 && NounBlackList.Contains(commandParts[1]))
            {
                return null;
            }

            // Keeping this commented out for now.  It would be faster, but it doesn't automatically
            // import modules. This may actually be preferred, but it's a big change that needs to
            // be discussed more.
            // if (powerShellContext.CurrentRunspace.Location == Session.RunspaceLocation.Local)
            // {
            //     return await powerShellContext.UsingEngine<CommandInfo>(
            //         engine =>
            //         {
            //             return engine
            //                 .SessionState
            //                 .InvokeCommand
            //                 .GetCommand(commandName, CommandTypes.All);
            //         });
            // }

            PSCommand command = new PSCommand();
            command.AddCommand(@"Microsoft.PowerShell.Core\Get-Command");
            command.AddArgument(commandName);
            command.AddParameter("ErrorAction", "Ignore");

            return
                (await powerShellContext
                    .ExecuteCommand<PSObject>(command, false, false))
                    .Select(o => o.BaseObject)
                    .OfType<CommandInfo>()
                    .FirstOrDefault();
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
                command.AddParameter("ErrorAction", "Ignore");

                var results = await powerShellContext.ExecuteCommand<PSObject>(command, false, false);
                helpObject = results.FirstOrDefault();

                if (helpObject != null)
                {
                    // Extract the synopsis string from the object
                    synopsisString =
                        (string)helpObject.Properties["synopsis"].Value ??
                        string.Empty;

                    // Ignore the placeholder value for this field
                    if (string.Equals(synopsisString, "SHORT DESCRIPTION", System.StringComparison.CurrentCultureIgnoreCase))
                    {
                        synopsisString = string.Empty;
                    }
                }
            }

            return synopsisString;
        }
    }
}

