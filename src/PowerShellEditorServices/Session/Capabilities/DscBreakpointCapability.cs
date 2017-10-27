﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Session.Capabilities
{
    using Microsoft.PowerShell.EditorServices.Utility;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Management.Automation;

    internal class DscBreakpointCapability : IRunspaceCapability
    {
        private string[] dscResourceRootPaths = new string[0];

        private Dictionary<string, int[]> breakpointsPerFile =
            new Dictionary<string, int[]>();

        public async Task<List<BreakpointDetails>> SetLineBreakpoints(
            PowerShellContext powerShellContext,
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
            await powerShellContext.ExecuteScriptString(
                hashtableString.Length > 0
                    ? $"Enable-DscDebug -Breakpoint {hashtableString}"
                    : "Disable-DscDebug",
                false,
                false);

            // Verify all the breakpoints and return them
            foreach (var breakpoint in breakpoints)
            {
                breakpoint.Verified = true;
            }

            return breakpoints.ToList();
        }

        public bool IsDscResourcePath(string scriptPath)
        {
            return dscResourceRootPaths.Any(
                dscResourceRootPath =>
                    scriptPath.StartsWith(
                        dscResourceRootPath,
                        StringComparison.CurrentCultureIgnoreCase));
        }

        public static DscBreakpointCapability CheckForCapability(
            RunspaceDetails runspaceDetails,
            PowerShellContext powerShellContext,
            ILogger logger)
        {
            DscBreakpointCapability capability = null;

            // DSC support is enabled only for Windows PowerShell.
            if ((runspaceDetails.PowerShellVersion.Version.Major < 6) &&
                (runspaceDetails.Context != RunspaceContext.DebuggedRunspace))
            {
                using (PowerShell powerShell = PowerShell.Create())
                {
                    powerShell.Runspace = runspaceDetails.Runspace;

                    // Attempt to import the updated DSC module
                    powerShell.AddCommand("Import-Module");
                    powerShell.AddArgument(@"${env:ProgramFiles}\DesiredStateConfiguration\1.0.0.0\Modules\PSDesiredStateConfiguration\PSDesiredStateConfiguration.psd1");
                    powerShell.AddParameter("PassThru");
                    powerShell.AddParameter("ErrorAction", "Ignore");

                    PSObject moduleInfo = null;

                    try
                    {
                        moduleInfo = powerShell.Invoke().FirstOrDefault();
                    }
                    catch (CmdletInvocationException e)
                    {
                        logger.WriteException("Could not load the DSC module!", e);
                    }

                    if (moduleInfo != null)
                    {
                        logger.Write(LogLevel.Verbose, "Side-by-side DSC module found, gathering DSC resource paths...");

                        // The module was loaded, add the breakpoint capability
                        capability = new DscBreakpointCapability();
                        runspaceDetails.AddCapability(capability);

                        powerShell.Commands.Clear();
                        powerShell.AddScript("Write-Host \"Gathering DSC resource paths, this may take a while...\"");
                        powerShell.Invoke();

                        // Get the list of DSC resource paths
                        powerShell.Commands.Clear();
                        powerShell.AddCommand("Get-DscResource");
                        powerShell.AddCommand("Select-Object");
                        powerShell.AddParameter("ExpandProperty", "ParentPath");

                        Collection<PSObject> resourcePaths = null;

                        try
                        {
                            resourcePaths = powerShell.Invoke();
                        }
                        catch (CmdletInvocationException e)
                        {
                            logger.WriteException("Get-DscResource failed!", e);
                        }

                        if (resourcePaths != null)
                        {
                            capability.dscResourceRootPaths =
                                resourcePaths
                                    .Select(o => (string)o.BaseObject)
                                    .ToArray();

                            logger.Write(LogLevel.Verbose, $"DSC resources found: {resourcePaths.Count}");
                        }
                        else
                        {
                            logger.Write(LogLevel.Verbose, $"No DSC resources found.");
                        }
                    }
                    else
                    {
                        logger.Write(LogLevel.Verbose, $"Side-by-side DSC module was not found.");
                    }
                }
            }

            return capability;
        }
    }
}