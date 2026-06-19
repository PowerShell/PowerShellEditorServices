// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Xunit.Sdk;

namespace PowerShellEditorServices.Test.E2E;

/// <summary>
/// The shared skip reason used by the discovery-time Windows PowerShell skip
/// attributes for the debug adapter end-to-end tests.
/// </summary>
internal static class WindowsPowerShellDebugAdapterSkip
{
    public const string Reason = "The debug adapter can wedge during startup on in-box Windows PowerShell since the 20260614 runner image; see https://github.com/PowerShell/PowerShellEditorServices/issues/2323.";
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
/// </remarks>
[XunitTestCaseDiscoverer("Xunit.Sdk.SkippableFactDiscoverer", "Xunit.SkippableFact")]
public sealed class SkippableFactOnWindowsPowerShellAttribute : SkippableFactAttribute
{
    public SkippableFactOnWindowsPowerShellAttribute()
    {
        if (PsesStdioLanguageServerProcessHost.IsWindowsPowerShell)
        {
            Skip = WindowsPowerShellDebugAdapterSkip.Reason;
        }
    }
}
