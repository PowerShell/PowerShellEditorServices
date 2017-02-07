//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    using System;
    using System.Management.Automation;
    using System.Management.Automation.Language;
    using System.Security;

    internal class ConsoleReadLine
    {
        #region Private Fields

        private PowerShellContext powerShellContext;

        #endregion

        #region Constructors

        public ConsoleReadLine(PowerShellContext powerShellContext)
        {
            this.powerShellContext = powerShellContext;
        }

        #endregion

        #region Public Methods

        public async Task<string> ReadCommandLine(CancellationToken cancellationToken)
        {
            string inputBeforeCompletion = null;
            string inputAfterCompletion = null;
            CommandCompletion currentCompletion = null;

            int historyIndex = -1;
            Collection<PSObject> currentHistory = null;

            StringBuilder inputLine = new StringBuilder();

            int initialCursorCol = Console.CursorLeft;
            int initialCursorRow = Console.CursorTop;

            int initialWindowLeft = Console.WindowLeft;
            int initialWindowTop = Console.WindowTop;

            int currentCursorIndex = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                ConsoleKeyInfo? possibleKeyInfo = await this.ReadKeyAsync(cancellationToken);

                if (!possibleKeyInfo.HasValue)
                {
                    // The read operation was cancelled
                    return null;
                }

                ConsoleKeyInfo keyInfo = possibleKeyInfo.Value;

                // Do final position calculation after the key has been pressed
                // because the window could have been resized before then
                int promptStartCol = initialCursorCol;
                int promptStartRow = initialCursorRow;

                // The effective width of the console is 1 less than
                // Console.WindowWidth, all calculations should be
                // with respect to that
                int consoleWidth = Console.WindowWidth - 1;

                if ((keyInfo.Modifiers & ConsoleModifiers.Alt) == ConsoleModifiers.Alt ||
                    (keyInfo.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                {
                    // Ignore any Ctrl or Alt key combinations
                    // TODO: What about word movements?
                    continue;
                }
                else if (keyInfo.Key == ConsoleKey.Tab)
                {
                    if (currentCompletion == null)
                    {
                        inputBeforeCompletion = inputLine.ToString();
                        inputAfterCompletion = null;

                        int cursorColumn =
                            this.CalculateIndexFromCursor(
                                promptStartCol,
                                promptStartRow,
                                consoleWidth);

                        // TODO: This logic should be moved to AstOperations or similar!

                        if (this.powerShellContext.IsDebuggerStopped)
                        {
                            PSCommand command = new PSCommand();
                            command.AddCommand("TabExpansion2");
                            command.AddParameter("InputScript", inputBeforeCompletion);
                            command.AddParameter("CursorColumn", cursorColumn);
                            command.AddParameter("Options", null);

                            var results =
                                await this.powerShellContext.ExecuteCommand<CommandCompletion>(command, false, false);

                            currentCompletion = results.FirstOrDefault();
                        }
                        else
                        {
                            using (RunspaceHandle runspaceHandle = await this.powerShellContext.GetRunspaceHandle())
                            using (PowerShell powerShell = PowerShell.Create())
                            {
                                powerShell.Runspace = runspaceHandle.Runspace;
                                currentCompletion =
                                    CommandCompletion.CompleteInput(
                                        inputBeforeCompletion,
                                        cursorColumn,
                                        null,
                                        powerShell);

                                int replacementEndIndex =
                                        currentCompletion.ReplacementIndex +
                                        currentCompletion.ReplacementLength;

                                inputAfterCompletion =
                                    inputLine.ToString(
                                        replacementEndIndex,
                                        inputLine.Length - replacementEndIndex);
                            }
                        }
                    }

                    CompletionResult completion =
                        currentCompletion.GetNextResult(
                            !keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift));

                    if (completion != null)
                    {
                        currentCursorIndex =
                            this.InsertInput(
                                inputLine,
                                promptStartCol,
                                promptStartRow,
                                $"{completion.CompletionText}{inputAfterCompletion}",
                                insertIndex: currentCompletion.ReplacementIndex,
                                replaceLength: inputLine.Length - currentCompletion.ReplacementIndex);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.LeftArrow)
                {
                    currentCompletion = null;

                    if (currentCursorIndex > 0)
                    {
                        this.MoveCursorToIndex(
                            promptStartCol,
                            promptStartRow,
                            consoleWidth,
                            --currentCursorIndex);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Home)
                {
                    currentCompletion = null;
                    currentCursorIndex = 0;

                    this.MoveCursorToIndex(
                        promptStartCol,
                        promptStartRow,
                        consoleWidth,
                        currentCursorIndex);
                }
                else if (keyInfo.Key == ConsoleKey.RightArrow)
                {
                    currentCompletion = null;

                    if (currentCursorIndex < inputLine.Length)
                    {
                        this.MoveCursorToIndex(
                            promptStartCol,
                            promptStartRow,
                            consoleWidth,
                            ++currentCursorIndex);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.End)
                {
                    currentCompletion = null;
                    currentCursorIndex = inputLine.Length;

                    this.MoveCursorToIndex(
                        promptStartCol,
                        promptStartRow,
                        consoleWidth,
                        currentCursorIndex);
                }
                else if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    currentCompletion = null;

                    // TODO: Ctrl+Up should allow navigation in multi-line input

                    if (currentHistory == null)
                    {
                        historyIndex = -1;

                        using (RunspaceHandle runspaceHandle = await this.powerShellContext.GetRunspaceHandle())
                        using (PowerShell powerShell = PowerShell.Create())
                        {
                            powerShell.Runspace = runspaceHandle.Runspace;
                            powerShell.AddCommand("Get-History");
                            currentHistory = powerShell.Invoke();
                        }

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
                                insertIndex: 0,
                                replaceLength: inputLine.Length);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
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
                                currentCursorIndex - 1,
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
                else if (keyInfo.KeyChar != 0)
                {
                    // Normal character input
                    currentCompletion = null;

                    currentCursorIndex =
                        this.InsertInput(
                            inputLine,
                            promptStartCol,
                            promptStartRow,
                            keyInfo.KeyChar.ToString(),
                            finalCursorIndex: currentCursorIndex + 1);
                }
            }

            return null;
        }

        public Task<string> ReadSimpleLine()
        {
            // TODO: Implement this!
            return Task.FromResult(string.Empty);
        }

        public Task<SecureString> ReadSecureLine()
        {
            // TODO: Implement this!
            return Task.FromResult(new SecureString());
        }

        #endregion

        #region Private Methods

        private async Task<ConsoleKeyInfo?> ReadKeyAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    return Console.ReadKey(true);
               }

                await Task.Delay(50);
            }

            return null;
        }

        private int CalculateIndexFromCursor(
            int promptStartCol,
            int promptStartRow,
            int consoleWidth)
        {
            return
                ((Console.CursorTop - promptStartRow) * consoleWidth) +
                Console.CursorLeft - promptStartCol;
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
            int insertIndex = -1,
            int replaceLength = 0,
            int finalCursorIndex = -1)
        {
            int consoleWidth = Console.WindowWidth - 1;
            int previousInputLength = inputLine.Length;

            int startCol = -1;
            int startRow = -1;

            if (insertIndex == -1)
            {
                // Find the insertion index based on the position of the
                // cursor relative to the initial position
                insertIndex =
                    this.CalculateIndexFromCursor(
                        promptStartCol,
                        promptStartRow,
                        consoleWidth);
            }
            else
            {
                // Calculate the starting position based on the insert index
                this.CalculateCursorFromIndex(
                    promptStartCol,
                    promptStartRow,
                    consoleWidth,
                    insertIndex,
                    out startCol,
                    out startRow);
            }

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

            // Set the cursor position if necessary
            int writeCursorCol = startCol;
            if (startCol > -1)
            {
                Console.SetCursorPosition(startCol, startRow);
            }
            else
            {
                writeCursorCol = Console.CursorLeft;
            }

            // Re-render affected section
            // TODO: Render this in chunks for perf
            for (int i = insertIndex;
                 i < Math.Max(inputLine.Length, previousInputLength);
                 i++)
            {
                if (i < inputLine.Length)
                {
                    Console.Write(inputLine[i]);
                }
                else
                {
                    Console.Write(' ');
                }

                writeCursorCol++;

                if (writeCursorCol == consoleWidth)
                {
                    writeCursorCol = 0;
                    Console.CursorTop += 1;
                    Console.CursorLeft = 0;
                }
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
                this.MoveCursorToIndex(
                    promptStartCol,
                    promptStartRow,
                    consoleWidth,
                    finalCursorIndex);
            }

            //Console.Write(
            //    inputLine.ToString(
            //        insertIndex,
            //        inputLine.Length - insertIndex));

            // Return the updated cursor index
            return finalCursorIndex != -1 ? finalCursorIndex : inputLine.Length;
        }

        private void MoveCursorToIndex(
            int promptStartCol,
            int promptStartRow,
            int consoleWidth,
            int cursorIndex)
        {
            this.CalculateCursorFromIndex(
                promptStartCol,
                promptStartRow,
                consoleWidth,
                cursorIndex,
                out int newCol,
                out int newRow);

            Console.SetCursorPosition(newCol, newRow);
        }

        #endregion
    }
}
