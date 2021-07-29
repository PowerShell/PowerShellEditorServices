// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using Thread = OmniSharp.Extensions.DebugAdapter.Protocol.Models.Thread;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class ThreadsHandler : IThreadsHandler
    {
        internal static Thread PipelineThread { get; } =
            new Thread { Id = 1, Name = "PowerShell Pipeline Thread" };

        public Task<ThreadsResponse> Handle(ThreadsArguments request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ThreadsResponse
            {
                // TODO: OmniSharp supports multithreaded debugging (where
                // multiple threads can be debugged at once), but we don't. This
                // means we always need to set AllThreadsStopped and
                // AllThreadsContinued in our events. But if we one day support
                // multithreaded debugging, we'd need a way to associate
                // debugged runspaces with .NET threads in a consistent way.
                Threads = new Container<Thread>(PipelineThread)
            });
        }
    }
}
