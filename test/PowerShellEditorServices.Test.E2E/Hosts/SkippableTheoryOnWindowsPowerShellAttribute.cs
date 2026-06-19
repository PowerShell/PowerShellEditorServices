// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Xunit.Sdk;

namespace PowerShellEditorServices.Test.E2E;

/// <summary>
/// A <see cref="SkippableTheoryAttribute"/> that additionally skips the theory at
/// <em>discovery</em> time when running under in-box Windows PowerShell. See
/// <see cref="SkippableFactOnWindowsPowerShellAttribute"/> for why the skip must
/// happen at discovery time rather than via an in-body <see cref="Skip"/> call.
/// </summary>
[XunitTestCaseDiscoverer("Xunit.Sdk.SkippableTheoryDiscoverer", "Xunit.SkippableFact")]
public sealed class SkippableTheoryOnWindowsPowerShellAttribute : SkippableTheoryAttribute
{
    public SkippableTheoryOnWindowsPowerShellAttribute()
    {
        if (PsesStdioLanguageServerProcessHost.IsWindowsPowerShell)
        {
            Skip = WindowsPowerShellDebugAdapterSkip.Reason;
        }
    }
}
