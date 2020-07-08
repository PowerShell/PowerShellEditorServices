using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    internal class PowerShellConsoleService : IDisposable
    {
        public static PowerShellConsoleService CreateAndStart(
            ILoggerFactory loggerFactory,
            PowerShellExecutionService executionService)
        {
            return new PowerShellConsoleService(
                loggerFactory,
                executionService,
                executionService.EditorServicesHost,
                executionService.ReadLine,
                executionService.PSReadLineProxy);
        }

        private readonly ILogger _logger;

        private readonly PowerShellExecutionService _executionService;

        private readonly EditorServicesConsolePSHost _editorServicesHost;

        private readonly ConsoleReadLine _readLine;

        private readonly PSReadLineProxy _psrlProxy;

        private Task _consoleLoopThread;

        private CancellationTokenSource _replLoopCancellationSource;

        private CancellationTokenSource _currentCommandCancellationSource;

        private bool _canCancel;

        private PowerShellConsoleService(
            ILoggerFactory loggerFactory,
            PowerShellExecutionService executionService,
            EditorServicesConsolePSHost editorServicesHost,
            ConsoleReadLine readLine,
            PSReadLineProxy psrlProxy)
        {
            _logger = loggerFactory.CreateLogger<PowerShellConsoleService>();
            _executionService = executionService;
            _editorServicesHost = editorServicesHost;
            _readLine = readLine;
            _psrlProxy = psrlProxy;
        }

        public void Dispose()
        {
            System.Console.CancelKeyPress -= OnCancelKeyPress;
            _executionService.PromptFramePushed -= OnPromptFramePushed;
            _executionService.PromptFramePopped -= OnPromptFramePopped;
        }

        public void StartRepl()
        {
            _replLoopCancellationSource = new CancellationTokenSource();
            System.Console.CancelKeyPress += OnCancelKeyPress;
            System.Console.OutputEncoding = Encoding.UTF8;
            _psrlProxy.OverrideReadKey(ReadKey);
            _consoleLoopThread = Task.Run(RunReplLoopAsync, _replLoopCancellationSource.Token);
            _executionService.RegisterConsoleService(this);
            _executionService.PromptFramePushed += OnPromptFramePushed;
            _executionService.PromptFramePopped += OnPromptFramePopped;
        }

        public void CancelCurrentPrompt()
        {
            if (_canCancel)
            {
                _currentCommandCancellationSource?.Cancel();
            }
        }

        public void Stop()
        {
            _replLoopCancellationSource.Cancel();
        }

        private async Task RunReplLoopAsync()
        {
            while (!_replLoopCancellationSource.IsCancellationRequested)
            {
                _currentCommandCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_replLoopCancellationSource.Token);
                _canCancel = true;
                try
                {
                    string promptString = (await GetPromptOutputAsync().ConfigureAwait(false)).FirstOrDefault() ?? "PS> ";

                    WritePrompt(promptString);

                    string userInput = await InvokeReadLineAsync().ConfigureAwait(false);

                    if (_currentCommandCancellationSource.IsCancellationRequested)
                    {
                        _editorServicesHost.UI.WriteLine();
                        continue;
                    }

                    await InvokeInputAsync(userInput).ConfigureAwait(false);

                    if (_currentCommandCancellationSource.IsCancellationRequested)
                    {
                        _editorServicesHost.UI.WriteLine();
                    }
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception e)
                {

                }
                finally
                {
                    _canCancel = false;
                    _currentCommandCancellationSource.Dispose();
                    _currentCommandCancellationSource = null;
                }
            }
        }

        private Task<IReadOnlyList<string>> GetPromptOutputAsync()
        {
            var promptCommand = new PSCommand().AddCommand("prompt");

            return _executionService.ExecutePSCommandAsync<string>(
                promptCommand,
                new PowerShellExecutionOptions(),
                CancellationToken.None);
        }

        private void WritePrompt(string promptString)
        {
            _editorServicesHost.UI.Write(promptString);
        }

        private Task<string> InvokeReadLineAsync()
        {
            return _readLine.ReadCommandLineAsync(_currentCommandCancellationSource.Token);
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

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            CancelCurrentPrompt();
        }

        public void OnPromptFramePushed(object sender, PromptFramePushedArgs args)
        {
            Task.Run(RunReplLoopAsync);
        }

        public void OnPromptFramePopped(object sender, PromptFramePoppedArgs args)
        {
            CancelCurrentPrompt();
        }

        private ConsoleKeyInfo ReadKey(bool intercept)
        {
            return ConsoleProxy.SafeReadKey(intercept, _currentCommandCancellationSource?.Token ?? CancellationToken.None);
        }
    }
}
