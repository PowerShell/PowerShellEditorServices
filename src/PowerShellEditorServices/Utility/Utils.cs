//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// General purpose common utilities to prevent reimplementation.
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// True if we are running on .NET Core, false otherwise.
        /// </summary>
        public static bool IsNetCore { get; } = RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.Ordinal);
    }
}
