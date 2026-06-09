// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getCommand", Direction.ClientToServer)]
    internal interface IGetCommandHandler : IJsonRpcRequestHandler<GetCommandParams, List<PSCommandMessage>> { }

    internal class GetCommandParams : IRequest<List<PSCommandMessage>>
    {
        /// <summary>
        /// An optional name (supports wildcards) to scope the returned commands.
        /// When omitted, all commands are returned.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// An optional module name (supports wildcards) to scope the returned
        /// commands. When omitted, commands from all modules are returned.
        /// </summary>
        public string Module { get; set; }

        /// <summary>
        /// When true, the expensive parameter and parameter-set metadata is not
        /// resolved or returned. Callers that only need command names and modules
        /// (such as the Command Explorer tree) should set this to avoid the large
        /// serialization cost of the full command table.
        /// </summary>
        public bool ExcludeParameters { get; set; }

        /// <summary>
        /// When true, module-less functions and scripts that PowerShell's default
        /// session provides (e.g. cd.., prompt, TabExpansion2) are omitted. These
        /// are interactive shell conveniences and engine plumbing rather than
        /// commands a user authored or imported, so the Command Explorer hides them.
        /// Module-provided commands (including built-in modules) are never affected.
        /// </summary>
        public bool ExcludeDefaultFunctions { get; set; }
    }

    /// <summary>
    /// Describes the message to get the details for a single PowerShell Command
    /// from the current session
    /// </summary>
    internal class PSCommandMessage
    {
        public string Name { get; set; }
        public string ModuleName { get; set; }
        public string ModuleVersion { get; set; }
        public string DefaultParameterSet { get; set; }
        public Dictionary<string, ParameterMetadata> Parameters { get; set; }
        public System.Collections.ObjectModel.ReadOnlyCollection<CommandParameterSetInfo> ParameterSets { get; set; }
    }

    internal class GetCommandHandler : IGetCommandHandler
    {
        private readonly IInternalPowerShellExecutionService _executionService;

        public GetCommandHandler(IInternalPowerShellExecutionService executionService) => _executionService = executionService;

        public async Task<List<PSCommandMessage>> Handle(GetCommandParams request, CancellationToken cancellationToken)
        {
            PSCommand psCommand = new();

            // Executes the following, scoping by name and/or module when provided
            // so we don't serialize the entire command table (which is expensive):
            // Get-Command -CommandType Function,Cmdlet,ExternalScript [-Name <name>] [-Module <module>] | Sort-Object -Property Name
            psCommand
                .AddCommand(@"Microsoft.PowerShell.Core\Get-Command")
                    .AddParameter("CommandType", new[] { "Function", "Cmdlet", "ExternalScript" });

            if (!string.IsNullOrEmpty(request.Name))
            {
                psCommand.AddParameter("Name", request.Name);
            }

            if (!string.IsNullOrEmpty(request.Module))
            {
                psCommand.AddParameter("Module", request.Module);
            }

            // A name or module filter that matches nothing writes a non-terminating
            // error; ignore it so we simply return an empty list instead.
            psCommand.AddParameter("ErrorAction", "Ignore");

            psCommand
                .AddCommand(@"Microsoft.PowerShell.Utility\Sort-Object")
                    .AddParameter("Property", "Name");

            IEnumerable<CommandInfo> result = await _executionService.ExecutePSCommandAsync<CommandInfo>(psCommand, cancellationToken).ConfigureAwait(false);

            List<PSCommandMessage> commandList = new();
            if (result != null)
            {
                foreach (CommandInfo command in result)
                {
                    // Skip commands injected by the editor's terminal integration
                    // (the PSES host's fake PSConsoleHostReadLine and VS Code's
                    // shell-integration helpers); they are implementation details,
                    // not real commands the user authored or imported.
                    if (IsEditorInjectedCommand(command))
                    {
                        continue;
                    }

                    // Optionally drop PowerShell's default-session shell functions
                    // (and the install's profile-resource script), which are
                    // module-less and not meaningful in the command list.
                    if (request.ExcludeDefaultFunctions
                        && IsDefaultSessionFunction(command))
                    {
                        continue;
                    }

                    // When only names/modules are requested, skip resolving the
                    // parameter metadata entirely. Accessing Parameters/ParameterSets
                    // forces PowerShell to compute (and we then serialize) the full
                    // metadata, which is the dominant cost for the whole command table.
                    if (request.ExcludeParameters)
                    {
                        commandList.Add(new PSCommandMessage
                        {
                            Name = command.Name,
                            ModuleName = command.ModuleName,
                            ModuleVersion = command.Version?.ToString()
                        });
                        continue;
                    }

                    // Some info objects have a quicker way to get the DefaultParameterSet. These
                    // are also the most likely to show up so win-win.
                    string defaultParameterSet = null;
                    switch (command)
                    {
                        case CmdletInfo info:
                            defaultParameterSet = info.DefaultParameterSet;
                            break;
                        case FunctionInfo info:
                            defaultParameterSet = info.DefaultParameterSet;
                            break;
                    }

                    if (defaultParameterSet == null)
                    {
                        // Try to get the default ParameterSet if it isn't streamlined (ExternalScriptInfo for example)
                        foreach (CommandParameterSetInfo parameterSetInfo in command.ParameterSets)
                        {
                            if (parameterSetInfo.IsDefault)
                            {
                                defaultParameterSet = parameterSetInfo.Name;
                                break;
                            }
                        }
                    }

                    commandList.Add(new PSCommandMessage
                    {
                        Name = command.Name,
                        ModuleName = command.ModuleName,
                        ModuleVersion = command.Version?.ToString(),
                        Parameters = command.Parameters,
                        ParameterSets = command.ParameterSets,
                        DefaultParameterSet = defaultParameterSet
                    });
                }
            }

            return commandList;
        }

        // Names of helper functions injected by VS Code's terminal shell
        // integration script (shellIntegration.ps1), which the PSES host executes.
        // These are editor plumbing rather than user- or module-provided commands.
        private static readonly HashSet<string> s_shellIntegrationFunctions = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "__VSCode-Escape-Value",
            "Set-MappedKeyHandler",
            "Set-MappedKeyHandlers"
        };

        // Identifies commands injected by the editor's terminal integration that
        // should not be surfaced as real commands.
        private static bool IsEditorInjectedCommand(CommandInfo command)
        {
            if (command.CommandType != CommandTypes.Function)
            {
                return false;
            }

            // The fake global PSConsoleHostReadLine function that the PSES host
            // defines for terminal shell integration (see PsesInternalHost.cs) has
            // no real version, whereas the genuine PSReadLine export always reports
            // a real version, so that export is never matched here.
            if (command.Name == "PSConsoleHostReadLine"
                && (command.Version is null
                    || (command.Version.Major == 0
                        && command.Version.Minor == 0
                        && command.Version.Build <= 0
                        && command.Version.Revision <= 0)))
            {
                return true;
            }

            return s_shellIntegrationFunctions.Contains(command.Name);
        }

        // The names of the functions that PowerShell's default session state
        // provides (cd.., cd\, cd~, Clear-Host, exec, help, oss, Pause, prompt,
        // TabExpansion2). Enumerated once from InitialSessionState so the list stays
        // correct across PowerShell versions rather than being hard-coded.
        private static readonly System.Lazy<HashSet<string>> s_defaultSessionFunctions = new(() =>
            new HashSet<string>(
                InitialSessionState.CreateDefault2().Commands
                    .OfType<SessionStateFunctionEntry>()
                    .Select(static entry => entry.Name),
                System.StringComparer.OrdinalIgnoreCase));

        // Identifies module-less functions and scripts that PowerShell's default
        // session provides — interactive shell conveniences and engine plumbing that
        // aren't meaningful in the command list. Only matches commands with no module,
        // so a module-provided command (including built-in modules) is never affected.
        private static bool IsDefaultSessionFunction(CommandInfo command)
        {
            if (!string.IsNullOrEmpty(command.ModuleName))
            {
                return false;
            }

            // The profile-resource script shipped alongside the PowerShell install
            // (e.g. pwsh.profile.resource.ps1) is install plumbing, not a user script.
            if (command.CommandType == CommandTypes.ExternalScript)
            {
                return command.Name.StartsWith("pwsh.profile.resource", System.StringComparison.OrdinalIgnoreCase);
            }

            return command.CommandType == CommandTypes.Function
                && s_defaultSessionFunctions.Value.Contains(command.Name);
        }
    }
}
