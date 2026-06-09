// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getModule", Direction.ClientToServer)]
    internal interface IGetModuleHandler : IJsonRpcRequestHandler<GetModuleParams, PSModuleMessage> { }

    internal class GetModuleParams : IRequest<PSModuleMessage>
    {
        /// <summary>
        /// The name of the module to retrieve metadata for.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// An optional specific version of the module. When omitted, the newest
        /// available version is returned.
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// Describes the metadata for a single PowerShell module, used to populate
    /// the Command Explorer's module tooltips.
    /// </summary>
    internal class PSModuleMessage
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Path { get; set; }
        public string Author { get; set; }
        public string CompanyName { get; set; }
        public string ProjectUri { get; set; }
        public string PowerShellVersion { get; set; }
    }

    internal class GetModuleHandler : IGetModuleHandler
    {
        private readonly IInternalPowerShellExecutionService _executionService;

        public GetModuleHandler(IInternalPowerShellExecutionService executionService) => _executionService = executionService;

        public async Task<PSModuleMessage> Handle(GetModuleParams request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                return null;
            }

            // Resolve a module's metadata from the available modules, pinning to a
            // specific version when requested and otherwise taking the newest.
            const string GetModuleScript = @"
                [System.Diagnostics.DebuggerHidden()]
                [CmdletBinding()]
                param (
                    [String]$Name,
                    [String]$Version
                )
                $modules = Microsoft.PowerShell.Core\Get-Module -ListAvailable -Name $Name -ErrorAction Ignore
                if ($Version) {
                    $modules = $modules | Microsoft.PowerShell.Core\Where-Object { $_.Version.ToString() -eq $Version }
                }
                $module = $modules | Microsoft.PowerShell.Utility\Sort-Object Version -Descending | Microsoft.PowerShell.Utility\Select-Object -First 1
                if ($null -eq $module) {
                    return
                }
                [PSCustomObject]@{
                    Name = $module.Name
                    Version = $module.Version.ToString()
                    Description = $module.Description
                    Path = $module.Path
                    Author = $module.Author
                    CompanyName = $module.CompanyName
                    ProjectUri = if ($module.ProjectUri) { $module.ProjectUri.ToString() } else { '' }
                    PowerShellVersion = if ($module.PowerShellVersion) { $module.PowerShellVersion.ToString() } else { '' }
                }
                ";

            PSCommand getModuleCommand = new PSCommand()
                .AddScript(GetModuleScript, useLocalScope: true)
                .AddParameter("Name", request.Name)
                .AddParameter("Version", request.Version);

            IReadOnlyList<PSObject> results = await _executionService.ExecutePSCommandAsync<PSObject>(
                getModuleCommand,
                cancellationToken,
                new PowerShellExecutionOptions
                {
                    ThrowOnError = false
                }).ConfigureAwait(false);

            PSObject result = results is { Count: > 0 } ? results[0] : null;
            if (result is null)
            {
                return null;
            }

            return new PSModuleMessage
            {
                Name = GetPropertyString(result, "Name"),
                Version = GetPropertyString(result, "Version"),
                Description = GetPropertyString(result, "Description"),
                Path = GetPropertyString(result, "Path"),
                Author = GetPropertyString(result, "Author"),
                CompanyName = GetPropertyString(result, "CompanyName"),
                ProjectUri = GetPropertyString(result, "ProjectUri"),
                PowerShellVersion = GetPropertyString(result, "PowerShellVersion")
            };
        }

        private static string GetPropertyString(PSObject psObject, string propertyName)
            => psObject.Properties[propertyName]?.Value as string ?? string.Empty;
    }
}
