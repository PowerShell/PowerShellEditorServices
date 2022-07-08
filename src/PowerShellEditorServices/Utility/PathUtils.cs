// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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

        internal static bool HasPowerShellScriptExtension(string fileNameOrPath)
        {
            if (fileNameOrPath is null or "")
            {
                return false;
            }

            ReadOnlySpan<char> pathSeparators = stackalloc char[] { DefaultPathSeparator, AlternatePathSeparator };
            ReadOnlySpan<char> asSpan = fileNameOrPath.AsSpan().TrimEnd(pathSeparators);
            int separatorIndex = asSpan.LastIndexOfAny(pathSeparators);
            if (separatorIndex is not -1)
            {
                asSpan = asSpan[(separatorIndex + 1)..];
            }

            int dotIndex = asSpan.LastIndexOf('.');
            if (dotIndex is -1)
            {
                return false;
            }

            ReadOnlySpan<char> extension = asSpan[(dotIndex + 1)..];
            if (extension.IsEmpty)
            {
                return false;
            }

            return extension.Equals("psm1".AsSpan(), StringComparison.OrdinalIgnoreCase)
                || extension.Equals("ps1".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
