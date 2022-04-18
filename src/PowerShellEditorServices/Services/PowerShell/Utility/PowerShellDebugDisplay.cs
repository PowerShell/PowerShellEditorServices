// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if DEBUG
using System.Diagnostics;
using SMA = System.Management.Automation;

[assembly: DebuggerDisplay("{Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility.PowerShellDebugDisplay.ToDebuggerString(this)}", Target = typeof(SMA.PowerShell))]
[assembly: DebuggerDisplay("{Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility.PSCommandDebugDisplay.ToDebuggerString(this)}", Target = typeof(SMA.PSCommand))]

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;

internal static class PowerShellDebugDisplay
{
    public static string ToDebuggerString(SMA.PowerShell pwsh)
    {
        if (pwsh.Commands.Commands.Count == 0)
        {
            return "{}";
        }

        return $"{{{pwsh.Commands.Commands[0].CommandText}}}";
    }
}

internal static class PSCommandDebugDisplay
{
    public static string ToDebuggerString(SMA.PSCommand command)
    {
        if (command.Commands.Count == 0)
        {
            return "{}";
        }

        return $"{{{command.Commands[0].CommandText}}}";
    }
}
#endif
