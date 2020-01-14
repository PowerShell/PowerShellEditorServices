//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ThreadsHandler : IThreadsHandler
    {
        public Task<ThreadsResponse> Handle(ThreadsArguments request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ThreadsResponse
            {
                // TODO: What do I do with these?
                Threads = new Container<OmniSharp.Extensions.DebugAdapter.Protocol.Models.Thread>(
                    new OmniSharp.Extensions.DebugAdapter.Protocol.Models.Thread
                    {
                        Id = 1,
                        Name = "Main Thread"
                    })
            });
        }
    }
}
