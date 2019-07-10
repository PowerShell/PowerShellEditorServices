using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// General purpose common utilities to prevent reimplementation.
    /// </summary>
    internal static class VersionUtils
    {
        /// <summary>
        /// True if we are running on .NET Core, false otherwise.
        /// </summary>
        public static bool IsNetCore { get; } = RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.Ordinal);

        /// <summary>
        /// Get's the Version of PowerShell being used.
        /// </summary>
        public static Version PSVersion { get; } = PowerShellReflectionUtils.PSVersion;

        /// <summary>
        /// True if we are running in Windows PowerShell, false otherwise.
        /// </summary>
        public static bool IsPS5 { get; } = PSVersion.Major == 5;

        /// <summary>
        /// True if we are running in PowerShell Core 6, false otherwise.
        /// </summary>
        public static bool IsPS6 { get; } = PSVersion.Major == 6;

        /// <summary>
        /// True if we are running in PowerShell 7, false otherwise.
        /// </summary>
        public static bool IsPS7 { get; } = PSVersion.Major == 7;
    }

    internal static class PowerShellReflectionUtils
    {

        private static readonly Assembly _psRuntimeAssembly = typeof(System.Management.Automation.Runspaces.Runspace).Assembly;
        private static readonly PropertyInfo _psVersionProperty = _psRuntimeAssembly.GetType("System.Management.Automation.PSVersionInfo")
            .GetProperty("PSVersion", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Get's the Version of PowerShell being used.
        /// </summary>
        public static Version PSVersion { get; } = _psVersionProperty.GetValue(null) as Version;
    }
}
