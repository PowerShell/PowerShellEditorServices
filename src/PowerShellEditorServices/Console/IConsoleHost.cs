//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading.Tasks;

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
            bool includeNewLine = true,
            OutputType outputType = OutputType.Normal,
            ConsoleColor foregroundColor = ConsoleColor.White,
            ConsoleColor backgroundColor = ConsoleColor.Black);

        /// <summary>
        /// Prompts the user to make a choice using the provided details.
        /// </summary>
        /// <param name="promptCaption">
        /// The caption string which will be displayed to the user.
        /// </param>
        /// <param name="promptMessage">
        /// The descriptive message which will be displayed to the user.
        /// </param>
        /// <param name="choices">
        /// The list of choices from which the user will select.
        /// </param>
        /// <param name="defaultChoice">
        /// The default choice to highlight for the user.
        /// </param>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's choice.
        /// </returns>
        Task<int> PromptForChoice(
            string promptCaption,
            string promptMessage,
            IEnumerable<ChoiceDetails> choices,
            int defaultChoice);

        // TODO: Get rid of this method!  Leaky abstraction.

        /// <summary>
        /// Sends a user's prompt choice response back to the specified prompt ID.
        /// </summary>
        /// <param name="promptId">
        /// The ID of the prompt to which the user is responding.
        /// </param>
        /// <param name="choiceResult">
        /// The index of the choice that the user selected.
        /// </param>
        void PromptForChoiceResult(
            int promptId,
            int choiceResult);

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
}
