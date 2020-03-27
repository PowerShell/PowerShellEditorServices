//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    using System.Collections.Generic;
    using System.Management.Automation;
    using Microsoft.Extensions.Logging;

    internal class PSReadLinePromptContext : IPromptContext
    {
        private const string ReadLineScript = @"
            [System.Diagnostics.DebuggerHidden()]
            [System.Diagnostics.DebuggerStepThrough()]
            param()
            return [Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine2, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null]::ReadLine(
                $Host.Runspace,
                $ExecutionContext,
                $args[0])";

        private const string ReadLineInitScript = @"
            [System.Diagnostics.DebuggerHidden()]
            [System.Diagnostics.DebuggerStepThrough()]
            param()
            end {
                $module = Get-Module -ListAvailable PSReadLine |
                    Where-Object { $_.Version -gt '2.0.0' -or ($_.Version -eq '2.0.0' -and -not $_.PrivateData.PSData.Prerelease) } |
                    Sort-Object -Descending Version |
                    Select-Object -First 1
                if (-not $module) {
                    return
                }

                Import-Module -ModuleInfo $module
                return [Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine2, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null]
            }";

        private static ExecutionOptions s_psrlExecutionOptions = new ExecutionOptions
        {
            WriteErrorsToHost = false,
            WriteOutputToHost = false,
            InterruptCommandPrompt = false,
            AddToHistory = false,
            IsReadLine = true,
        };

        private readonly PowerShellContextService _powerShellContext;

        private readonly PromptNest _promptNest;

        private readonly InvocationEventQueue _invocationEventQueue;

        private readonly ConsoleReadLine _consoleReadLine;

        private readonly PSReadLineProxy _readLineProxy;

        private CancellationTokenSource _readLineCancellationSource;

        internal PSReadLinePromptContext(
            PowerShellContextService powerShellContext,
            PromptNest promptNest,
            InvocationEventQueue invocationEventQueue,
            PSReadLineProxy readLineProxy)
        {
            _promptNest = promptNest;
            _powerShellContext = powerShellContext;
            _invocationEventQueue = invocationEventQueue;
            _consoleReadLine = new ConsoleReadLine(powerShellContext);
            _readLineProxy = readLineProxy;

            _readLineProxy.OverrideReadKey(
                intercept => ConsoleProxy.SafeReadKey(
                    intercept,
                    _readLineCancellationSource.Token));
        }

        internal static bool TryGetPSReadLineProxy(
            ILogger logger,
            Runspace runspace,
            out PSReadLineProxy readLineProxy)
        {
            readLineProxy = null;
            logger.LogTrace("Attempting to load PSReadLine");
            using (var pwsh = PowerShell.Create())
            {
                pwsh.Runspace = runspace;
                var psReadLineType = pwsh
                    .AddScript(ReadLineInitScript)
                    .Invoke<Type>()
                    .FirstOrDefault();

                if (psReadLineType == null)
                {
                    logger.LogWarning("PSReadLine unable to be loaded: {Reason}", pwsh.HadErrors ? pwsh.Streams.Error[0].ToString() : "<Unknown reason>");
                    return false;
                }

                try
                {
                    readLineProxy = new PSReadLineProxy(psReadLineType, logger);
                }
                catch (InvalidOperationException e)
                {
                    // The Type we got back from PowerShell doesn't have the members we expected.
                    // Could be an older version, a custom build, or something a newer version with
                    // breaking changes.
                    logger.LogWarning("PSReadLine unable to be loaded: {Reason}", e);
                    return false;
                }
            }

            return true;
        }

        public async Task<string> InvokeReadLineAsync(bool isCommandLine, CancellationToken cancellationToken)
        {
            _readLineCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var localTokenSource = _readLineCancellationSource;
            if (localTokenSource.Token.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            if (!isCommandLine)
            {
                return await _consoleReadLine.InvokeLegacyReadLineAsync(
                    isCommandLine: false,
                    _readLineCancellationSource.Token).ConfigureAwait(false);
            }

            var readLineCommand = new PSCommand()
                .AddScript(ReadLineScript)
                .AddArgument(_readLineCancellationSource.Token);

            IEnumerable<string> readLineResults = await _powerShellContext.ExecuteCommandAsync<string>(
                readLineCommand,
                errorMessages: null,
                s_psrlExecutionOptions).ConfigureAwait(false);

            string line = readLineResults.FirstOrDefault();

            return cancellationToken.IsCancellationRequested
                ? string.Empty
                : line;
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

            await WaitForReadLineExitAsync().ConfigureAwait(false);
        }

        public void WaitForReadLineExit()
        {
            using (_promptNest.GetRunspaceHandle(CancellationToken.None, isReadLine: true))
            { }
        }

        public async Task WaitForReadLineExitAsync() {
            using (await _promptNest.GetRunspaceHandleAsync(CancellationToken.None, isReadLine: true).ConfigureAwait(false))
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
