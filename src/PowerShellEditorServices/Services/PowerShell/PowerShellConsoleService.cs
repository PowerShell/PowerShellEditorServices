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
        }

        public void Dispose()
        {
            while (_replLoopTaskStack.Count > 0)
            {
                StopCurrentRepl();
            }

            System.Console.CancelKeyPress -= OnCancelKeyPress;
            _executionService.PromptFramePushed -= OnPromptFramePushed;
            _executionService.PromptFramePopped -= OnPromptFramePopped;
        }

        public void StartRepl()
        {
            System.Console.CancelKeyPress += OnCancelKeyPress;
            System.Console.OutputEncoding = Encoding.UTF8;
            _psrlProxy.OverrideReadKey(ReadKey);
            _executionService.RegisterConsoleService(this);
            _executionService.PromptFramePushed += OnPromptFramePushed;
            _executionService.PromptFramePopped += OnPromptFramePopped;
            PushNewReplTask();
        }

        public void CancelCurrentPrompt()
        {
            if (_canCancel)
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
                    var currentCommandCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(replTask.CancellationTokenSource.Token);
                    _currentCommandCancellationSourceStack.Push(currentCommandCancellationSource);
                    _canCancel = true;
                    try
                    {

                        string promptString = (await GetPromptOutputAsync(currentCommandCancellationSource.Token).ConfigureAwait(false)).FirstOrDefault() ?? "PS> ";

                        if (currentCommandCancellationSource.IsCancellationRequested)
                        {
                            continue;
                        }

                        WritePrompt(promptString);

                        string userInput = await InvokeReadLineAsync(currentCommandCancellationSource.Token).ConfigureAwait(false);

                        if (currentCommandCancellationSource.IsCancellationRequested)
                        {
                            continue;
                        }

                        await InvokeInputAsync(userInput, currentCommandCancellationSource.Token).ConfigureAwait(false);

                        if (replTask.CancellationTokenSource.IsCancellationRequested)
                        {
                            break;
                        }

                        if (currentCommandCancellationSource.IsCancellationRequested)
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
                        _currentCommandCancellationSourceStack.Pop().Dispose();
                    }
                }
            }
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

        private void OnPromptFramePopped(object sender, PromptFramePoppedArgs args)
        {
            StopCurrentRepl();
        }

        private ConsoleKeyInfo ReadKey(bool intercept)
        {
            return ConsoleProxy.SafeReadKey(intercept, _currentCommandCancellationSourceStack.Peek().Token);
        }

        private void PushNewReplTask()
        {
            var replLoopCancellationSource = new CancellationTokenSource();
            lock (_stackLock)
            {
                _replLoopTaskStack.Push(new ReplTask(Task.Run(RunReplLoopAsync, replLoopCancellationSource.Token), replLoopCancellationSource));
            }
        }


        private struct ReplTask
        {
            public ReplTask(Task loopTask, CancellationTokenSource cancellationTokenSource)
            {
                LoopTask = loopTask;
                CancellationTokenSource = cancellationTokenSource;
            }

            public Task LoopTask { get; }

            public CancellationTokenSource CancellationTokenSource { get; }
        }
    }
}
