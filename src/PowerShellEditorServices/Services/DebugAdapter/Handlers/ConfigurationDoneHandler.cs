// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
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
        private readonly PowerShellExecutionService _executionService;
        private readonly WorkspaceService _workspaceService;

        private readonly IPowerShellDebugContext _debugContext;

        public ConfigurationDoneHandler(
            ILoggerFactory loggerFactory,
            IDebugAdapterServerFacade debugAdapterServer,
            DebugService debugService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService,
            PowerShellExecutionService executionService,
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

                if (BreakpointApiUtils.SupportsBreakpointApis)
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
                        BuildPSCommandFromArguments(scriptToLaunch, _debugStateService.Arguments),
                        CancellationToken.None,
                        s_debuggerExecutionOptions)
                    .ConfigureAwait(false);
            }

            _debugAdapterServer.SendNotification(EventNames.Terminated);
        }

        private PSCommand BuildPSCommandFromArguments(string command, IReadOnlyList<string> arguments)
        {
            if (arguments is null or { Count: 0 })
            {
                return new PSCommand().AddCommand(command);
            }

            // We are forced to use a hack here so that we can reuse PowerShell's parameter binding
            var sb = new StringBuilder()
                .Append("& '")
                .Append(command.Replace("'", "''"))
                .Append("'");

            foreach (string arg in arguments)
            {
                sb.Append(' ');

                if (ArgumentNeedsEscaping(arg))
                {
                    sb.Append('\'').Append(arg.Replace("'", "''")).Append('\'');
                }
                else
                {
                    sb.Append(arg);
                }
            }

            return new PSCommand().AddScript(sb.ToString());
        }

        private bool ArgumentNeedsEscaping(string argument)
        {
            foreach (char c in argument)
            {
                switch (c)
                {
                    case '\'':
                    case '"':
                    case '|':
                    case '&':
                    case ';':
                    case ':':
                    case char w when char.IsWhiteSpace(w):
                        return true;
                }
            }

            return false;
        }
    }
}
