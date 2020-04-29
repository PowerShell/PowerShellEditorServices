//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides utility methods for working with PowerShell commands.
    /// </summary>
    internal static class CommandHelpers
    {
        private static readonly HashSet<string> s_nounExclusionList = new HashSet<string>
            {
                // PowerShellGet v2 nouns
                "CredsFromCredentialProvider",
                "DscResource",
                "InstalledModule",
                "InstalledScript",
                "PSRepository",
                "RoleCapability",
                "Script",
                "ScriptFileInfo",

                // PackageManagement nouns
                "Package",
                "PackageProvider",
                "PackageSource",
            };

        // This is used when a noun exists in multiple modules (for example, "Command" is used in Microsoft.PowerShell.Core and also PowerShellGet)
        private static readonly HashSet<string> s_cmdletExclusionList = new HashSet<string>
            {
                // Commands in PowerShellGet with conflicting nouns
                "Find-Command",
                "Find-Module",
                "Install-Module",
                "Publish-Module",
                "Save-Module",
                "Uninstall-Module",
                "Update-Module",
                "Update-ModuleManifest",
            };

        private static readonly ConcurrentDictionary<string, CommandInfo> s_commandInfoCache =
            new ConcurrentDictionary<string, CommandInfo>();

        private static readonly ConcurrentDictionary<string, string> s_synopsisCache =
            new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Gets the CommandInfo instance for a command with a particular name.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="powerShellContext">The PowerShellContext to use for running Get-Command.</param>
        /// <returns>A CommandInfo object with details about the specified command.</returns>
        public static async Task<CommandInfo> GetCommandInfoAsync(
            string commandName,
            PowerShellContextService powerShellContext)
        {
            Validate.IsNotNull(nameof(commandName), commandName);
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            // If we have a CommandInfo cached, return that.
            if (s_commandInfoCache.TryGetValue(commandName, out CommandInfo cmdInfo))
            {
                return cmdInfo;
            }

            // Make sure the command's noun or command's name isn't in the exclusion lists.
            // This is currently necessary to make sure that Get-Command doesn't
            // load PackageManagement or PowerShellGet v2 because they cause
            // a major slowdown in IntelliSense.
            var commandParts = commandName.Split('-');
            if ((commandParts.Length == 2 && s_nounExclusionList.Contains(commandParts[1]))
                    || s_cmdletExclusionList.Contains(commandName))
            {
                return null;
            }

            PSCommand command = new PSCommand();
            command.AddCommand(@"Microsoft.PowerShell.Core\Get-Command");
            command.AddArgument(commandName);
            command.AddParameter("ErrorAction", "Ignore");

            CommandInfo commandInfo = (await powerShellContext.ExecuteCommandAsync<PSObject>(command, sendOutputToHost: false, sendErrorToHost: false).ConfigureAwait(false))
                .Select(o => o.BaseObject)
                .OfType<CommandInfo>()
                .FirstOrDefault();

            // Only cache CmdletInfos since they're exposed in binaries they are likely to not change throughout the session.
            if (commandInfo?.CommandType == CommandTypes.Cmdlet)
            {
                s_commandInfoCache.TryAdd(commandName, commandInfo);
            }

            return commandInfo;
        }

        /// <summary>
        /// Gets the command's "Synopsis" documentation section.
        /// </summary>
        /// <param name="commandInfo">The CommandInfo instance for the command.</param>
        /// <param name="powerShellContext">The PowerShellContext to use for getting command documentation.</param>
        /// <returns></returns>
        public static async Task<string> GetCommandSynopsisAsync(
            CommandInfo commandInfo,
            PowerShellContextService powerShellContext)
        {
            Validate.IsNotNull(nameof(commandInfo), commandInfo);
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            // A small optimization to not run Get-Help on things like DSC resources.
            if (commandInfo.CommandType != CommandTypes.Cmdlet &&
                commandInfo.CommandType != CommandTypes.Function &&
                commandInfo.CommandType != CommandTypes.Filter)
            {
                return string.Empty;
            }

            // If we have a synopsis cached, return that.
            // NOTE: If the user runs Update-Help, it's possible that this synopsis will be out of date.
            // Given the perf increase of doing this, and the simple workaround of restarting the extension,
            // this seems worth it.
            if (s_synopsisCache.TryGetValue(commandInfo.Name, out string synopsis))
            {
                return synopsis;
            }

            PSCommand command = new PSCommand()
                .AddCommand(@"Microsoft.PowerShell.Core\Get-Help")
                // We use .Name here instead of just passing in commandInfo because
                // CommandInfo.ToString() duplicates the Prefix if one exists.
                .AddParameter("Name", commandInfo.Name)
                .AddParameter("ErrorAction", "Ignore");

            var results = await powerShellContext.ExecuteCommandAsync<PSObject>(command, sendOutputToHost: false, sendErrorToHost: false).ConfigureAwait(false);
            PSObject helpObject = results.FirstOrDefault();

            // Extract the synopsis string from the object
            string synopsisString =
                (string)helpObject?.Properties["synopsis"].Value ??
                string.Empty;

            // Only cache cmdlet infos because since they're exposed in binaries, the can never change throughout the session.
            if (commandInfo.CommandType == CommandTypes.Cmdlet)
            {
                s_synopsisCache.TryAdd(commandInfo.Name, synopsisString);
            }

            // Ignore the placeholder value for this field
            if (string.Equals(synopsisString, "SHORT DESCRIPTION", System.StringComparison.CurrentCultureIgnoreCase))
            {
                return string.Empty;
            }

            return synopsisString;
        }
    }
}
