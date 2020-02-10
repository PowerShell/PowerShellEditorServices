//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides details about output that has been written to the
    /// PowerShell host.
    /// </summary>
    internal class OutputWrittenEventArgs
    {
        /// <summary>
        /// Gets the text of the output.
        /// </summary>
        public string OutputText { get; private set; }

        /// <summary>
        /// Gets the type of the output.
        /// </summary>
        public OutputType OutputType { get; private set; }

        /// <summary>
        /// Gets a boolean which indicates whether a newline
        /// should be written after the output.
        /// </summary>
        public bool IncludeNewLine { get; private set; }

        /// <summary>
        /// Gets the foreground color of the output text.
        /// </summary>
        public ConsoleColor ForegroundColor { get; private set; }

        /// <summary>
        /// Gets the background color of the output text.
        /// </summary>
        public ConsoleColor BackgroundColor { get; private set; }

        /// <summary>
        /// Creates an instance of the OutputWrittenEventArgs class.
        /// </summary>
        /// <param name="outputText">The text of the output.</param>
        /// <param name="includeNewLine">A boolean which indicates whether a newline should be written after the output.</param>
        /// <param name="outputType">The type of the output.</param>
        /// <param name="foregroundColor">The foreground color of the output text.</param>
        /// <param name="backgroundColor">The background color of the output text.</param>
        public OutputWrittenEventArgs(
            string outputText,
            bool includeNewLine,
            OutputType outputType,
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor)
        {
            this.OutputText = outputText;
            this.IncludeNewLine = includeNewLine;
            this.OutputType = outputType;
            this.ForegroundColor = foregroundColor;
            this.BackgroundColor = backgroundColor;
        }
    }
}
