// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.DebugAdapter.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ConfigurationDoneHandler : IConfigurationDoneHandler
    {
        private static readonly PowerShellExecutionOptions s_debuggerExecutionOptions = new()
        {
            MustRunInForeground = true,
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

        public ConfigurationDoneHandler(
            ILoggerFactory loggerFactory,
            IDebugAdapterServerFacade debugAdapterServer,
            DebugService debugService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService,
            IInternalPowerShellExecutionService executionService,
            WorkspaceService workspaceService,
            IPowerShellDebugContext debugContext)
        {
            _logger = loggerFactory.CreateLogger<ConfigurationDoneHandler>();
            _debugAdapterServer = debugAdapterServer;
            _debugService = debugService;
            _debugStateService = debugStateService;
            _debugEventHandlerService = debugEventHandlerService;
            _executionService = executionService;
            _workspaceService = workspaceService;
            _debugContext = debugContext;
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
            // TODO: Theoretically we can make PowerShell respect line breakpoints in untitled
            // files, but the previous method was a hack that conflicted with correct passing of
            // arguments to the debugged script. We are prioritizing the latter over the former, as
            // command breakpoints and `Wait-Debugger` work fine.
            string command = ScriptFile.IsUntitledPath(scriptToLaunch)
                ? string.Concat("{ ", _workspaceService.GetFile(scriptToLaunch).Contents, " }")
                : string.Concat('"', scriptToLaunch, '"');

            await _executionService.ExecutePSCommandAsync(
                PSCommandHelpers.BuildCommandFromArguments(command, _debugStateService.Arguments),
                CancellationToken.None,
                s_debuggerExecutionOptions).ConfigureAwait(false);
            _debugAdapterServer.SendNotification(EventNames.Terminated);
        }
    }
}
