//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal class LegacyReadLineContext : IPromptContext
    {
        private readonly ConsoleReadLine _legacyReadLine;

        internal LegacyReadLineContext(PowerShellContextService powerShellContext)
        {
            _legacyReadLine = new ConsoleReadLine(powerShellContext);
        }

        public Task AbortReadLineAsync()
        {
            return Task.FromResult(true);
        }

        public Task<string> InvokeReadLineAsync(bool isCommandLine, CancellationToken cancellationToken)
        {
            return _legacyReadLine.InvokeLegacyReadLineAsync(isCommandLine, cancellationToken);
        }

        public Task WaitForReadLineExitAsync()
        {
            return Task.FromResult(true);
        }

        public void AddToHistory(string command)
        {
            // Do nothing, history is managed completely by the PowerShell engine in legacy ReadLine.
        }

        public void AbortReadLine()
        {
            // Do nothing, no additional actions are needed to cancel ReadLine.
        }

        public void WaitForReadLineExit()
        {
            // Do nothing, ReadLine cancellation is instant or not appliciable.
        }

        public void ForcePSEventHandling()
        {
            // Do nothing, the pipeline thread is not occupied by legacy ReadLine.
        }
    }
}
