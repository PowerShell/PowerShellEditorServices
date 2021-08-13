using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    internal class PowerShellExecutionService
    {
        private readonly ILogger _logger;

        private readonly InternalHost _psesHost;

        public PowerShellExecutionService(
            ILoggerFactory loggerFactory,
            InternalHost psesHost)
        {
            _logger = loggerFactory.CreateLogger<PowerShellExecutionService>();
            _psesHost = psesHost;
        }

        public Action<object, RunspaceChangedEventArgs> RunspaceChanged;

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Func<SMA.PowerShell, CancellationToken, TResult> func)
            => _psesHost.ExecuteDelegateAsync(representation, executionOptions, cancellationToken, func);

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<SMA.PowerShell, CancellationToken> action)
            => _psesHost.ExecuteDelegateAsync(representation, executionOptions, cancellationToken, action);

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Func<CancellationToken, TResult> func)
            => _psesHost.ExecuteDelegateAsync(representation, executionOptions, cancellationToken, func);

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<CancellationToken> action)
            => _psesHost.ExecuteDelegateAsync(representation, executionOptions, cancellationToken, action);

        public Task<IReadOnlyList<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null)
            => _psesHost.ExecutePSCommandAsync<TResult>(psCommand, cancellationToken, executionOptions);

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null) => ExecutePSCommandAsync<PSObject>(psCommand, cancellationToken, executionOptions);

        public void CancelCurrentTask()
        {
            _psesHost.CancelCurrentTask();
        }
    }
}
