//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Concurrent;
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
        private static readonly ConcurrentDictionary<string, bool> NounExclusionList =
            new ConcurrentDictionary<string, bool>();

        static CommandHelpers()
        {
            NounExclusionList.TryAdd("Module", true);
            NounExclusionList.TryAdd("Script", true);
            NounExclusionList.TryAdd("Package", true);
            NounExclusionList.TryAdd("PackageProvider", true);
            NounExclusionList.TryAdd("PackageSource", true);
            NounExclusionList.TryAdd("InstalledModule", true);
            NounExclusionList.TryAdd("InstalledScript", true);
            NounExclusionList.TryAdd("ScriptFileInfo", true);
            NounExclusionList.TryAdd("PSRepository", true);
        }

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

            // Make sure the command's noun isn't blacklisted.  This is
            // currently necessary to make sure that Get-Command doesn't
            // load PackageManagement or PowerShellGet because they cause
            // a major slowdown in IntelliSense.
            var commandParts = commandName.Split('-');
            if (commandParts.Length == 2 && NounExclusionList.ContainsKey(commandParts[1]))
            {
                return null;
            }

            PSCommand command = new PSCommand();
            command.AddCommand(@"Microsoft.PowerShell.Core\Get-Command");
            command.AddArgument(commandName);
            command.AddParameter("ErrorAction", "Ignore");

            return (await powerShellContext.ExecuteCommandAsync<PSObject>(command, sendOutputToHost: false, sendErrorToHost: false).ConfigureAwait(false))
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

            PSCommand command = new PSCommand()
                .AddCommand(@"Microsoft.PowerShell.Core\Get-Help")
                // We use .Name here instead of just passing in commandInfo because
                // CommandInfo.ToString() duplicates the Prefix if one exists.
                .AddArgument(commandInfo.Name)
                .AddParameter("ErrorAction", "Ignore");

            var results = await powerShellContext.ExecuteCommandAsync<PSObject>(command, sendOutputToHost: false, sendErrorToHost: false).ConfigureAwait(false);
            PSObject helpObject = results.FirstOrDefault();

            // Extract the synopsis string from the object
            string synopsisString =
                (string)helpObject?.Properties["synopsis"].Value ??
                string.Empty;

            // Ignore the placeholder value for this field
            if (string.Equals(synopsisString, "SHORT DESCRIPTION", System.StringComparison.CurrentCultureIgnoreCase))
            {
                return string.Empty;
            }

            return synopsisString;
        }
    }
}
