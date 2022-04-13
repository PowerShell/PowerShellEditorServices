// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal static class PathUtils
    {
        /// <summary>
        /// <para>The default path separator used by the base implementation of the providers.</para>
        /// <para>
        /// Porting note: IO.Path.DirectorySeparatorChar is correct for all platforms. On Windows,
        /// it is '\', and on Linux, it is '/', as expected.
        /// </para>
        /// </summary>
        internal static readonly char DefaultPathSeparator = Path.DirectorySeparatorChar;

        /// <summary>
        /// <para>The alternate path separator used by the base implementation of the providers.</para>
        /// <para>
        /// Porting note: we do not use .NET's AlternatePathSeparatorChar here because it correctly
        /// states that both the default and alternate are '/' on Linux. However, for PowerShell to
        /// be "slash agnostic", we need to use the assumption that a '\' is the alternate path
        /// separator on Linux.
        /// </para>
        /// </summary>
        internal static readonly char AlternatePathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '/' : '\\';

        /// <summary>
        /// Converts all alternate path separators to the current platform's main path separators.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The normalized path.</returns>
        public static string NormalizePathSeparators(string path) => string.IsNullOrWhiteSpace(path) ? path : path.Replace(AlternatePathSeparator, DefaultPathSeparator);

        /// <summary>
        /// Return the given path with all PowerShell globbing characters escaped,
        /// plus optionally the whitespace.
        /// </summary>
        /// <param name="path">The path to process.</param>
        /// <param name="escapeSpaces">Specify True to escape spaces in the path, otherwise False.</param>
        /// <returns>The path with *, ?, [, and ] escaped, including spaces if required</returns>
        internal static string WildcardEscapePath(string path, bool escapeSpaces = false)
        {
            string wildcardEscapedPath = WildcardPattern.Escape(path);

            if (escapeSpaces)
            {
                wildcardEscapedPath = wildcardEscapedPath.Replace(" ", "` ");
            }
            return wildcardEscapedPath;
        }
    }
}
