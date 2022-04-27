// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.DebugAdapter.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ConfigurationDoneHandler : IConfigurationDoneHandler
    {
        // TODO: We currently set `WriteInputToHost` as true, which writes our debugged commands'
        // `GetInvocationText` and that reveals some obscure implementation details we should
        // instead hide from the user with pretty strings (or perhaps not write out at all).
        //
        // This API is mostly used for F5 execution so it requires the foreground.
        private static readonly PowerShellExecutionOptions s_debuggerExecutionOptions = new()
        {
            RequiresForeground = true,
            WriteInputToHost = true,
            WriteOutputToHost = true,
            ThrowOnError = false,
            AddToHistory = true,
        };

        private readonly ILogger _logger;
        private readonly IDebugAdapterServerFacade _debugAdapterServer;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly WorkspaceService _workspaceService;
        private readonly IPowerShellDebugContext _debugContext;
        private readonly IRunspaceContext _runspaceContext;

        // TODO: Decrease these arguments since they're a bunch of interfaces that can be simplified
        // (i.e., `IRunspaceContext` should just be available on `IPowerShellExecutionService`).
        public ConfigurationDoneHandler(
            ILoggerFactory loggerFactory,
            IDebugAdapterServerFacade debugAdapterServer,
            DebugService debugService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService,
            IInternalPowerShellExecutionService executionService,
            WorkspaceService workspaceService,
            IPowerShellDebugContext debugContext,
            IRunspaceContext runspaceContext)
        {
            _logger = loggerFactory.CreateLogger<ConfigurationDoneHandler>();
            _debugAdapterServer = debugAdapterServer;
            _debugService = debugService;
            _debugStateService = debugStateService;
            _debugEventHandlerService = debugEventHandlerService;
            _executionService = executionService;
            _workspaceService = workspaceService;
            _debugContext = debugContext;
            _runspaceContext = runspaceContext;
        }

        public Task<ConfigurationDoneResponse> Handle(ConfigurationDoneArguments request, CancellationToken cancellationToken)
        {
            _debugService.IsClientAttached = true;

            if (!string.IsNullOrEmpty(_debugStateService.ScriptToLaunch))
            {
                // NOTE: This is an unawaited task because responding to "configuration done" means
                // setting up the debugger, and in our case that means starting the script but not
                // waiting for it to finish.
                Task _ = LaunchScriptAsync(_debugStateService.ScriptToLaunch).HandleErrorsAsync(_logger);
            }

            if (_debugStateService.IsInteractiveDebugSession && _debugService.IsDebuggerStopped)
            {
                if (_debugService.CurrentDebuggerStoppedEventArgs is not null)
                {
                    // If this is an interactive session and there's a pending breakpoint, send that
                    // information along to the debugger client.
                    _debugEventHandlerService.TriggerDebuggerStopped(_debugService.CurrentDebuggerStoppedEventArgs);
                }
                else
                {
                    // If this is an interactive session and there's a pending breakpoint that has
                    // not been propagated through the debug service, fire the debug service's
                    // OnDebuggerStop event.
                    _debugService.OnDebuggerStopAsync(null, _debugContext.LastStopEventArgs);
                }
            }

            return Task.FromResult(new ConfigurationDoneResponse());
        }

        private async Task LaunchScriptAsync(string scriptToLaunch)
        {
            PSCommand command;
            if (ScriptFile.IsUntitledPath(scriptToLaunch))
            {
                ScriptFile untitledScript = _workspaceService.GetFile(scriptToLaunch);
                if (BreakpointApiUtils.SupportsBreakpointApis(_runspaceContext.CurrentRunspace))
                {
                    // Parse untitled files with their `Untitled:` URI as the filename which will
                    // cache the URI and contents within the PowerShell parser. By doing this, we
                    // light up the ability to debug untitled files with line breakpoints. This is
                    // only possible with PowerShell 7's new breakpoint APIs since the old API,
                    // Set-PSBreakpoint, validates that the given path points to a real file.
                    ScriptBlockAst ast = Parser.ParseInput(
                        untitledScript.Contents,
                        untitledScript.DocumentUri.ToString(),
                        out Token[] _,
                        out ParseError[] _);

                    // In order to use utilize the parser's cache (and therefore hit line
                    // breakpoints) we need to use the AST's `ScriptBlock` object. Due to
                    // limitations in PowerShell's public API, this means we must use the
                    // `PSCommand.AddArgument(object)` method, hence this hack where we dot-source
                    // `$args[0]. Fortunately the dot-source operator maintains a stack of arguments
                    // on each invocation, so passing the user's arguments directly in the initial
                    // `AddScript` surprisingly works.
                    command = PSCommandHelpers
                        .BuildDotSourceCommandWithArguments("$args[0]", _debugStateService.Arguments)
                        .AddArgument(ast.GetScriptBlock());
                }
                else
                {
                    // Without the new APIs we can only execute the untitled script's contents.
                    // Command breakpoints and `Wait-Debugger` will work.
                    command = PSCommandHelpers.BuildDotSourceCommandWithArguments(
                        string.Concat("{ ", untitledScript.Contents, " }"), _debugStateService.Arguments);
                }
            }
            else
            {
                // For a saved file we just execute its path (after escaping it).
                command = PSCommandHelpers.BuildDotSourceCommandWithArguments(
                    string.Concat('"', scriptToLaunch, '"'), _debugStateService.Arguments);
            }

            await _executionService.ExecutePSCommandAsync(
                command,
                CancellationToken.None,
                s_debuggerExecutionOptions).ConfigureAwait(false);
            _debugAdapterServer.SendNotification(EventNames.Terminated);
        }
    }
}
