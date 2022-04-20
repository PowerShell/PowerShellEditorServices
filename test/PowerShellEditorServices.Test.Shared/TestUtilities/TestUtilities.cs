// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared
{
    /// <summary>
    /// Convenience class to simplify cross-platform testing
    /// </summary>
    public static class TestUtilities
    {
        private static readonly char[] s_unixNewlines = new[] { '\n' };

        /// <summary>
        /// Takes a UNIX-style path and converts it to the path appropriate to the platform.
        /// </summary>
        /// <param name="unixPath">A forward-slash separated path.</param>
        /// <returns>A path with directories separated by the appropriate separator.</returns>
        public static string NormalizePath(string unixPath)
        {
            if (unixPath == null)
            {
                return unixPath;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return unixPath.Replace('/', Path.DirectorySeparatorChar);
            }

            return unixPath;
        }

        /// <summary>
        /// Gets a normalized path from the directory of this assembly to the given path under the
        /// shared test folder.
        /// </summary>
        /// <param name="path">A path or file under the shared test folder.</param>
        /// <returns>The normalized and resolved path to it.</returns>
        public static string GetSharedPath(string path)
        {
            // TODO: When testing net462 with x64 host, another .. is needed!
            return NormalizePath(Path.Combine(
                Path.GetDirectoryName(typeof(TestUtilities).Assembly.Location),
                "../../../../PowerShellEditorServices.Test.Shared",
                path));
        }

        /// <summary>
        /// Take a string with UNIX newlines and replaces them with platform-appropriate newlines.
        /// </summary>
        /// <param name="unixString">The string with UNIX-style newlines.</param>
        /// <returns>The platform-newline-normalized string.</returns>
        public static string NormalizeNewlines(string unixString)
        {
            if (unixString == null)
            {
                return unixString;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return string.Join(Environment.NewLine, unixString.Split(s_unixNewlines));
            }

            return unixString;
        }

        /// <summary>
        /// Platform-normalize a string -- takes a UNIX-style string and gives it platform-appropriate newlines and path separators.
        /// </summary>
        /// <param name="unixString">The string to normalize for the platform, given with UNIX-specific separators.</param>
        /// <returns>The same string but separated by platform-appropriate directory and newline separators.</returns>
        public static string PlatformNormalize(string unixString) => NormalizeNewlines(NormalizePath(unixString));

        /// <summary>
        /// Not for use in production -- convenience code for debugging tests.
        /// </summary>
        public static void AwaitDebuggerHere(
            [CallerMemberName] string callerName = null,
            [CallerFilePath] string callerPath = null,
            [CallerLineNumber] int callerLine = -1)
        {
            if (Debugger.IsAttached)
            {
                return;
            }

            System.Console.WriteLine();
            System.Console.WriteLine("===== AWAITING DEBUGGER =====");
            System.Console.WriteLine($"  PID: {Process.GetCurrentProcess().Id}");
            System.Console.WriteLine($"  Waiting at {callerPath} line {callerLine} ({callerName})");
            System.Console.WriteLine("  PRESS ANY KEY TO CONTINUE");
            System.Console.WriteLine("=============================");
            System.Console.ReadKey();
        }
    }
}
