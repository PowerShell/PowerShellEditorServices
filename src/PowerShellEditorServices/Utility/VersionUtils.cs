//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Utility
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
        /// Gets the Version of PowerShell being used.
        /// </summary>
        public static Version PSVersion { get; } = PowerShellReflectionUtils.PSVersion;

        /// <summary>
        /// Gets the Edition of PowerShell being used.
        /// </summary>
        public static string PSEdition { get; } = PowerShellReflectionUtils.PSEdition;

        /// <summary>
        /// Gets the string of the PSVersion including prerelease tags if it applies.
        /// </summary>
        public static string PSVersionString { get; } = PowerShellReflectionUtils.PSVersionString;

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
        public static bool IsPS7OrGreater { get; } = PSVersion.Major >= 7;

        /// <summary>
        /// True if we are running in on Windows, false otherwise.
        /// </summary>
        public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// True if we are running in on macOS, false otherwise.
        /// </summary>
        public static bool IsMacOS { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// True if we are running in on Linux, false otherwise.
        /// </summary>
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    internal static class PowerShellReflectionUtils
    {

        private static readonly Type s_psVersionInfoType = typeof(System.Management.Automation.Runspaces.Runspace).Assembly.GetType("System.Management.Automation.PSVersionInfo");

        // This property is a Version type in PowerShell. It's existed since 5.1, but it was only made public in 6.2.
        private static readonly PropertyInfo s_psVersionProperty = s_psVersionInfoType
            .GetProperty("PSVersion", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        // This property is a SemanticVersion in PowerShell that contains the prerelease tag as well.
        // It was added in 6.2 so we can't depend on it for anything before.
        private static readonly PropertyInfo s_psCurrentVersionProperty = s_psVersionInfoType
            .GetProperty("PSCurrentVersion", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        private static readonly PropertyInfo s_psEditionProperty = s_psVersionInfoType
            .GetProperty("PSEdition", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        /// <summary>
        /// Get's the Version of PowerShell being used. Note: this will get rid of the SemVer 2.0 suffix because apparently
        /// that property is added as a note property and it is not there when we reflect.
        /// </summary>
        public static Version PSVersion { get; } = s_psVersionProperty.GetValue(null) as Version;

        /// <summary>
        /// Get's the Edition of PowerShell being used.
        /// </summary>
        public static string PSEdition { get; } = s_psEditionProperty.GetValue(null) as string;

        /// <summary>
        /// Gets the stringified version of PowerShell including prerelease tags if it applies.
        /// </summary>
        public static string PSVersionString { get; } = s_psCurrentVersionProperty != null
            ? s_psCurrentVersionProperty.GetValue(null).ToString()
            : s_psVersionProperty.GetValue(null).ToString();
    }
}
