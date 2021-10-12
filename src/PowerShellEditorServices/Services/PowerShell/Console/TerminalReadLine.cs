// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System.Management.Automation;
using System.Security;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    using System;

    internal abstract class TerminalReadLine : IReadLine
    {
        public abstract string ReadLine(CancellationToken cancellationToken);

        public abstract bool TryOverrideIdleHandler(Action<CancellationToken> idleHandler);

        public abstract bool TryOverrideReadKey(Func<bool, ConsoleKeyInfo> readKeyOverride);

        protected abstract ConsoleKeyInfo ReadKey(CancellationToken cancellationToken);

        public SecureString ReadSecureLine(CancellationToken cancellationToken)
        {
            Console.TreatControlCAsInput = true;
            int previousInputLength = 0;
            SecureString secureString = new SecureString();
            try
            {
                bool enterPressed = false;
                while (!enterPressed && !cancellationToken.IsCancellationRequested)
                {
                    ConsoleKeyInfo keyInfo = ReadKey(cancellationToken);

                    if (keyInfo.IsCtrlC())
                    {
                        throw new PipelineStoppedException();
                    }

                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Enter:
                            // Break to return the completed string
                            enterPressed = true;
                            continue;

                        case ConsoleKey.Tab:
                            break;

                        case ConsoleKey.Backspace:
                            if (secureString.Length > 0)
                            {
                                secureString.RemoveAt(secureString.Length - 1);
                            }
                            break;

                        default:
                            if (keyInfo.KeyChar != 0 && !char.IsControl(keyInfo.KeyChar))
                            {
                                secureString.AppendChar(keyInfo.KeyChar);
                            }
                            break;
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
                        int row = ConsoleProxy.GetCursorTop(cancellationToken);
                        int col = ConsoleProxy.GetCursorLeft(cancellationToken);

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
    }
}
