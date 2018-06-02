using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.EditorServices.Console;

namespace Microsoft.PowerShell.EditorServices.Session {
    using System.Management.Automation;

    internal class PSReadLinePromptContext : IPromptContext {
        private const string ReadLineScript = @"
            [System.Diagnostics.DebuggerHidden()]
            [System.Diagnostics.DebuggerStepThrough()]
            param()
            return [Microsoft.PowerShell.PSConsoleReadLine]::ReadLine(
                $Host.Runspace,
                $ExecutionContext,
                $args[0])";

        // private const string ReadLineScript = @"
        //     [System.Diagnostics.DebuggerHidden()]
        //     [System.Diagnostics.DebuggerStepThrough()]
        //     param(
        //         [Parameter(Mandatory)]
        //         [Threading.CancellationToken] $CancellationToken,

        //         [ValidateNotNull()]
        //         [runspace] $Runspace = $Host.Runspace,

        //         [ValidateNotNull()]
        //         [System.Management.Automation.EngineIntrinsics] $EngineIntrinsics = $ExecutionContext
        //     )
        //     end {
        //         if ($CancellationToken.IsCancellationRequested) {
        //             return [string]::Empty
        //         }

        //         return [Microsoft.PowerShell.PSConsoleReadLine]::ReadLine(
        //             $Runspace,
        //             $EngineIntrinsics,
        //             $CancellationToken)
        //     }";

        private const string ReadLineInitScript = @"
            [System.Diagnostics.DebuggerHidden()]
            [System.Diagnostics.DebuggerStepThrough()]
            param()
            end {
                $module = Get-Module -ListAvailable PSReadLine | Select-Object -First 1
                if (-not $module -or $module.Version -lt ([version]'2.0.0')) {
                    return
                }

                Import-Module -ModuleInfo $module
                return 'Microsoft.PowerShell.PSConsoleReadLine' -as [type]
            }";

        private readonly PowerShellContext _powerShellContext;

        private PromptNest _promptNest;

        private InvocationEventQueue _invocationEventQueue;

        private ConsoleReadLine _consoleReadLine;

        private CancellationTokenSource _readLineCancellationSource;

        private PSReadLineProxy _readLineProxy;

        internal PSReadLinePromptContext(
            PowerShellContext powerShellContext,
            PromptNest promptNest,
            InvocationEventQueue invocationEventQueue,
            PSReadLineProxy readLineProxy)
        {
            _promptNest = promptNest;
            _powerShellContext = powerShellContext;
            _invocationEventQueue = invocationEventQueue;
            _consoleReadLine = new ConsoleReadLine(powerShellContext);
            _readLineProxy = readLineProxy;

            #if CoreCLR
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            _readLineProxy.OverrideReadKey(
                intercept => ConsoleProxy.UnixReadKey(
                    intercept,
                    _readLineCancellationSource.Token));
            #endif
        }

        internal static bool TryGetPSReadLineProxy(Runspace runspace, out PSReadLineProxy readLineProxy)
        {
            readLineProxy = null;
            using (var pwsh = PowerShell.Create())
            {
                pwsh.Runspace = runspace;
                var psReadLineType = pwsh
                    .AddScript(ReadLineInitScript)
                    .Invoke<Type>()
                    .FirstOrDefault();

                if (psReadLineType == null)
                {
                    return false;
                }

                try
                {
                    readLineProxy = new PSReadLineProxy(psReadLineType);
                }
                catch (InvalidOperationException)
                {
                    // The Type we got back from PowerShell doesn't have the members we expected.
                    // Could be an older version, a custom build, or something a newer version with
                    // breaking changes.
                    return false;
                }
            }

            return true;
        }

        public async Task<string> InvokeReadLine(bool isCommandLine, CancellationToken cancellationToken)
        {
            _readLineCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var localTokenSource = _readLineCancellationSource;
            if (localTokenSource.Token.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            try
            {
                if (!isCommandLine)
                {
                    return await _consoleReadLine.InvokeLegacyReadLine(
                        false,
                        _readLineCancellationSource.Token);
                }

                var result = (await _powerShellContext.ExecuteCommand<string>(
                    new PSCommand()
                        .AddScript(ReadLineScript)
                        .AddArgument(_readLineCancellationSource.Token),
                    null,
                    new ExecutionOptions()
                    {
                        WriteErrorsToHost = false,
                        WriteOutputToHost = false,
                        InterruptCommandPrompt = false,
                        AddToHistory = false,
                        IsReadLine = isCommandLine
                    }))
                    .FirstOrDefault();

                return cancellationToken.IsCancellationRequested
                    ? string.Empty
                    : result;
            }
            finally
            {
                _readLineCancellationSource = null;
            }
        }

        public void AbortReadLine()
        {
            if (_readLineCancellationSource == null)
            {
                return;
            }

            _readLineCancellationSource.Cancel();

            WaitForReadLineExit();
        }

        public async Task AbortReadLineAsync() {
            if (_readLineCancellationSource == null)
            {
                return;
            }

            _readLineCancellationSource.Cancel();

            await WaitForReadLineExitAsync();
        }

        public void WaitForReadLineExit()
        {
            using (_promptNest.GetRunspaceHandle(CancellationToken.None, isReadLine: true))
            { }
        }

        public async Task WaitForReadLineExitAsync () {
            using (await _promptNest.GetRunspaceHandleAsync(CancellationToken.None, isReadLine: true))
            { }
        }

        public void AddToHistory(string command)
        {
            _readLineProxy.AddToHistory(command);
        }

        public void ForcePSEventHandling()
        {
            _readLineProxy.ForcePSEventHandling();
        }
    }
}
