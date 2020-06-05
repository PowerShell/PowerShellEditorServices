//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    using System.IO;
    using System.Management.Automation;

    internal class PSReadLinePromptContext : IPromptContext
    {
        private static readonly string _psReadLineModulePath = Path.Combine(
            Path.GetDirectoryName(typeof(PSReadLinePromptContext).Assembly.Location),
            "..",
            "..",
            "..",
            "PSReadLine");

        private static readonly string ReadLineInitScript = $@"
            [System.Diagnostics.DebuggerHidden()]
            [System.Diagnostics.DebuggerStepThrough()]
            param()
            end {{
                $module = Get-Module -ListAvailable PSReadLine |
                    Where-Object {{ $_.Version -ge '2.0.2' }} |
                    Sort-Object -Descending Version |
                    Select-Object -First 1
                if (-not $module) {{
                    Import-Module '{_psReadLineModulePath.Replace("'", "''")}'
                    return [Microsoft.PowerShell.PSConsoleReadLine]
                }}

                Import-Module -ModuleInfo $module
                return [Microsoft.PowerShell.PSConsoleReadLine]
            }}";

        private static readonly Lazy<CmdletInfo> s_lazyInvokeReadLineForEditorServicesCmdletInfo = new Lazy<CmdletInfo>(() =>
        {
            var type = Type.GetType("Microsoft.PowerShell.EditorServices.Commands.InvokeReadLineForEditorServicesCommand, Microsoft.PowerShell.EditorServices.Hosting");
            return new CmdletInfo("__Invoke-ReadLineForEditorServices", type);
        });

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
                    .AddScript(ReadLineInitScript, useLocalScope: true)
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
                .AddCommand(s_lazyInvokeReadLineForEditorServicesCmdletInfo.Value)
                .AddParameter("CancellationToken", _readLineCancellationSource.Token);

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
