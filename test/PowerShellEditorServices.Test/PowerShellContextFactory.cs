// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Test.Shared;

namespace Microsoft.PowerShell.EditorServices.Test
{
    internal static class PowerShellContextFactory
    {
        // NOTE: These paths are arbitrarily chosen just to verify that the profile paths
        //       can be set to whatever they need to be for the given host.

        public static readonly ProfilePathInfo TestProfilePaths =
            new ProfilePathInfo(
                    Path.GetFullPath(
                        TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Profile/Test.PowerShellEditorServices_profile.ps1")),
                    Path.GetFullPath(
                        TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Profile/ProfileTest.ps1")),
                    Path.GetFullPath(
                        TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Test.PowerShellEditorServices_profile.ps1")),
                    Path.GetFullPath(
                        TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/ProfileTest.ps1")));

        public static System.Management.Automation.Runspaces.Runspace initialRunspace;

        public static PowerShellContextService Create(ILogger logger)
        {
            PowerShellContextService powerShellContext = new PowerShellContextService(logger, null, isPSReadLineEnabled: false);

            HostStartupInfo testHostDetails = new HostStartupInfo(
                "PowerShell Editor Services Test Host",
                "Test.PowerShellEditorServices",
                new Version("1.0.0"),
                null,
                TestProfilePaths,
                new List<string>(),
                new List<string>(),
                // TODO: We want to replace this property with an entire initial session state,
                // which would then also control the process-scoped execution policy.
                PSLanguageMode.FullLanguage,
                null,
                0,
                consoleReplEnabled: false,
                usesLegacyReadLine: false);

            initialRunspace = PowerShellContextService.CreateRunspace(
                    testHostDetails,
                    powerShellContext,
                    new TestPSHostUserInterface(powerShellContext, logger),
                    logger);

            powerShellContext.Initialize(
                TestProfilePaths,
                initialRunspace,
                ownsInitialRunspace: true,
                consoleHost: null);

            return powerShellContext;
        }
    }

    internal class TestPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        public TestPSHostUserInterface(
            PowerShellContextService powerShellContext,
            ILogger logger)
            : base(
                powerShellContext,
                new SimplePSHostRawUserInterface(logger),
                NullLogger.Instance)
        {
        }

        public override void WriteOutput(string outputString, bool includeNewLine, OutputType outputType, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
        }

        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            throw new NotImplementedException();
        }

        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            throw new NotImplementedException();
        }

        protected override Task<string> ReadCommandLineAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult("USER COMMAND");
        }

        protected override void UpdateProgress(long sourceId, ProgressDetails progressDetails)
        {
        }
    }
}
