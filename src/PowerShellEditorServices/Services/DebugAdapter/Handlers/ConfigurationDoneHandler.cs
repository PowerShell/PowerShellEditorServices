// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly IRunspaceContext _runspaceContext;

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

            if (_debugStateService.OwnsEditorSession)
            {
                // If this is a debug-only session, we need to start
                // the command loop manually
                //_powerShellContextService.ConsoleReader.StartCommandLoop();
            }

            if (!string.IsNullOrEmpty(_debugStateService.ScriptToLaunch))
            {
                LaunchScriptAsync(_debugStateService.ScriptToLaunch)
                    .HandleErrorsAsync(_logger);
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
                        _debugService.OnDebuggerStopAsync(null, _debugContext.LastStopEventArgs);
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

                if (BreakpointApiUtils.SupportsBreakpointApis(_runspaceContext.CurrentRunspace))
                {
                    // Parse untitled files with their `Untitled:` URI as the file name which will cache the URI & contents within the PowerShell parser.
                    // By doing this, we light up the ability to debug Untitled files with breakpoints.
                    // This is only possible via the direct usage of the breakpoint APIs in PowerShell because
                    // Set-PSBreakpoint validates that paths are actually on the filesystem.
                    ScriptBlockAst ast = Parser.ParseInput(untitledScript.Contents, untitledScript.DocumentUri.ToString(), out Token[] tokens, out ParseError[] errors);

                    // This seems to be the simplest way to invoke a script block (which contains breakpoint information) via the PowerShell API.
                    var cmd = new PSCommand().AddScript(". $args[0]").AddArgument(ast.GetScriptBlock());
                    await _executionService
                        .ExecutePSCommandAsync<object>(cmd, CancellationToken.None, s_debuggerExecutionOptions)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _executionService
                        .ExecutePSCommandAsync(
                            new PSCommand().AddScript(untitledScript.Contents),
                            CancellationToken.None,
                            s_debuggerExecutionOptions)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await _executionService
                    .ExecutePSCommandAsync(
                        PSCommandHelpers.BuildPSCommandFromArguments(scriptToLaunch, _debugStateService.Arguments),
                        CancellationToken.None,
                        s_debuggerExecutionOptions)
                    .ConfigureAwait(false);
            }

            _debugAdapterServer.SendNotification(EventNames.Terminated);
        }
    }
}
