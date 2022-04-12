// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class SourceHandler : ISourceHandler
    {
        public Task<SourceResponse> Handle(SourceArguments request, CancellationToken cancellationToken) =>
            // TODO: Implement this message.  For now, doesn't seem to
            // be a problem that it's missing.
            Task.FromResult(new SourceResponse());
    }
}
