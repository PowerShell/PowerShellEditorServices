// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ScopesHandler : IScopesHandler
    {
        private readonly DebugService _debugService;

        public ScopesHandler(DebugService debugService) => _debugService = debugService;

        public Task<ScopesResponse> Handle(ScopesArguments request, CancellationToken cancellationToken)
        {
            VariableScope[] variableScopes =
                _debugService.GetVariableScopes(
                    (int)request.FrameId);

            return Task.FromResult(new ScopesResponse
            {
                Scopes = new Container<Scope>(variableScopes
                    .Select(LspDebugUtils.CreateScope))
            });
        }
    }
}
