// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Xunit.Sdk;

namespace PowerShellEditorServices.Test.E2E;

/// <summary>
/// The shared skip reason used by the discovery-time Windows PowerShell skip
/// attributes for the end-to-end tests.
/// </summary>
/// <remarks>
/// This is a runner-image regression, not a PSES code change: re-running a
/// commit that predates all recent PRs (and previously passed) reproduces the
/// same hang on the current windows-latest image, while macOS and Linux stay
/// green. The wedge is in the in-box Windows PowerShell server's startup, so it
/// affects both the debug adapter and language server end-to-end suites.
/// </remarks>
internal static class WindowsPowerShellServerStartupSkip
{
    public const string Reason = "The in-box Windows PowerShell server can wedge during startup on the current windows-latest runner image (a runner-image regression, not our code); see https://github.com/PowerShell/PowerShellEditorServices/issues/2323.";
}

/// <summary>
/// A <see cref="SkippableFactAttribute"/> that additionally skips the test at
/// <em>discovery</em> time when running under in-box Windows PowerShell.
/// </summary>
/// <remarks>
/// A runtime <see cref="Skip.If(bool, string)"/> in the test body cannot prevent
/// the per-test <c>IAsyncLifetime.InitializeAsync</c> from running first, because
/// xUnit invokes the lifetime setup (which starts the PSES server) before the
/// method body. When the hang occurs during that setup, a body-level skip is never
/// reached. Setting <see cref="FactAttribute.Skip"/> here makes xUnit treat the
/// test as statically skipped, so it never instantiates the test class or runs
/// <c>InitializeAsync</c>. The <see cref="SkippableFactAttribute"/> discoverer is
/// retained so runtime <see cref="Skip"/> calls (e.g. for Constrained Language
/// Mode) still work when the test is not skipped at discovery time.
/// <para>
/// Caveat: xUnit still creates an <see cref="IClassFixture{TFixture}"/> even when
/// every test method in the class is skipped at discovery time, so a fixture that
/// starts the server in its own <c>InitializeAsync</c> (e.g. <c>LSPTestsFixture</c>)
/// must additionally guard against starting it under Windows PowerShell.
/// </para>
/// </remarks>
[XunitTestCaseDiscoverer("Xunit.Sdk.SkippableFactDiscoverer", "Xunit.SkippableFact")]
public sealed class SkippableFactOnWindowsPowerShellAttribute : SkippableFactAttribute
{
    public SkippableFactOnWindowsPowerShellAttribute()
    {
        if (PsesStdioLanguageServerProcessHost.IsWindowsPowerShell)
        {
            Skip = WindowsPowerShellServerStartupSkip.Reason;
        }
    }
}
