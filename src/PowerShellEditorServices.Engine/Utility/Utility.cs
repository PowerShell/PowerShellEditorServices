using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Engine
{
    /// <summary>
    /// General purpose common utilities to prevent reimplementation.
    /// </summary>
    internal static class Utility
    {
        /// <summary>
        /// True if we are running on .NET Core, false otherwise.
        /// </summary>
        public static bool IsNetCore { get; } = RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.Ordinal);
}
}
