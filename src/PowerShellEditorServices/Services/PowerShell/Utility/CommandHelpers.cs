// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    /// <summary>
    /// Provides utility methods for working with PowerShell commands.
    /// </summary>
    internal static class CommandHelpers
    {
        private static readonly HashSet<string> s_nounExclusionList = new()
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
        private static readonly HashSet<string> s_cmdletExclusionList = new()
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

        private static readonly ConcurrentDictionary<string, CommandInfo> s_commandInfoCache = new();
        private static readonly ConcurrentDictionary<string, string> s_synopsisCache = new();
        internal static readonly ConcurrentDictionary<string, List<string>> s_cmdletToAliasCache = new(System.StringComparer.OrdinalIgnoreCase);
        internal static readonly ConcurrentDictionary<string, string> s_aliasToCmdletCache = new(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the CommandInfo instance for a command with a particular name.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="currentRunspace">The current runspace.</param>
        /// <param name="executionService">The execution service.</param>
        /// <returns>A CommandInfo object with details about the specified command.</returns>
        public static async Task<CommandInfo> GetCommandInfoAsync(
            string commandName,
            IRunspaceInfo currentRunspace,
            IInternalPowerShellExecutionService executionService)
        {
            // This mechanism only works in-process
            if (currentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                return null;
            }

            Validate.IsNotNull(nameof(commandName), commandName);
            Validate.IsNotNull(nameof(executionService), executionService);

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

            PSCommand command = new PSCommand()
                .AddCommand(@"Microsoft.PowerShell.Core\Get-Command")
                .AddArgument(commandName)
                .AddParameter("ErrorAction", "Ignore");

            IReadOnlyList<CommandInfo> results = await executionService
                .ExecutePSCommandAsync<CommandInfo>(command, CancellationToken.None)
                .ConfigureAwait(false);

            CommandInfo commandInfo = results.Count > 0 ? results[0] : null;

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
        /// <param name="executionService">The exeuction service to use for getting command documentation.</param>
        /// <returns>The synopsis.</returns>
        public static async Task<string> GetCommandSynopsisAsync(
            CommandInfo commandInfo,
            IInternalPowerShellExecutionService executionService)
        {
            Validate.IsNotNull(nameof(commandInfo), commandInfo);
            Validate.IsNotNull(nameof(executionService), executionService);

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
                .AddParameter("Online", false)
                .AddParameter("ErrorAction", "Ignore");

            IReadOnlyList<PSObject> results = await executionService
                .ExecutePSCommandAsync<PSObject>(command, CancellationToken.None)
                .ConfigureAwait(false);

            // Extract the synopsis string from the object
            PSObject helpObject = results.Count > 0 ? results[0] : null;
            string synopsisString = (string)helpObject?.Properties["synopsis"].Value ?? string.Empty;

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

        /// <summary>
        /// Gets all aliases found in the runspace
        /// </summary>
        /// <param name="executionService"></param>
        public static async Task<(Dictionary<string, List<string>>, Dictionary<string, string>)> GetAliasesAsync(IInternalPowerShellExecutionService executionService)
        {
            Validate.IsNotNull(nameof(executionService), executionService);

            IEnumerable<CommandInfo> aliases = await executionService.ExecuteDelegateAsync<IEnumerable<CommandInfo>>(
                nameof(GetAliasesAsync),
                executionOptions: null,
                (pwsh, _) =>
                {
                    CommandInvocationIntrinsics invokeCommand = pwsh.Runspace.SessionStateProxy.InvokeCommand;
                    return invokeCommand.GetCommands("*", CommandTypes.Alias, nameIsPattern: true);
                },
                CancellationToken.None).ConfigureAwait(false);

            foreach (AliasInfo aliasInfo in aliases)
            {
                // TODO: When we move to netstandard2.1, we can use another overload which generates
                // static delegates and thus reduces allocations.
                s_cmdletToAliasCache.AddOrUpdate(
                    aliasInfo.Definition,
                    (_) => new List<string> { aliasInfo.Name },
                    (_, v) => { v.Add(aliasInfo.Name); return v; });

                s_aliasToCmdletCache.TryAdd(aliasInfo.Name, aliasInfo.Definition);
            }

            return (new Dictionary<string, List<string>>(s_cmdletToAliasCache),
                new Dictionary<string, string>(s_aliasToCmdletCache));
        }
    }
}
