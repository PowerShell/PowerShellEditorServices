//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides a simplified interface for writing output to a
    /// PowerShell host implementation.
    /// </summary>
    internal interface IHostOutput
    {
        /// <summary>
        /// Writes output of the given type to the user interface with
        /// the given foreground and background colors.  Also includes
        /// a newline if requested.
        /// </summary>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="includeNewLine">
        /// If true, a newline should be appended to the output's contents.
        /// </param>
        /// <param name="outputType">
        /// Specifies the type of output to be written.
        /// </param>
        /// <param name="foregroundColor">
        /// Specifies the foreground color of the output to be written.
        /// </param>
        /// <param name="backgroundColor">
        /// Specifies the background color of the output to be written.
        /// </param>
        void WriteOutput(
            string outputString,
            bool includeNewLine,
            OutputType outputType,
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor);
    }

    /// <summary>
    /// Provides helpful extension methods for the IHostOutput interface.
    /// </summary>
    internal static class IHostOutputExtensions
    {
        /// <summary>
        /// Writes normal output with a newline to the user interface.
        /// </summary>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        public static void WriteOutput(
            this IHostOutput hostOutput,
            string outputString)
        {
            hostOutput.WriteOutput(outputString, true);
        }

        /// <summary>
        /// Writes normal output to the user interface.
        /// </summary>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="includeNewLine">
        /// If true, a newline should be appended to the output's contents.
        /// </param>
        public static void WriteOutput(
            this IHostOutput hostOutput,
            string outputString,
            bool includeNewLine)
        {
            hostOutput.WriteOutput(
                outputString,
                includeNewLine,
                OutputType.Normal);
        }

        /// <summary>
        /// Writes output of a particular type to the user interface
        /// with a newline ending.
        /// </summary>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="outputType">
        /// Specifies the type of output to be written.
        /// </param>
        public static void WriteOutput(
            this IHostOutput hostOutput,
            string outputString,
            OutputType outputType)
        {
            hostOutput.WriteOutput(
                outputString,
                true,
                OutputType.Normal);
        }

        /// <summary>
        /// Writes output of a particular type to the user interface.
        /// </summary>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="includeNewLine">
        /// If true, a newline should be appended to the output's contents.
        /// </param>
        /// <param name="outputType">
        /// Specifies the type of output to be written.
        /// </param>
        public static void WriteOutput(
            this IHostOutput hostOutput,
            string outputString,
            bool includeNewLine,
            OutputType outputType)
        {
            hostOutput.WriteOutput(
                outputString,
                includeNewLine,
                outputType,
                ConsoleColor.Gray,
                (ConsoleColor)(-1)); // -1 indicates the console's raw background color
        }

        /// <summary>
        /// Writes output of a particular type to the user interface using
        /// a particular foreground color.
        /// </summary>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="includeNewLine">
        /// If true, a newline should be appended to the output's contents.
        /// </param>
        /// <param name="outputType">
        /// Specifies the type of output to be written.
        /// </param>
        /// <param name="foregroundColor">
        /// Specifies the foreground color of the output to be written.
        /// </param>
        public static void WriteOutput(
            this IHostOutput hostOutput,
            string outputString,
            bool includeNewLine,
            OutputType outputType,
            ConsoleColor foregroundColor)
        {
            hostOutput.WriteOutput(
                outputString,
                includeNewLine,
                outputType,
                foregroundColor,
                (ConsoleColor)(-1)); // -1 indicates the console's raw background color
        }
    }
}
