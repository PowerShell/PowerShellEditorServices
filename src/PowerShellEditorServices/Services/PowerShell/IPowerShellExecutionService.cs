// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    public interface IPowerShellExecutionService
    {
        Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            CancellationToken cancellationToken);

        Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            Action<SMA.PowerShell, CancellationToken> action,
            CancellationToken cancellationToken);

        Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<CancellationToken, TResult> func,
            CancellationToken cancellationToken);

        Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            Action<CancellationToken> action,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null);

        Task ExecutePSCommandAsync(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null);

        void CancelCurrentTask();
    }

    internal interface IInternalPowerShellExecutionService : IPowerShellExecutionService
    {
        event Action<object, RunspaceChangedEventArgs> RunspaceChanged;
    }
}
