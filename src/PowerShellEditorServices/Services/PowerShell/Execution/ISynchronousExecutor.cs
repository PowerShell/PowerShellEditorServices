using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using SMA = System.Management.Automation;
using System.Text;
using System.Threading;
using System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal interface ISynchronousExecutor
    {
        TResult InvokeDelegate<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<CancellationToken, TResult> func,
            CancellationToken cancellationToken);

        void InvokeDelegate(
            string representation,
            ExecutionOptions executionOptions,
            Action<CancellationToken> action,
            CancellationToken cancellationToken);

        TResult InvokePSDelegate<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            CancellationToken cancellationToken);

        void InvokePSDelegate(
            string representation,
            ExecutionOptions executionOptions,
            Action<SMA.PowerShell, CancellationToken> action,
            CancellationToken cancellationToken);

        IReadOnlyList<TResult> InvokePSCommand<TResult>(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken);

        void InvokePSCommand(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken);
    }
}
