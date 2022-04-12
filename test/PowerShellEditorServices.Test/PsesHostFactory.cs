// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Test
{
    internal static class PsesHostFactory
    {
        // NOTE: These paths are arbitrarily chosen just to verify that the profile paths can be set
        // to whatever they need to be for the given host.

        public static readonly ProfilePathInfo TestProfilePaths = new(
            Path.GetFullPath(TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Profile/Test.PowerShellEditorServices_profile.ps1")),
            Path.GetFullPath(TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Profile/ProfileTest.ps1")),
            Path.GetFullPath(TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Test.PowerShellEditorServices_profile.ps1")),
            Path.GetFullPath(TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/ProfileTest.ps1")));

        public static readonly string BundledModulePath = Path.GetFullPath(TestUtilities.NormalizePath("../../../../../module"));

        public static PsesInternalHost Create(ILoggerFactory loggerFactory)
        {
            // We intentionally use `CreateDefault2()` as it loads `Microsoft.PowerShell.Core` only,
            // which is a more minimal and therefore safer state.
            InitialSessionState initialSessionState = InitialSessionState.CreateDefault2();

            // We set the process scope's execution policy (which is really the runspace's scope) to
            // `Bypass` so we can import our bundled modules. This is equivalent in scope to the CLI
            // argument `-ExecutionPolicy Bypass`, which (for instance) the extension passes. Thus
            // we emulate this behavior for consistency such that unit tests can pass in a similar
            // environment.
            if (VersionUtils.IsWindows)
            {
                initialSessionState.ExecutionPolicy = ExecutionPolicy.Bypass;
            }

            HostStartupInfo testHostDetails = new(
                name: "PowerShell Editor Services Test Host",
                profileId: "Test.PowerShellEditorServices",
                version: new Version("1.0.0"),
                psHost: new NullPSHost(),
                profilePaths: TestProfilePaths,
                featureFlags: Array.Empty<string>(),
                additionalModules: Array.Empty<string>(),
                initialSessionState: initialSessionState,
                logPath: null,
                logLevel: (int)LogLevel.None,
                consoleReplEnabled: false,
                usesLegacyReadLine: false,
                bundledModulePath: BundledModulePath);

            PsesInternalHost psesHost = new(loggerFactory, null, testHostDetails);

            // NOTE: Because this is used by constructors it can't use await.
            if (psesHost.TryStartAsync(new HostStartOptions { LoadProfiles = false }, CancellationToken.None).GetAwaiter().GetResult())
            {
                return psesHost;
            }

            throw new Exception("Host didn't start!");
        }
    }

    internal class NullPSHost : PSHost
    {
        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override string Name => nameof(NullPSHost);
        public override PSHostUserInterface UI { get; } = new NullPSHostUI();
        public override Version Version { get; } = new Version(1, 0, 0);
        public override void EnterNestedPrompt() { /* Do nothing */ }
        public override void ExitNestedPrompt() { /* Do nothing */ }
        public override void NotifyBeginApplication() { /* Do nothing */ }
        public override void NotifyEndApplication() { /* Do nothing */ }
        public override void SetShouldExit(int exitCode) { /* Do nothing */ }
    }
}
