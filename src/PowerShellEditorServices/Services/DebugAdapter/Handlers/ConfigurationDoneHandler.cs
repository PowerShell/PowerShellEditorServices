//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ConfigurationDoneHandler : IConfigurationDoneHandler
    {
        private readonly ILogger _logger;
        private readonly IJsonRpcServer _jsonRpcServer;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly PowerShellContextService _powerShellContextService;
        private readonly WorkspaceService _workspaceService;

        public ConfigurationDoneHandler(
            ILoggerFactory loggerFactory,
            IJsonRpcServer jsonRpcServer,
            DebugService debugService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService,
            PowerShellContextService powerShellContextService,
            WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<ConfigurationDoneHandler>();
            _jsonRpcServer = jsonRpcServer;
            _debugService = debugService;
            _debugStateService = debugStateService;
            _debugEventHandlerService = debugEventHandlerService;
            _powerShellContextService = powerShellContextService;
            _workspaceService = workspaceService;
        }

        public Task<ConfigurationDoneResponse> Handle(ConfigurationDoneArguments request, CancellationToken cancellationToken)
        {
            _debugService.IsClientAttached = true;

            if (_debugStateService.OwnsEditorSession)
            {
                // If this is a debug-only session, we need to start
                // the command loop manually
                _powerShellContextService.ConsoleReader.StartCommandLoop();
            }

            if (!string.IsNullOrEmpty(_debugStateService.ScriptToLaunch))
            {
                if (_powerShellContextService.SessionState == PowerShellContextState.Ready)
                {
                    // Configuration is done, launch the script
                    var nonAwaitedTask = LaunchScriptAsync(_debugStateService.ScriptToLaunch)
                        .ConfigureAwait(continueOnCapturedContext: false);
                }
                else
                {
                    _logger.LogTrace("configurationDone request called after script was already launched, skipping it.");
                }
            }

            if (_debugStateService.IsInteractiveDebugSession)
            {
                if (_debugService.IsDebuggerStopped)
                {
                    if (_debugService.CurrentDebuggerStoppedEventArgs != null)
                    {
                        // If this is an interactive session and there's a pending breakpoint,
                        // send that information along to the debugger client
                        _debugEventHandlerService.TriggerDebuggerStopped(_debugService.CurrentDebuggerStoppedEventArgs);
                    }
                    else
                    {
                        // If this is an interactive session and there's a pending breakpoint that has not been propagated through
                        // the debug service, fire the debug service's OnDebuggerStop event.
                        _debugService.OnDebuggerStopAsync(null, _powerShellContextService.CurrentDebuggerStopEventArgs);
                    }
                }
            }

            return Task.FromResult(new ConfigurationDoneResponse());
        }

        private async Task LaunchScriptAsync(string scriptToLaunch)
        {
            // Is this an untitled script?
            if (ScriptFile.IsUntitledPath(scriptToLaunch))
            {
                ScriptFile untitledScript = _workspaceService.GetFile(scriptToLaunch);

                if (BreakpointApiUtils.SupportsBreakpointApis)
                {
                    // Parse untitled files with their `Untitled:` URI as the file name which will cache the URI & contents within the PowerShell parser.
                    // By doing this, we light up the ability to debug Untitled files with breakpoints.
                    // This is only possible via the direct usage of the breakpoint APIs in PowerShell because
                    // Set-PSBreakpoint validates that paths are actually on the filesystem.
                    ScriptBlockAst ast = Parser.ParseInput(untitledScript.Contents, untitledScript.DocumentUri.ToString(), out Token[] tokens, out ParseError[] errors);

                    // This seems to be the simplest way to invoke a script block (which contains breakpoint information) via the PowerShell API.
                    var cmd = new PSCommand().AddScript(". $args[0]").AddArgument(ast.GetScriptBlock());
                    await _powerShellContextService
                        .ExecuteCommandAsync<object>(cmd, sendOutputToHost: true, sendErrorToHost:true)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _powerShellContextService
                        .ExecuteScriptStringAsync(untitledScript.Contents, writeInputToHost: true, writeOutputToHost: true)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await _powerShellContextService
                    .ExecuteScriptWithArgsAsync(scriptToLaunch, _debugStateService.Arguments, writeInputToHost: true).ConfigureAwait(false);
            }

            _jsonRpcServer.SendNotification(EventNames.Terminated);
        }
    }
}
