//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    using System;
    using System.Management.Automation;
    using System.Management.Automation.Language;
    using System.Security;

    internal class ConsoleReadLine
    {
        #region Private Field
        private readonly PowerShellContextService powerShellContext;

        #endregion

        #region Constructors

        public ConsoleReadLine(PowerShellContextService powerShellContext)
        {
            this.powerShellContext = powerShellContext;
        }

        #endregion

        #region Public Methods

        public Task<string> ReadCommandLineAsync(CancellationToken cancellationToken)
        {
            return this.ReadLineAsync(true, cancellationToken);
        }

        public Task<string> ReadSimpleLineAsync(CancellationToken cancellationToken)
        {
            return this.ReadLineAsync(false, cancellationToken);
        }

        public async Task<SecureString> ReadSecureLineAsync(CancellationToken cancellationToken)
        {
            SecureString secureString = new SecureString();

            // TODO: Are these values used?
            int initialPromptRow = await ConsoleProxy.GetCursorTopAsync(cancellationToken).ConfigureAwait(false);
            int initialPromptCol = await ConsoleProxy.GetCursorLeftAsync(cancellationToken).ConfigureAwait(false);
            int previousInputLength = 0;

            Console.TreatControlCAsInput = true;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsoleKeyInfo keyInfo = await ReadKeyAsync(cancellationToken).ConfigureAwait(false);

                    if ((int)keyInfo.Key == 3 ||
                        keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        throw new PipelineStoppedException();
                    }
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        // Break to return the completed string
                        break;
                    }
                    if (keyInfo.Key == ConsoleKey.Tab)
                    {
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (secureString.Length > 0)
                        {
                            secureString.RemoveAt(secureString.Length - 1);
                        }
                    }
                    else if (keyInfo.KeyChar != 0 && !char.IsControl(keyInfo.KeyChar))
                    {
                        secureString.AppendChar(keyInfo.KeyChar);
                    }

                    // Re-render the secure string characters
                    int currentInputLength = secureString.Length;
                    int consoleWidth = Console.WindowWidth;

                    if (currentInputLength > previousInputLength)
                    {
                        Console.Write('*');
                    }
                    else if (previousInputLength > 0 && currentInputLength < previousInputLength)
                    {
                        int row = await ConsoleProxy.GetCursorTopAsync(cancellationToken).ConfigureAwait(false);
                        int col = await ConsoleProxy.GetCursorLeftAsync(cancellationToken).ConfigureAwait(false);

                        // Back up the cursor before clearing the character
                        col--;
                        if (col < 0)
                        {
                            col = consoleWidth - 1;
                            row--;
                        }

                        Console.SetCursorPosition(col, row);
                        Console.Write(' ');
                        Console.SetCursorPosition(col, row);
                    }

                    previousInputLength = currentInputLength;
                }
            }
            finally
            {
                Console.TreatControlCAsInput = false;
            }

            return secureString;
        }

        #endregion

        #region Private Methods

        private static Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken)
        {
            return ConsoleProxy.ReadKeyAsync(intercept: true, cancellationToken);
        }

        private Task<string> ReadLineAsync(bool isCommandLine, CancellationToken cancellationToken)
        {
            return this.powerShellContext.InvokeReadLineAsync(isCommandLine, cancellationToken);
        }

        /// <summary>
        /// Invokes a custom ReadLine method that is similar to but more basic than PSReadLine.
        /// This method should be used when PSReadLine is disabled, either by user settings or
        /// unsupported PowerShell versions.
        /// </summary>
        /// <param name="isCommandLine">
        /// Indicates whether ReadLine should act like a command line.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token that will be checked prior to completing the returned task.
        /// </param>
        /// <returns>
        /// A task object representing the asynchronus operation. The Result property on
        /// the task object returns the user input string.
        /// </returns>
        internal async Task<string> InvokeLegacyReadLineAsync(bool isCommandLine, CancellationToken cancellationToken)
        {
            // TODO: Is inputBeforeCompletion used?
            string inputBeforeCompletion = null;
            string inputAfterCompletion = null;
            CommandCompletion currentCompletion = null;

            int historyIndex = -1;
            Collection<PSObject> currentHistory = null;

            StringBuilder inputLine = new StringBuilder();

            int initialCursorCol = await ConsoleProxy.GetCursorLeftAsync(cancellationToken).ConfigureAwait(false);
            int initialCursorRow = await ConsoleProxy.GetCursorTopAsync(cancellationToken).ConfigureAwait(false);

            // TODO: Are these used?
            int initialWindowLeft = Console.WindowLeft;
            int initialWindowTop = Console.WindowTop;

            int currentCursorIndex = 0;

            Console.TreatControlCAsInput = true;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsoleKeyInfo keyInfo = await ReadKeyAsync(cancellationToken).ConfigureAwait(false);

                    // Do final position calculation after the key has been pressed
                    // because the window could have been resized before then
                    int promptStartCol = initialCursorCol;
                    int promptStartRow = initialCursorRow;
                    int consoleWidth = Console.WindowWidth;

                    if ((int)keyInfo.Key == 3 ||
                        keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        throw new PipelineStoppedException();
                    }
                    else if (keyInfo.Key == ConsoleKey.Tab && isCommandLine)
                    {
                        if (currentCompletion == null)
                        {
                            inputBeforeCompletion = inputLine.ToString();
                            inputAfterCompletion = null;

                            // TODO: This logic should be moved to AstOperations or similar!

                            if (this.powerShellContext.IsDebuggerStopped)
                            {
                                PSCommand command = new PSCommand();
                                command.AddCommand("TabExpansion2");
                                command.AddParameter("InputScript", inputBeforeCompletion);
                                command.AddParameter("CursorColumn", currentCursorIndex);
                                command.AddParameter("Options", null);

                                var results = await this.powerShellContext
                                    .ExecuteCommandAsync<CommandCompletion>(command, sendOutputToHost: false, sendErrorToHost: false)
                                    .ConfigureAwait(false);

                                currentCompletion = results.FirstOrDefault();
                            }
                            else
                            {
                                using (RunspaceHandle runspaceHandle = await this.powerShellContext.GetRunspaceHandleAsync().ConfigureAwait(false))
                                using (PowerShell powerShell = PowerShell.Create())
                                {
                                    powerShell.Runspace = runspaceHandle.Runspace;
                                    currentCompletion =
                                        CommandCompletion.CompleteInput(
                                            inputBeforeCompletion,
                                            currentCursorIndex,
                                            null,
                                            powerShell);

                                    if (currentCompletion.CompletionMatches.Count > 0)
                                    {
                                        int replacementEndIndex =
                                                currentCompletion.ReplacementIndex +
                                                currentCompletion.ReplacementLength;

                                        inputAfterCompletion =
                                            inputLine.ToString(
                                                replacementEndIndex,
                                                inputLine.Length - replacementEndIndex);
                                    }
                                    else
                                    {
                                        currentCompletion = null;
                                    }
                                }
                            }
                        }

                        CompletionResult completion =
                            currentCompletion?.GetNextResult(
                                !keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift));

                        if (completion != null)
                        {
                            currentCursorIndex =
                                this.InsertInput(
                                    inputLine,
                                    promptStartCol,
                                    promptStartRow,
                                    $"{completion.CompletionText}{inputAfterCompletion}",
                                    currentCursorIndex,
                                    insertIndex: currentCompletion.ReplacementIndex,
                                    replaceLength: inputLine.Length - currentCompletion.ReplacementIndex,
                                    finalCursorIndex: currentCompletion.ReplacementIndex + completion.CompletionText.Length);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.LeftArrow)
                    {
                        currentCompletion = null;

                        if (currentCursorIndex > 0)
                        {
                            currentCursorIndex =
                                this.MoveCursorToIndex(
                                    promptStartCol,
                                    promptStartRow,
                                    consoleWidth,
                                    currentCursorIndex - 1);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Home)
                    {
                        currentCompletion = null;

                        currentCursorIndex =
                            this.MoveCursorToIndex(
                                promptStartCol,
                                promptStartRow,
                                consoleWidth,
                                0);
                    }
                    else if (keyInfo.Key == ConsoleKey.RightArrow)
                    {
                        currentCompletion = null;

                        if (currentCursorIndex < inputLine.Length)
                        {
                            currentCursorIndex =
                                this.MoveCursorToIndex(
                                    promptStartCol,
                                    promptStartRow,
                                    consoleWidth,
                                    currentCursorIndex + 1);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.End)
                    {
                        currentCompletion = null;

                        currentCursorIndex =
                            this.MoveCursorToIndex(
                                promptStartCol,
                                promptStartRow,
                                consoleWidth,
                                inputLine.Length);
                    }
                    else if (keyInfo.Key == ConsoleKey.UpArrow && isCommandLine)
                    {
                        currentCompletion = null;

                        // TODO: Ctrl+Up should allow navigation in multi-line input

                        if (currentHistory == null)
                        {
                            historyIndex = -1;

                            PSCommand command = new PSCommand();
                            command.AddCommand("Get-History");

                            currentHistory = await this.powerShellContext.ExecuteCommandAsync<PSObject>(command, sendOutputToHost: false, sendErrorToHost: false)
                                .ConfigureAwait(false)
                                as Collection<PSObject>;

                            if (currentHistory != null)
                            {
                                historyIndex = currentHistory.Count;
                            }
                        }

                        if (currentHistory != null && currentHistory.Count > 0 && historyIndex > 0)
                        {
                            historyIndex--;

                            currentCursorIndex =
                                this.InsertInput(
                                    inputLine,
                                    promptStartCol,
                                    promptStartRow,
                                    (string)currentHistory[historyIndex].Properties["CommandLine"].Value,
                                    currentCursorIndex,
                                    insertIndex: 0,
                                    replaceLength: inputLine.Length);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.DownArrow && isCommandLine)
                    {
                        currentCompletion = null;

                        // The down arrow shouldn't cause history to be loaded,
                        // it's only for navigating an active history array

                        if (historyIndex > -1 && historyIndex < currentHistory.Count &&
                            currentHistory != null && currentHistory.Count > 0)
                        {
                            historyIndex++;

                            if (historyIndex < currentHistory.Count)
                            {
                                currentCursorIndex =
                                    this.InsertInput(
                                        inputLine,
                                        promptStartCol,
                                        promptStartRow,
                                        (string)currentHistory[historyIndex].Properties["CommandLine"].Value,
                                        currentCursorIndex,
                                        insertIndex: 0,
                                        replaceLength: inputLine.Length);
                            }
                            else if (historyIndex == currentHistory.Count)
                            {
                                currentCursorIndex =
                                    this.InsertInput(
                                        inputLine,
                                        promptStartCol,
                                        promptStartRow,
                                        string.Empty,
                                        currentCursorIndex,
                                        insertIndex: 0,
                                        replaceLength: inputLine.Length);
                            }
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        currentCompletion = null;
                        historyIndex = currentHistory != null ? currentHistory.Count : -1;

                        currentCursorIndex =
                            this.InsertInput(
                                inputLine,
                                promptStartCol,
                                promptStartRow,
                                string.Empty,
                                currentCursorIndex,
                                insertIndex: 0,
                                replaceLength: inputLine.Length);
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        currentCompletion = null;

                        if (currentCursorIndex > 0)
                        {
                            currentCursorIndex =
                                this.InsertInput(
                                    inputLine,
                                    promptStartCol,
                                    promptStartRow,
                                    string.Empty,
                                    currentCursorIndex,
                                    insertIndex: currentCursorIndex - 1,
                                    replaceLength: 1,
                                    finalCursorIndex: currentCursorIndex - 1);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Delete)
                    {
                        currentCompletion = null;

                        if (currentCursorIndex < inputLine.Length)
                        {
                            currentCursorIndex =
                                this.InsertInput(
                                    inputLine,
                                    promptStartCol,
                                    promptStartRow,
                                    string.Empty,
                                    currentCursorIndex,
                                    replaceLength: 1,
                                    finalCursorIndex: currentCursorIndex);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        string completedInput = inputLine.ToString();
                        currentCompletion = null;
                        currentHistory = null;

                        //if ((keyInfo.Modifiers & ConsoleModifiers.Shift) == ConsoleModifiers.Shift)
                        //{
                        //    // TODO: Start a new line!
                        //    continue;
                        //}

                        Parser.ParseInput(
                            completedInput,
                            out Token[] tokens,
                            out ParseError[] parseErrors);

                        //if (parseErrors.Any(e => e.IncompleteInput))
                        //{
                        //    // TODO: Start a new line!
                        //    continue;
                        //}

                        return completedInput;
                    }
                    else if (keyInfo.KeyChar != 0 && !char.IsControl(keyInfo.KeyChar))
                    {
                        // Normal character input
                        currentCompletion = null;

                        currentCursorIndex =
                            this.InsertInput(
                                inputLine,
                                promptStartCol,
                                promptStartRow,
                                keyInfo.KeyChar.ToString(), // TODO: Determine whether this should take culture into account
                                currentCursorIndex,
                                finalCursorIndex: currentCursorIndex + 1);
                    }
                }
            }
            finally
            {
                Console.TreatControlCAsInput = false;
            }

            return null;
        }

        // TODO: Is this used?
        private int CalculateIndexFromCursor(
            int promptStartCol,
            int promptStartRow,
            int consoleWidth)
        {
            return
                ((ConsoleProxy.GetCursorTop() - promptStartRow) * consoleWidth) +
                ConsoleProxy.GetCursorLeft() - promptStartCol;
        }

        private void CalculateCursorFromIndex(
            int promptStartCol,
            int promptStartRow,
            int consoleWidth,
            int inputIndex,
            out int cursorCol,
            out int cursorRow)
        {
            cursorCol = promptStartCol + inputIndex;
            cursorRow = promptStartRow + cursorCol / consoleWidth;
            cursorCol = cursorCol % consoleWidth;
        }

        private int InsertInput(
            StringBuilder inputLine,
            int promptStartCol,
            int promptStartRow,
            string insertedInput,
            int cursorIndex,
            int insertIndex = -1,
            int replaceLength = 0,
            int finalCursorIndex = -1)
        {
            int consoleWidth = Console.WindowWidth;
            int previousInputLength = inputLine.Length;

            if (insertIndex == -1)
            {
                insertIndex = cursorIndex;
            }

            // Move the cursor to the new insertion point
            this.MoveCursorToIndex(
                promptStartCol,
                promptStartRow,
                consoleWidth,
                insertIndex);

            // Edit the input string based on the insertion
            if (insertIndex < inputLine.Length)
            {
                if (replaceLength > 0)
                {
                    inputLine.Remove(insertIndex, replaceLength);
                }

                inputLine.Insert(insertIndex, insertedInput);
            }
            else
            {
                inputLine.Append(insertedInput);
            }

            // Re-render affected section
            Console.Write(
                inputLine.ToString(
                    insertIndex,
                    inputLine.Length - insertIndex));

            if (inputLine.Length < previousInputLength)
            {
                Console.Write(
                    new string(
                        ' ',
                        previousInputLength - inputLine.Length));
            }

            // Automatically set the final cursor position to the end
            // of the new input string.  This is needed if the previous
            // input string is longer than the new one and needed to have
            // its old contents overwritten.  This will position the cursor
            // back at the end of the new text
            if (finalCursorIndex == -1 && inputLine.Length < previousInputLength)
            {
                finalCursorIndex = inputLine.Length;
            }

            if (finalCursorIndex > -1)
            {
                // Move the cursor to the final position
                return
                    this.MoveCursorToIndex(
                        promptStartCol,
                        promptStartRow,
                        consoleWidth,
                        finalCursorIndex);
            }
            else
            {
                return inputLine.Length;
            }
        }

        private int MoveCursorToIndex(
            int promptStartCol,
            int promptStartRow,
            int consoleWidth,
            int newCursorIndex)
        {
            this.CalculateCursorFromIndex(
                promptStartCol,
                promptStartRow,
                consoleWidth,
                newCursorIndex,
                out int newCursorCol,
                out int newCursorRow);

            Console.SetCursorPosition(newCursorCol, newCursorRow);

            return newCursorIndex;
        }

        #endregion
    }
}
