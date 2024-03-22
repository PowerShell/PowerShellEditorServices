// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    using System;

    internal abstract class TerminalReadLine : IReadLine
    {
        public virtual void AddToHistory(string historyEntry)
        {
            // No-op by default. If the ReadLine provider is not PSRL then history is automatically
            // added as part of the invocation process.
        }

        public abstract string ReadLine(CancellationToken cancellationToken);

        protected abstract ConsoleKeyInfo ReadKey(CancellationToken cancellationToken);
    }
}
