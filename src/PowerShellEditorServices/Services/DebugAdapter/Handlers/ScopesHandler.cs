//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ScopesHandler : IScopesHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public ScopesHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<ScopesHandler>();
            _debugService = debugService;
        }

        public Task<ScopesResponse> Handle(ScopesArguments request, CancellationToken cancellationToken)
        {
            VariableScope[] variableScopes =
                _debugService.GetVariableScopes(
                    (int) request.FrameId);

            return Task.FromResult(new ScopesResponse
            {
                Scopes = new Container<Scope>(variableScopes
                    .Select(LspDebugUtils.CreateScope))
            });
        }
    }
}
