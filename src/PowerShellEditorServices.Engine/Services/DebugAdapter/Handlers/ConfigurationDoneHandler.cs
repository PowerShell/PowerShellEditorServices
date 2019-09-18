//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Engine.Services;
using Microsoft.PowerShell.EditorServices.Engine.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Engine.Services.TextDocument;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Engine.Handlers
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
            _logger = loggerFactory.CreateLogger<SetFunctionBreakpointsHandler>();
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
                if (_debugStateService.OwnsEditorSession)
                {
                    // If this is a debug-only session, we need to start
                    // the command loop manually
                    // TODO: Bring this back
                    //_editorSession.HostInput.StartCommandLoop();
                }

                if (_debugService.IsDebuggerStopped)
                {
                    // If this is an interactive session and there's a pending breakpoint,
                    // send that information along to the debugger client
                    _debugEventHandlerService.TriggerDebuggerStopped(_debugService.CurrentDebuggerStoppedEventArgs);
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

                await _powerShellContextService
                    .ExecuteScriptStringAsync(untitledScript.Contents, true, true);
            }
            else
            {
                await _powerShellContextService
                    .ExecuteScriptWithArgsAsync(scriptToLaunch, _debugStateService.Arguments, writeInputToHost: true);
            }

            _jsonRpcServer.SendNotification(EventNames.Terminated);
        }
    }
}
