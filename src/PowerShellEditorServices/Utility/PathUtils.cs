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
        /// The <see cref="StringComparison" /> value to be used when comparing paths. Will be
        /// <see cref="StringComparison.Ordinal" /> for case sensitive file systems and <see cref="StringComparison.OrdinalIgnoreCase" />
        /// in case insensitive file systems.
        /// </summary>
        internal static readonly StringComparison PathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Determines whether two specified strings represent the same path.
        /// </summary>
        /// <param name="left">The first path to compare, or <see langword="null" />.</param>
        /// <param name="right">The second path to compare, or <see langword="null" />.</param>
        /// <returns>
        /// <see langword="true" /> if the value of <paramref name="left" /> represents the same
        /// path as the value of <paramref name="right" />; otherwise, <see langword="false" />.
        /// </returns>
        internal static bool IsPathEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
            {
                return string.IsNullOrEmpty(right);
            }

            if (string.IsNullOrEmpty(right))
            {
                return false;
            }

            left = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar);
            right = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar);
            return left.Equals(right, PathComparison);
        }

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
