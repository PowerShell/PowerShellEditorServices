//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class InitializeHandler : IInitializeHandler
    {
        private readonly ILogger<InitializeHandler> _logger;
        private readonly DebugService _debugService;
        private readonly BreakpointService _breakpointService;

        public InitializeHandler(
            ILoggerFactory factory,
            DebugService debugService,
            BreakpointService breakpointService)
        {
            _logger = factory.CreateLogger<InitializeHandler>();
            _debugService = debugService;
            _breakpointService = breakpointService;
        }

        public async Task<InitializeResponse> Handle(InitializeRequestArguments request, CancellationToken cancellationToken)
        {
            // Clear any existing breakpoints before proceeding
            await _breakpointService.RemoveAllBreakpointsAsync().ConfigureAwait(false);

            // Now send the Initialize response to continue setup
            return new InitializeResponse
                {
                    SupportsConditionalBreakpoints = true,
                    SupportsConfigurationDoneRequest = true,
                    SupportsFunctionBreakpoints = true,
                    SupportsHitConditionalBreakpoints = true,
                    SupportsLogPoints = true,
                    SupportsSetVariable = true
                };
        }
    }
}
