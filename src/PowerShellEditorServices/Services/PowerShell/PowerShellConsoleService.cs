using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    internal class PowerShellConsoleService : IDisposable
    {
        public static PowerShellConsoleService CreateAndStart(
            ILogger logger,
            PowerShellExecutionService executionService)
        {
            var consoleService = new PowerShellConsoleService(logger, executionService);
            consoleService.StartRepl();
            return consoleService;
        }

        private readonly ILogger _logger;

        private readonly PowerShellExecutionService _executionService;

        private Task _consoleLoopThread;

        private CancellationTokenSource _replLoopCancellationSource;

        private CancellationTokenSource _currentCommandCancellationSource;

        private PSReadLineProxy _psrlProxy;

        private PowerShellConsoleService(ILogger logger, PowerShellExecutionService executionService)
        {
            _logger = logger;
            _executionService = executionService;
        }

        public void Dispose()
        {
            System.Console.CancelKeyPress -= HandleConsoleCancellation;
        }

        private void StartRepl()
        {
            _replLoopCancellationSource = new CancellationTokenSource();
            System.Console.CancelKeyPress += HandleConsoleCancellation;
            System.Console.OutputEncoding = Encoding.UTF8;
            _consoleLoopThread = Task.Run(RunReplLoopAsync, _replLoopCancellationSource.Token);
        }

        private async Task RunReplLoopAsync()
        {
            _psrlProxy = await PSReadLineProxy.LoadAndCreateAsync(_logger, _executionService).ConfigureAwait(false);

            while (!_replLoopCancellationSource.IsCancellationRequested)
            {
                _currentCommandCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_replLoopCancellationSource.Token);

                try
                {
                    await InvokePromptFunctionAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private Task InvokePromptFunctionAsync()
        {
            var promptCommand = new PSCommand().AddCommand("prompt");
            var executionOptions = new PowerShellExecutionOptions
            {
                WriteOutputToHost = true,
            };

            return _executionService.ExecutePSCommandAsync(
                promptCommand,
                executionOptions,
                _currentCommandCancellationSource.Token);
        }

        private Task<string> InvokeReadLineAsync()
        {
        }

        private void HandleConsoleCancellation(object sender, ConsoleCancelEventArgs args)
        {
        }
    }
}
