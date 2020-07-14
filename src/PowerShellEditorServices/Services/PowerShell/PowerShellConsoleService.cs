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

        private readonly object _stackLock = new object();

        private readonly ILogger _logger;

        private readonly PowerShellExecutionService _executionService;

        private readonly EditorServicesConsolePSHost _editorServicesHost;

        private readonly ConsoleReadLine _readLine;

        private readonly PSReadLineProxy _psrlProxy;

        private readonly Stack<ReplTask> _replLoopTaskStack;

        private readonly Stack<CancellationTokenSource> _currentCommandCancellationSourceStack;

        private bool _canCancel;

        private ConsoleKeyInfo? _lastKey;

        private bool _exiting;

        private PowerShellConsoleService(
            ILoggerFactory loggerFactory,
            PowerShellExecutionService executionService,
            EditorServicesConsolePSHost editorServicesHost,
            ConsoleReadLine readLine,
            PSReadLineProxy psrlProxy)
        {
            _logger = loggerFactory.CreateLogger<PowerShellConsoleService>();
            _replLoopTaskStack = new Stack<ReplTask>();
            _currentCommandCancellationSourceStack = new Stack<CancellationTokenSource>();
            _executionService = executionService;
            _editorServicesHost = editorServicesHost;
            _readLine = readLine;
            _psrlProxy = psrlProxy;
            _exiting = false;
        }

        public void Dispose()
        {
            while (_replLoopTaskStack.Count > 0)
            {
                StopCurrentRepl();
            }

            System.Console.CancelKeyPress -= OnCancelKeyPress;
            _executionService.PromptFramePushed -= OnPromptFramePushed;
            _executionService.PromptCancellationRequested -= OnPromptCancellationRequested;
            _executionService.NestedPromptExited -= OnNestedPromptExited;
        }

        public void StartRepl()
        {
            System.Console.CancelKeyPress += OnCancelKeyPress;
            System.Console.OutputEncoding = Encoding.UTF8;
            _psrlProxy.OverrideReadKey(ReadKey);
            _executionService.PromptFramePushed += OnPromptFramePushed;
            _executionService.PromptCancellationRequested += OnPromptCancellationRequested;
            _executionService.NestedPromptExited += OnNestedPromptExited;
            PushNewReplTask();
        }

        public void CancelCurrentPrompt()
        {
            bool canCancel = false;
            lock (_stackLock)
            {
                canCancel = _replLoopTaskStack.Peek().CanCancel;
            }

            if (canCancel)
            {
                _currentCommandCancellationSourceStack.Peek().Cancel();
            }
        }

        public void StopCurrentRepl()
        {
            ReplTask replTask;
            lock (_stackLock)
            {
                replTask = _replLoopTaskStack.Pop();
            }

            replTask.CancellationTokenSource.Cancel();
        }

        private async Task RunReplLoopAsync()
        {
            ReplTask replTask;
            lock (_stackLock)
            {
                replTask = _replLoopTaskStack.Peek();
            }

            using (replTask.CancellationTokenSource)
            {
                while (!replTask.CancellationTokenSource.IsCancellationRequested)
                {
                    var currentCommandCancellationSource = new CancellationTokenSource();
                    _currentCommandCancellationSourceStack.Push(currentCommandCancellationSource);
                    replTask.CanCancel = true;
                    try
                    {

                        string promptString = (await GetPromptOutputAsync(currentCommandCancellationSource.Token).ConfigureAwait(false)).FirstOrDefault() ?? "PS> ";

                        if (currentCommandCancellationSource.IsCancellationRequested)
                        {
                            continue;
                        }

                        WritePrompt(promptString);

                        string userInput = await InvokeReadLineAsync(currentCommandCancellationSource.Token).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(userInput))
                        {
                            if (currentCommandCancellationSource.IsCancellationRequested
                                || LastKeyWasCtrlC())
                            {
                                _editorServicesHost.UI.WriteLine();
                            }
                            continue;
                        }

                        await InvokeInputAsync(userInput, currentCommandCancellationSource.Token).ConfigureAwait(false);

                        if (replTask.CancellationTokenSource.IsCancellationRequested)
                        {
                            break;
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
                        replTask.CanCancel = false;
                        _currentCommandCancellationSourceStack.Pop().Dispose();
                    }
                }
            }

            _exiting = false;
        }

        private Task<IReadOnlyList<string>> GetPromptOutputAsync(CancellationToken cancellationToken)
        {
            var promptCommand = new PSCommand().AddCommand("prompt");

            return _executionService.ExecutePSCommandAsync<string>(
                promptCommand,
                new PowerShellExecutionOptions(),
                cancellationToken);
        }

        private void WritePrompt(string promptString)
        {
            _editorServicesHost.UI.Write(promptString);
        }

        private Task<string> InvokeReadLineAsync(CancellationToken cancellationToken)
        {
            return _readLine.ReadCommandLineAsync(cancellationToken);
        }

        private Task InvokeInputAsync(string input, CancellationToken cancellationToken)
        {
            var command = new PSCommand().AddScript(input);
            var executionOptions = new PowerShellExecutionOptions
            {
                WriteOutputToHost = true,
                AddToHistory = true
            };

            return _executionService.ExecutePSCommandAsync(command, executionOptions, cancellationToken);
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            CancelCurrentPrompt();
        }

        private void OnPromptFramePushed(object sender, PromptFramePushedArgs args)
        {
            PushNewReplTask();
        }

        private void OnPromptCancellationRequested(object sender, PromptCancellationRequestedArgs args)
        {
            CancelCurrentPrompt();
        }

        private void OnNestedPromptExited(object sender, NestedPromptExitedArgs args)
        {
            _exiting = true;
            StopCurrentRepl();
        }

        private void OnReplCanceled()
        {
            if (_exiting)
            {
                return;
            }

            CancelCurrentPrompt();
        }

        private ConsoleKeyInfo ReadKey(bool intercept)
        {
            _lastKey = ConsoleProxy.SafeReadKey(intercept, _currentCommandCancellationSourceStack.Peek().Token);
            return _lastKey.Value;
        }

        private bool LastKeyWasCtrlC()
        {
            return _lastKey != null
                && _lastKey.Value.Key == ConsoleKey.C
                && (_lastKey.Value.Modifiers & ConsoleModifiers.Control) != 0
                && (_lastKey.Value.Modifiers & ConsoleModifiers.Alt) == 0;
        }

        private void PushNewReplTask()
        {
            var replLoopCancellationSource = new CancellationTokenSource();
            lock (_stackLock)
            {
                replLoopCancellationSource.Token.Register(OnReplCanceled);
                var replTask = new ReplTask(Task.Run(RunReplLoopAsync, replLoopCancellationSource.Token), replLoopCancellationSource);
                _replLoopTaskStack.Push(replTask);
            }
        }


        private class ReplTask
        {
            public ReplTask(Task loopTask, CancellationTokenSource cancellationTokenSource)
            {
                LoopTask = loopTask;
                CancellationTokenSource = cancellationTokenSource;
                Guid = Guid.NewGuid();
                CanCancel = false;
            }

            public Task LoopTask { get; }

            public CancellationTokenSource CancellationTokenSource { get; }

            public Guid Guid { get; }

            public bool CanCancel { get; set; }
        }
    }
}
