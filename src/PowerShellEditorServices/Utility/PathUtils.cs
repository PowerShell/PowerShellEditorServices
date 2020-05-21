//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal class PathUtils
    {
        /// <summary>
        /// The default path separator used by the base implementation of the providers.
        ///
        /// Porting note: IO.Path.DirectorySeparatorChar is correct for all platforms. On Windows,
        /// it is '\', and on Linux, it is '/', as expected.
        /// </summary>
        internal static readonly char DefaultPathSeparator = Path.DirectorySeparatorChar;

        /// <summary>
        /// The alternate path separator used by the base implementation of the providers.
        ///
        /// Porting note: we do not use .NET's AlternatePathSeparatorChar here because it correctly
        /// states that both the default and alternate are '/' on Linux. However, for PowerShell to
        /// be "slash agnostic", we need to use the assumption that a '\' is the alternate path
        /// separator on Linux.
        /// </summary>
        internal static readonly char AlternatePathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '/' : '\\';

        /// <summary>
        /// Converts all alternate path separators to the current platform's main path separators.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The normalized path.</returns>
        public static string NormalizePathSeparators(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? path : path.Replace(AlternatePathSeparator, DefaultPathSeparator);
        }
    }
}
