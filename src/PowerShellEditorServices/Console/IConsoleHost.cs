//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Provides a simplified interface for implementing a PowerShell
    /// host that will be used for an interactive console.
    /// </summary>
    public interface IConsoleHost
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

        /// <summary>
        /// Creates a ChoicePromptHandler to use for displaying a
        /// choice prompt to the user.
        /// </summary>
        /// <returns>A new ChoicePromptHandler instance.</returns>
        ChoicePromptHandler GetChoicePromptHandler();

        /// <summary>
        /// Sends a progress update event to the user.
        /// </summary>
        /// <param name="sourceId">The source ID of the progress event.</param>
        /// <param name="progressDetails">The details of the activity's current progress.</param>
        void UpdateProgress(
            long sourceId,
            ProgressDetails progressDetails);

        /// <summary>
        /// Notifies the IConsoleHost implementation that the PowerShell
        /// session is exiting.
        /// </summary>
        /// <param name="exitCode">The error code that identifies the session exit result.</param>
        void ExitSession(int exitCode);
    }

    /// <summary>
    /// Provides helpful extension methods for the IConsoleHost interface.
    /// </summary>
    public static class IConsoleHostExtensions
    {
        /// <summary>
        /// Writes normal output with a newline to the user interface.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        public static void WriteOutput(
            this IConsoleHost consoleHost,
            string outputString)
        {
            consoleHost.WriteOutput(outputString, true);
        }

        /// <summary>
        /// Writes normal output to the user interface.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="includeNewLine">
        /// If true, a newline should be appended to the output's contents.
        /// </param>
        public static void WriteOutput(
            this IConsoleHost consoleHost,
            string outputString,
            bool includeNewLine)
        {
            consoleHost.WriteOutput(
                outputString,
                includeNewLine,
                OutputType.Normal);
        }

        /// <summary>
        /// Writes output of a particular type to the user interface
        /// with a newline ending.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost implementation to use for WriteOutput calls.
        /// </param>
        /// <param name="outputString">
        /// The output string to be written.
        /// </param>
        /// <param name="outputType">
        /// Specifies the type of output to be written.
        /// </param>
        public static void WriteOutput(
            this IConsoleHost consoleHost,
            string outputString,
            OutputType outputType)
        {
            consoleHost.WriteOutput(
                outputString,
                true,
                OutputType.Normal);
        }

        /// <summary>
        /// Writes output of a particular type to the user interface.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost implementation to use for WriteOutput calls.
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
            this IConsoleHost consoleHost,
            string outputString,
            bool includeNewLine,
            OutputType outputType)
        {
            consoleHost.WriteOutput(
                outputString,
                includeNewLine,
                outputType,
                ConsoleColor.White,
                ConsoleColor.Black);
        }

        /// <summary>
        /// Writes output of a particular type to the user interface using
        /// a particular foreground color.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost implementation to use for WriteOutput calls.
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
            this IConsoleHost consoleHost,
            string outputString,
            bool includeNewLine,
            OutputType outputType,
            ConsoleColor foregroundColor)
        {
            consoleHost.WriteOutput(
                outputString,
                includeNewLine,
                outputType,
                foregroundColor,
                ConsoleColor.Black);
        }
    }
}
