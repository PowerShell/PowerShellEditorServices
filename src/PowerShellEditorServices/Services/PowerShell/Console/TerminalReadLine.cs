// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    using System;

    internal abstract class TerminalReadLine : IReadLine
    {
        public abstract string ReadLine(CancellationToken cancellationToken);

        protected abstract ConsoleKeyInfo ReadKey(CancellationToken cancellationToken);
    }
}
