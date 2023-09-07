﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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
    /// TODO: Handle the `fn ` prefix better.
    /// </summary>
    internal static class CommandHelpers
    {
        public record struct AliasMap(
            Dictionary<string, List<string>> CmdletToAliases,
            Dictionary<string, string> AliasToCmdlets);

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
        /// Gets the actual command behind a fully module qualified command invocation, e.g.
        /// <c>Microsoft.PowerShell.Management\Get-ChildItem</c> will return <c>Get-ChildItem</c>
        /// </summary>
        /// <param name="invocationName">
        /// The potentially module qualified command name at the site of invocation.
        /// </param>
        /// <param name="moduleName">
        /// A reference that will contain the module name if the invocation is module qualified.
        /// </param>
        /// <returns>The actual command name.</returns>
        public static string StripModuleQualification(string invocationName, out ReadOnlyMemory<char> moduleName)
        {
            int slashIndex = invocationName.LastIndexOfAny(new[] { '\\', '/' });
            if (slashIndex is -1)
            {
                moduleName = default;
                return invocationName;
            }

            // If '\' is the last character then it's probably not a module qualified command.
            if (slashIndex == invocationName.Length - 1)
            {
                moduleName = default;
                return invocationName;
            }

            // Storing moduleName as ROMemory saves a string allocation in the common case where it
            // is not needed.
            moduleName = invocationName.AsMemory().Slice(0, slashIndex);
            return invocationName.Substring(slashIndex + 1);
        }

        /// <summary>
        /// Gets the CommandInfo instance for a command with a particular name.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="currentRunspace">The current runspace.</param>
        /// <param name="executionService">The execution service.</param>
        /// <param name="cancellationToken">The token used to cancel this.</param>
        /// <returns>A CommandInfo object with details about the specified command.</returns>
        public static async Task<CommandInfo> GetCommandInfoAsync(
            string commandName,
            IRunspaceInfo currentRunspace,
            IInternalPowerShellExecutionService executionService,
            CancellationToken cancellationToken = default)
        {
            // This mechanism only works in-process
            if (currentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                return null;
            }

            Validate.IsNotNull(nameof(commandName), commandName);
            Validate.IsNotNull(nameof(executionService), executionService);

            // Remove the bucket identifier from symbol references.
            if (commandName.StartsWith("fn "))
            {
                commandName = commandName.Substring(3);
            }

            // If we have a CommandInfo cached, return that.
            if (s_commandInfoCache.TryGetValue(commandName, out CommandInfo cmdInfo))
            {
                return cmdInfo;
            }

            // Make sure the command's noun or command's name isn't in the exclusion lists.
            // This is currently necessary to make sure that Get-Command doesn't
            // load PackageManagement or PowerShellGet v2 because they cause
            // a major slowdown in IntelliSense.
            string[] commandParts = commandName.Split('-');
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
                .ExecutePSCommandAsync<CommandInfo>(command, cancellationToken)
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
        /// <param name="executionService">The execution service to use for getting command documentation.</param>
        /// <param name="cancellationToken">The token used to cancel this.</param>
        /// <returns>The synopsis.</returns>
        public static async Task<string> GetCommandSynopsisAsync(
            CommandInfo commandInfo,
            IInternalPowerShellExecutionService executionService,
            CancellationToken cancellationToken = default)
        {
            Validate.IsNotNull(nameof(commandInfo), commandInfo);
            Validate.IsNotNull(nameof(executionService), executionService);

            // A small optimization to not run Get-Help on things like DSC resources.
            if (commandInfo.CommandType is not CommandTypes.Cmdlet and
                not CommandTypes.Function and
                not CommandTypes.Filter)
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
                .ExecutePSCommandAsync<PSObject>(command, cancellationToken)
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
        /// <param name="cancellationToken"></param>
        public static async Task<AliasMap> GetAliasesAsync(
            IInternalPowerShellExecutionService executionService,
            CancellationToken cancellationToken = default)
        {
            Validate.IsNotNull(nameof(executionService), executionService);

            // Need to execute a PSCommand here as Runspace.SessionStateProxy cannot be used from
            // our PSRL on idle handler.
            IReadOnlyList<CommandInfo> aliases = await executionService.ExecutePSCommandAsync<CommandInfo>(
                new PSCommand()
                    .AddCommand(@"Microsoft.PowerShell.Core\Get-Command")
                    .AddParameter("ListImported", true)
                    .AddParameter("CommandType", CommandTypes.Alias),
                cancellationToken).ConfigureAwait(false);

            foreach (AliasInfo aliasInfo in aliases.Cast<AliasInfo>())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // TODO: When we move to netstandard2.1, we can use another overload which generates
                // static delegates and thus reduces allocations.
                s_cmdletToAliasCache.AddOrUpdate(
                    "fn " + aliasInfo.Definition,
                    (_) => new List<string> { "fn " + aliasInfo.Name },
                    (_, v) => { v.Add("fn " + aliasInfo.Name); return v; });

                s_aliasToCmdletCache.TryAdd("fn " + aliasInfo.Name, "fn " + aliasInfo.Definition);
            }

            return new AliasMap(
                new Dictionary<string, List<string>>(s_cmdletToAliasCache),
                new Dictionary<string, string>(s_aliasToCmdletCache));
        }
    }
}
