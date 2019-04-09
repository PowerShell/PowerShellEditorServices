//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#if CoreCLR
using System.Runtime.InteropServices;
#endif

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
#if CoreCLR
        public static bool IsNetCore { get; } = true;
#else
        public static bool IsNetCore { get; } = false;
#endif
    }
}
