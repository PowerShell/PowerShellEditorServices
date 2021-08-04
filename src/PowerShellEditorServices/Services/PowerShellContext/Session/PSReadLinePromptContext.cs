// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

    internal class PSReadLinePromptContext: IPromptContext
    {
        private static readonly Lazy<CmdletInfo> s_lazyInvokeReadLineForEditorServicesCmdletInfo = new Lazy<CmdletInfo>(() =>
        {
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblies = allAssemblies.FirstOrDefault(a => a.FullName.Contains("Microsoft.PowerShell.EditorServices"));
            var type = assemblies?.ExportedTypes?.FirstOrDefault(a => a.Name == "InvokeReadLineForEditorServicesCommand");
            return new CmdletInfo("__Invoke-ReadLineForEditorServices", type ?? typeof(PSCmdlet));
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
            out PSReadLineProxy readLineProxy)
        {
            readLineProxy = null;
            logger.LogTrace("Attempting to load PSReadLine");
            var psReadLineType = Type.GetType("Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine2");

            if (psReadLineType == null)
            {
                // NOTE: For some reason `Type.GetType(...)` can fail to find the type,
                // and in that case, this search through the `AppDomain` for some reason will succeed.
                // It's slower, but only happens when needed.
                logger.LogTrace("PSConsoleReadline type not found using Type.GetType(), searching all loaded assemblies...");
                psReadLineType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(asm => asm.GetName().Name.Equals("Microsoft.PowerShell.PSReadLine2"))
                    ?.ExportedTypes
                    ?.FirstOrDefault(type => type.FullName.Equals("Microsoft.PowerShell.PSConsoleReadLine"));
                if (psReadLineType == null)
                {
                    logger.LogWarning("PSConsoleReadLine type not found anywhere!");
                    return false;
                }
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
                logger.LogWarning("PSReadLineProxy unable to be initialized: {Reason}", e);
                return false;
            }
            catch (CommandNotFoundException e)
            {
                logger.LogWarning("PSReadLineProxy unable to be initialized: {Reason}", e);
                return false;
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

        public async Task AbortReadLineAsync()
        {
            if (_readLineCancellationSource == null)
            {
                return;
            }

            _readLineCancellationSource.Cancel();

            await WaitForReadLineExitAsync().ConfigureAwait(false);
        }

        public void WaitForReadLineExit()
        {
            using (_promptNest.GetRunspaceHandle(isReadLine: true, CancellationToken.None))
            { }
        }

        public async Task WaitForReadLineExitAsync()
        {
            using (await _promptNest.GetRunspaceHandleAsync(isReadLine: true, CancellationToken.None).ConfigureAwait(false))
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
