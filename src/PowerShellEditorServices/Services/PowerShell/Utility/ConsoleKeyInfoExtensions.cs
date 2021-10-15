// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal static class ConsoleKeyInfoExtensions
    {
        public static bool IsCtrlC(this ConsoleKeyInfo keyInfo)
        {
            if ((int)keyInfo.Key == 3)
            {
                return true;
            }

            return keyInfo.Key == ConsoleKey.C
                && (keyInfo.Modifiers & ConsoleModifiers.Control) != 0
                && (keyInfo.Modifiers & ConsoleModifiers.Shift) == 0
                && (keyInfo.Modifiers & ConsoleModifiers.Alt) == 0;
        }
    }
}
