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

        /// <summary>
        /// Retrieves the variable scopes (containers) for the currently selected stack frame. Variables details are fetched via a separate request.
        /// </summary>
        public Task<ScopesResponse> Handle(ScopesArguments request, CancellationToken cancellationToken)
        {
            // HACK: The StackTraceHandler injects an artificial label frame as the first frame as a performance optimization, so when scopes are requested by the client, we need to adjust the frame index accordingly to match the underlying PowerShell frame, so when the client clicks on the label (or hit the default breakpoint), they get variables populated from the top of the PowerShell stackframe. If the client dives deeper, we need to reflect that as well (though 90% of debug users don't actually investigate this)
            // VSCode Frame 0 (Label) -> PowerShell StackFrame 0 (for convenience)
            // VSCode Frame 1 (First Real PS Frame) -> Also PowerShell StackFrame 0
            // VSCode Frame 2 -> PowerShell StackFrame 1
            // VSCode Frame 3 -> PowerShell StackFrame 2
            // etc.
            int powershellFrameId = request.FrameId == 0 ? 0 : (int)request.FrameId - 1;

            VariableScope[] variableScopes = _debugService.GetVariableScopes(powershellFrameId);

            return Task.FromResult(new ScopesResponse
            {
                Scopes = new Container<Scope>(
                    variableScopes
                    .Select(LspDebugUtils.CreateScope)
                )
            });
        }
    }
}
