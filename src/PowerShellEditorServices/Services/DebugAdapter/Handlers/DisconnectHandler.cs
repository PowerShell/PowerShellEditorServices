// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Server;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class DisconnectHandler : IDisconnectHandler
    {
        private readonly ILogger<DisconnectHandler> _logger;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly DebugService _debugService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly PsesDebugServer _psesDebugServer;
        private readonly IRunspaceContext _runspaceContext;

        public DisconnectHandler(
            ILoggerFactory factory,
            PsesDebugServer psesDebugServer,
            IRunspaceContext runspaceContext,
            IInternalPowerShellExecutionService executionService,
            DebugService debugService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService)
        {
            _logger = factory.CreateLogger<DisconnectHandler>();
            _psesDebugServer = psesDebugServer;
            _runspaceContext = runspaceContext;
            _executionService = executionService;
            _debugService = debugService;
            _debugStateService = debugStateService;
            _debugEventHandlerService = debugEventHandlerService;
        }

        public async Task<DisconnectResponse> Handle(DisconnectArguments request, CancellationToken cancellationToken)
        {
            // TODO: We need to sort out the proper order of operations here.
            //       Currently we just tear things down in some order without really checking what the debugger is doing.
            //       We should instead ensure that the debugger is in some valid state, lock it and then tear things down

            _debugEventHandlerService.UnregisterEventHandlers();

            if (!_debugStateService.ExecutionCompleted)
            {
                _debugStateService.ExecutionCompleted = true;
                _debugService.Abort();

                if (_debugStateService.IsInteractiveDebugSession && _debugStateService.IsAttachSession)
                {
                    // Pop the sessions
                    if (_runspaceContext.CurrentRunspace.RunspaceOrigin == RunspaceOrigin.EnteredProcess)
                    {
                        try
                        {
                            await _executionService.ExecutePSCommandAsync(
                                new PSCommand().AddCommand("Exit-PSHostProcess"),
                                CancellationToken.None).ConfigureAwait(false);

                            if (_debugStateService.IsRemoteAttach)
                            {
                                await _executionService.ExecutePSCommandAsync(
                                    new PSCommand().AddCommand("Exit-PSSession"),
                                    CancellationToken.None).ConfigureAwait(false);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogException("Caught exception while popping attached process after debugging", e);
                        }
                    }
                }
                _debugService.IsClientAttached = false;
            }

            _logger.LogInformation("Debug adapter is shutting down...");

#pragma warning disable CS4014
            // Trigger the clean up of the debugger. No need to wait for it nor cancel it.
            Task.Run(_psesDebugServer.OnSessionEnded, CancellationToken.None);
#pragma warning restore CS4014

            return new DisconnectResponse();
        }
    }
}
