// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using System.Threading;
using SMA = System.Management.Automation;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal class DscBreakpointCapability
    {
        private string[] dscResourceRootPaths = Array.Empty<string>();

        private Dictionary<string, int[]> breakpointsPerFile =
            new Dictionary<string, int[]>();

        public async Task<BreakpointDetails[]> SetLineBreakpointsAsync(
            IInternalPowerShellExecutionService executionService,
            string scriptPath,
            BreakpointDetails[] breakpoints)
        {
            List<BreakpointDetails> resultBreakpointDetails =
                new List<BreakpointDetails>();

            // We always get the latest array of breakpoint line numbers
            // so store that for future use
            if (breakpoints.Length > 0)
            {
                // Set the breakpoints for this scriptPath
                this.breakpointsPerFile[scriptPath] =
                    breakpoints.Select(b => b.LineNumber).ToArray();
            }
            else
            {
                // No more breakpoints for this scriptPath, remove it
                this.breakpointsPerFile.Remove(scriptPath);
            }

            string hashtableString =
                string.Join(
                    ", ",
                    this.breakpointsPerFile
                        .Select(file => $"@{{Path=\"{file.Key}\";Line=@({string.Join(",", file.Value)})}}"));

            // Run Enable-DscDebug as a script because running it as a PSCommand
            // causes an error which states that the Breakpoint parameter has not
            // been passed.
            var dscCommand = new PSCommand().AddScript(
                hashtableString.Length > 0
                    ? $"Enable-DscDebug -Breakpoint {hashtableString}"
                    : "Disable-DscDebug");

            await executionService.ExecutePSCommandAsync(
                dscCommand,
                CancellationToken.None)
                .ConfigureAwait(false);

            // Verify all the breakpoints and return them
            foreach (var breakpoint in breakpoints)
            {
                breakpoint.Verified = true;
            }

            return breakpoints.ToArray();
        }

        public bool IsDscResourcePath(string scriptPath)
        {
            return dscResourceRootPaths.Any(
                dscResourceRootPath =>
                    scriptPath.StartsWith(
                        dscResourceRootPath,
                        StringComparison.CurrentCultureIgnoreCase));
        }

        public static Task<DscBreakpointCapability> GetDscCapabilityAsync(
            ILogger logger,
            IRunspaceInfo currentRunspace,
            PsesInternalHost psesHost,
            CancellationToken cancellationToken)
        {
            // DSC support is enabled only for Windows PowerShell.
            if ((currentRunspace.PowerShellVersionDetails.Version.Major >= 6) &&
                (currentRunspace.RunspaceOrigin != RunspaceOrigin.DebuggedRunspace))
            {
                return null;
            }

            Func<SMA.PowerShell, CancellationToken, DscBreakpointCapability> getDscBreakpointCapabilityFunc = (pwsh, cancellationToken) =>
            {
                var invocationSettings = new PSInvocationSettings
                {
                    AddToHistory = false,
                    ErrorActionPreference = ActionPreference.Stop
                };

                PSModuleInfo dscModule = null;
                try
                {
                    dscModule = pwsh.AddCommand("Import-Module")
                        .AddArgument(@"C:\Program Files\DesiredStateConfiguration\1.0.0.0\Modules\PSDesiredStateConfiguration\PSDesiredStateConfiguration.psd1")
                        .AddParameter("PassThru")
                        .InvokeAndClear<PSModuleInfo>(invocationSettings)
                        .FirstOrDefault();
                }
                catch (RuntimeException e)
                {
                    logger.LogException("Could not load the DSC module!", e);
                }

                if (dscModule == null)
                {
                    logger.LogTrace($"Side-by-side DSC module was not found.");
                    return null;
                }

                logger.LogTrace("Side-by-side DSC module found, gathering DSC resource paths...");

                // The module was loaded, add the breakpoint capability
                var capability = new DscBreakpointCapability();

                pwsh.AddCommand("Microsoft.PowerShell.Utility\\Write-Host")
                    .AddArgument("Gathering DSC resource paths, this may take a while...")
                    .InvokeAndClear(invocationSettings);

                Collection<string> resourcePaths = null;
                try
                {
                    // Get the list of DSC resource paths
                    resourcePaths = pwsh.AddCommand("Get-DscResource")
                        .AddCommand("Select-Object")
                        .AddParameter("ExpandProperty", "ParentPath")
                        .InvokeAndClear<string>(invocationSettings);
                }
                catch (CmdletInvocationException e)
                {
                    logger.LogException("Get-DscResource failed!", e);
                }

                if (resourcePaths == null)
                {
                    logger.LogTrace($"No DSC resources found.");
                    return null;
                }

                capability.dscResourceRootPaths = resourcePaths.ToArray();

                logger.LogTrace($"DSC resources found: {resourcePaths.Count}");

                return capability;
            };

            return psesHost.ExecuteDelegateAsync<DscBreakpointCapability>(
                nameof(getDscBreakpointCapabilityFunc),
                executionOptions: null,
                getDscBreakpointCapabilityFunc,
                cancellationToken);
        }
    }
}
