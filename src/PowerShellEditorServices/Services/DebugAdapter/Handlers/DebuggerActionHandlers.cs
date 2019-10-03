//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ContinueHandler : IContinueHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public ContinueHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<ContinueHandler>();
            _debugService = debugService;
        }

        public Task<ContinueResponse> Handle(ContinueArguments request, CancellationToken cancellationToken)
        {
            _debugService.Continue();
            return Task.FromResult(new ContinueResponse());
        }
    }

    internal class NextHandler : INextHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public NextHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<NextHandler>();
            _debugService = debugService;
        }

        public Task<NextResponse> Handle(NextArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepOver();
            return Task.FromResult(new NextResponse());
        }
    }

    internal class PauseHandler : IPauseHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public PauseHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<PauseHandler>();
            _debugService = debugService;
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
    }

    internal class StepInHandler : IStepInHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public StepInHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<StepInHandler>();
            _debugService = debugService;
        }

        public Task<StepInResponse> Handle(StepInArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepIn();
            return Task.FromResult(new StepInResponse());
        }
    }

    internal class StepOutHandler : IStepOutHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public StepOutHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<StepOutHandler>();
            _debugService = debugService;
        }

        public Task<StepOutResponse> Handle(StepOutArguments request, CancellationToken cancellationToken)
        {
            _debugService.StepOut();
            return Task.FromResult(new StepOutResponse());
        }
    }
}
