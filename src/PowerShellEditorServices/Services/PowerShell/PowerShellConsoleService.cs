using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using System;
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
            PowerShellStartupService startupService,
            PowerShellExecutionService executionService)
        {
            var consoleService = new PowerShellConsoleService(
                logger,
                executionService,
                startupService.EngineIntrinsics,
                startupService.EditorServicesHost,
                startupService.ReadLine);

            return consoleService;
        }

        private readonly ILogger _logger;

        private readonly PowerShellExecutionService _executionService;

        private readonly EngineIntrinsics _engineIntrinsics;

        private readonly EditorServicesConsolePSHost _editorServicesHost;

        private readonly ConsoleReadLine _readLine;

        private Task _consoleLoopThread;

        private CancellationTokenSource _replLoopCancellationSource;

        private CancellationTokenSource _currentCommandCancellationSource;

        private PowerShellConsoleService(
            ILogger logger,
            PowerShellExecutionService executionService,
            EngineIntrinsics engineIntrinsics,
            EditorServicesConsolePSHost editorServicesHost,
            ConsoleReadLine readLine)
        {
            _logger = logger;
            _executionService = executionService;
            _engineIntrinsics = engineIntrinsics;
            _editorServicesHost = editorServicesHost;
            _readLine = readLine;
        }

        public void Dispose()
        {
            System.Console.CancelKeyPress -= HandleConsoleCancellation;
        }

        public void StartRepl()
        {
            _replLoopCancellationSource = new CancellationTokenSource();
            System.Console.CancelKeyPress += HandleConsoleCancellation;
            System.Console.OutputEncoding = Encoding.UTF8;
            _consoleLoopThread = Task.Run(RunReplLoopAsync, _replLoopCancellationSource.Token);
        }

        private async Task RunReplLoopAsync()
        {
            while (!_replLoopCancellationSource.IsCancellationRequested)
            {
                _currentCommandCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_replLoopCancellationSource.Token);

                try
                {
                    await InvokePromptFunctionAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    // Poll for user input here so that the prompt does not block
                    string userInput = await InvokeReadLineAsync();

                    await InvokeInputAsync(userInput).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    continue;
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

        private async Task<string> InvokeReadLineAsync()
        {
            string input = null;
            while (string.IsNullOrEmpty(input))
            {
                try
                {
                    input = await InvokePSReadLineAsync(timeoutMillis: 30);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
            }

            return input;
        }

        private Task<string> InvokePSReadLineAsync(int timeoutMillis)
        {
            var readlineCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_currentCommandCancellationSource.Token);
            readlineCancellationSource.CancelAfter(timeoutMillis);

            return _readLine.ReadCommandLineAsync(readlineCancellationSource.Token);
        }

        private Task InvokeInputAsync(string input)
        {
            var command = new PSCommand().AddScript(input);
            var executionOptions = new PowerShellExecutionOptions
            {
                WriteOutputToHost = true,
                AddToHistory = true
            };

            return _executionService.ExecutePSCommandAsync(command, executionOptions, _currentCommandCancellationSource.Token);
        }

        private void HandleConsoleCancellation(object sender, ConsoleCancelEventArgs args)
        {
        }
    }
}
