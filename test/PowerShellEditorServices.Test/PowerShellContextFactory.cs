// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Test
{
    /*
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

        public static readonly string BundledModulePath = Path.GetFullPath(
            TestUtilities.NormalizePath("../../../../../module"));

        public static System.Management.Automation.Runspaces.Runspace InitialRunspace;

        public static PowerShellContextService Create(ILogger logger)
        {
            PowerShellContextService powerShellContext = new PowerShellContextService(logger, null, isPSReadLineEnabled: false);

            // We intentionally use `CreateDefault2()` as it loads `Microsoft.PowerShell.Core` only,
            // which is a more minimal and therefore safer state.
            var initialSessionState = InitialSessionState.CreateDefault2();

            // We set the process scope's execution policy (which is really the runspace's scope) to
            // `Bypass` so we can import our bundled modules. This is equivalent in scope to the CLI
            // argument `-ExecutionPolicy Bypass`, which (for instance) the extension passes. Thus
            // we emulate this behavior for consistency such that unit tests can pass in a similar
            // environment.
            if (VersionUtils.IsWindows)
            {
                initialSessionState.ExecutionPolicy = ExecutionPolicy.Bypass;
            }

            HostStartupInfo testHostDetails = new HostStartupInfo(
                "PowerShell Editor Services Test Host",
                "Test.PowerShellEditorServices",
                new Version("1.0.0"),
                null,
                TestProfilePaths,
                new List<string>(),
                new List<string>(),
                initialSessionState,
                null,
                0,
                consoleReplEnabled: false,
                usesLegacyReadLine: false,
                bundledModulePath: BundledModulePath);

            InitialRunspace = PowerShellContextService.CreateTestRunspace(
                    testHostDetails,
                    powerShellContext,
                    new TestPSHostUserInterface(powerShellContext, logger),
                    logger);

            powerShellContext.Initialize(
                TestProfilePaths,
                InitialRunspace,
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
    */
}
