// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    // TODO: Inherit from ABCs instead of satisfying interfaces.
    internal class DebuggerActionHandlers : IContinueHandler, INextHandler, IPauseHandler, IStepInHandler, IStepOutHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public DebuggerActionHandlers(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<ContinueHandlerBase>();
            _debugService = debugService;
        }

        public Task<ContinueResponse> Handle(ContinueArguments request, CancellationToken cancellationToken)
        {
            _debugService.Continue();
            return Task.FromResult(new ContinueResponse());
        }

        public Task<NextResponse> Handle(NextArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepOver();
            return Task.FromResult(new NextResponse());
        }

        public Task<PauseResponse> Handle(PauseArguments request, CancellationToken cancellationToken)
        {
            try
            {
                _debugService.Break();
                return Task.FromResult(new PauseResponse());
            }
            catch(NotSupportedException e)
            {
                throw new RpcErrorException(0, e.Message);
            }
        }

        public Task<StepInResponse> Handle(StepInArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepIn();
            return Task.FromResult(new StepInResponse());
        }

        public Task<StepOutResponse> Handle(StepOutArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepOut();
            return Task.FromResult(new StepOutResponse());
        }
    }
}
