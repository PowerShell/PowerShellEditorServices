// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal class DscBreakpointCapability
    {
        private static bool? isDscInstalled;
        private string[] dscResourceRootPaths = Array.Empty<string>();
        private readonly Dictionary<string, int[]> breakpointsPerFile = new();

        public async Task<IReadOnlyList<BreakpointDetails>> SetLineBreakpointsAsync(
            IInternalPowerShellExecutionService executionService,
            string scriptPath,
            IReadOnlyList<BreakpointDetails> breakpoints)
        {
            // We always get the latest array of breakpoint line numbers
            // so store that for future use
            int[] lineNumbers = breakpoints.Select(b => b.LineNumber).ToArray();
            if (lineNumbers.Length > 0)
            {
                // Set the breakpoints for this scriptPath
                breakpointsPerFile[scriptPath] = lineNumbers;
            }
            else
            {
                // No more breakpoints for this scriptPath, remove it
                breakpointsPerFile.Remove(scriptPath);
            }

            string hashtableString =
                string.Join(
                    ", ",
                    breakpointsPerFile
                        .Select(file => $"@{{Path=\"{file.Key}\";Line=@({string.Join(",", file.Value)})}}"));

            // Run Enable-DscDebug as a script because running it as a PSCommand
            // causes an error which states that the Breakpoint parameter has not
            // been passed.
            PSCommand dscCommand = new PSCommand().AddScript(
                hashtableString.Length > 0
                    ? $"Enable-DscDebug -Breakpoint {hashtableString}"
                    : "Disable-DscDebug");

            await executionService.ExecutePSCommandAsync(
                dscCommand,
                CancellationToken.None)
                .ConfigureAwait(false);

            // Verify all the breakpoints and return them
            foreach (BreakpointDetails breakpoint in breakpoints)
            {
                breakpoint.Verified = true;
            }

            return breakpoints;
        }

        public bool IsDscResourcePath(string scriptPath)
        {
            return dscResourceRootPaths.Any(
                dscResourceRootPath =>
                    scriptPath.StartsWith(
                        dscResourceRootPath,
                        StringComparison.CurrentCultureIgnoreCase));
        }

        public static async Task<DscBreakpointCapability> GetDscCapabilityAsync(
            ILogger logger,
            IRunspaceInfo currentRunspace,
            PsesInternalHost psesHost)
        {
            // DSC support is enabled only for Windows PowerShell.
            if ((currentRunspace.PowerShellVersionDetails.Version.Major >= 6) &&
                (currentRunspace.RunspaceOrigin != RunspaceOrigin.DebuggedRunspace))
            {
                return null;
            }

            if (!isDscInstalled.HasValue)
            {
                PSCommand psCommand = new PSCommand()
                    .AddCommand("Import-Module")
                    .AddArgument(@"C:\Program Files\DesiredStateConfiguration\1.0.0.0\Modules\PSDesiredStateConfiguration\PSDesiredStateConfiguration.psd1")
                    .AddParameter("PassThru")
                    .AddParameter("ErrorAction", ActionPreference.Ignore);

                IReadOnlyList<PSModuleInfo> dscModule =
                    await psesHost.ExecutePSCommandAsync<PSModuleInfo>(
                        psCommand,
                        CancellationToken.None,
                        new PowerShellExecutionOptions { ThrowOnError = false }).ConfigureAwait(false);

                isDscInstalled = dscModule.Count > 0;
                logger.LogTrace("Side-by-side DSC module found: " + isDscInstalled.Value);
            }

            if (isDscInstalled.Value)
            {
                PSCommand psCommand = new PSCommand()
                    .AddCommand("Get-DscResource")
                    .AddCommand("Select-Object")
                    .AddParameter("ExpandProperty", "ParentPath");

                IReadOnlyList<string> resourcePaths =
                    await psesHost.ExecutePSCommandAsync<string>(
                        psCommand,
                        CancellationToken.None,
                        new PowerShellExecutionOptions { ThrowOnError = false }
                    ).ConfigureAwait(false);

                logger.LogTrace($"DSC resources found: {resourcePaths.Count}");
                return new DscBreakpointCapability
                {
                    dscResourceRootPaths = resourcePaths.ToArray()
                };
            }

            return null;
        }
    }
}
