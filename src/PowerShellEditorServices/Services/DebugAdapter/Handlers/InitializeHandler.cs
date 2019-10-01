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

        public InitializeHandler(
            ILoggerFactory factory,
            DebugService debugService)
        {
            _logger = factory.CreateLogger<InitializeHandler>();
            _debugService = debugService;
        }

        public async Task<InitializeResponse> Handle(InitializeRequestArguments request, CancellationToken cancellationToken)
        {
            // Clear any existing breakpoints before proceeding
            await _debugService.ClearAllBreakpointsAsync();

            // Now send the Initialize response to continue setup
            return new InitializeResponse
                {
                    SupportsConfigurationDoneRequest = true,
                    SupportsFunctionBreakpoints = true,
                    SupportsConditionalBreakpoints = true,
                    SupportsHitConditionalBreakpoints = true,
                    SupportsSetVariable = true
                };
        }
    }
}
