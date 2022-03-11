// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ContinueHandler : ContinueHandlerBase
    {
        private readonly DebugService _debugService;

        public ContinueHandler(DebugService debugService) => _debugService = debugService;

        public override Task<ContinueResponse> Handle(ContinueArguments request, CancellationToken cancellationToken)
        {
            _debugService.Continue();
            return Task.FromResult(new ContinueResponse());
        }
    }

    internal class NextHandler : NextHandlerBase
    {
        private readonly DebugService _debugService;

        public NextHandler(DebugService debugService) => _debugService = debugService;

        public override Task<NextResponse> Handle(NextArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepOver();
            return Task.FromResult(new NextResponse());
        }
    }

    internal class PauseHandler : PauseHandlerBase
    {
        private readonly DebugService _debugService;

        public PauseHandler(DebugService debugService) => _debugService = debugService;

        public override Task<PauseResponse> Handle(PauseArguments request, CancellationToken cancellationToken)
        {
            try
            {
                _debugService.Break();
                return Task.FromResult(new PauseResponse());
            }
            catch (NotSupportedException e)
            {
                throw new RpcErrorException(0, e.Message);
            }
        }
    }

    internal class StepInHandler : StepInHandlerBase
    {
        private readonly DebugService _debugService;

        public StepInHandler(DebugService debugService) => _debugService = debugService;

        public override Task<StepInResponse> Handle(StepInArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepIn();
            return Task.FromResult(new StepInResponse());
        }
    }

    internal class StepOutHandler : StepOutHandlerBase
    {
        private readonly DebugService _debugService;

        public StepOutHandler(DebugService debugService) => _debugService = debugService;

        public override Task<StepOutResponse> Handle(StepOutArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepOut();
            return Task.FromResult(new StepOutResponse());
        }
    }
}
