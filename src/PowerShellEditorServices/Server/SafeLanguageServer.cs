// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Server
{
    /// <summary>
    /// An LSP server that ensures that client/server initialization has occurred
    /// before sending or receiving any other notifications or requests.
    /// </summary>
    internal interface ISafeLanguageServer : IResponseRouter
    {
        ITextDocumentLanguageServer TextDocument { get; }

        IClientLanguageServer Client { get; }

        IGeneralLanguageServer General { get; }

        IWindowLanguageServer Window { get; }

        IWorkspaceLanguageServer Workspace { get; }
    }

    /// <summary>
    /// An implementation around Omnisharp's LSP server to ensure
    /// messages are not sent before initialization has completed.
    /// </summary>
    internal class SafeLanguageServer : ISafeLanguageServer
    {
        private readonly ILanguageServerFacade _languageServer;

        private readonly AsyncLatch _serverReady;

        private readonly ConcurrentQueue<Action> _notificationQueue;

        public SafeLanguageServer(ILanguageServerFacade languageServer)
        {
            _languageServer = languageServer;
            _serverReady = new AsyncLatch();
            _notificationQueue = new ConcurrentQueue<Action>();
        }

        public ITextDocumentLanguageServer TextDocument
        {
            get
            {
                _serverReady.Wait();
                return _languageServer.TextDocument;
            }
        }

        public IClientLanguageServer Client
        {
            get
            {
                _serverReady.Wait();
                return _languageServer.Client;
            }
        }

        public IGeneralLanguageServer General
        {
            get
            {
                _serverReady.Wait();
                return _languageServer.General;
            }
        }

        public IWindowLanguageServer Window
        {
            get
            {
                _serverReady.Wait();
                return _languageServer.Window;
            }
        }

        public IWorkspaceLanguageServer Workspace
        {
            get
            {
                _serverReady.Wait();
                return _languageServer.Workspace;
            }
        }

        public void SetReady()
        {
            _serverReady.Open();

            // Send any pending notifications now
            while (_notificationQueue.TryDequeue(out Action notifcationAction))
            {
                notifcationAction();
            }
        }

        public void SendNotification(string method)
        {
            if (!_serverReady.IsReady)
            {
                _notificationQueue.Enqueue(() => _languageServer.SendNotification(method));
                return;
            }

            _languageServer.SendNotification(method);
        }

        public void SendNotification<T>(string method, T @params)
        {
            if (!_serverReady.IsReady)
            {
                _notificationQueue.Enqueue(() => _languageServer.SendNotification(method, @params));
                return;
            }

            _languageServer.SendNotification(method, @params);
        }

        public void SendNotification(IRequest request)
        {
            if (!_serverReady.IsReady)
            {
                _notificationQueue.Enqueue(() => _languageServer.SendNotification(request));
                return;
            }

            _languageServer.SendNotification(request);
        }

        public IResponseRouterReturns SendRequest<T>(string method, T @params)
        {
            _serverReady.Wait();
            return _languageServer.SendRequest(method, @params);
        }

        public IResponseRouterReturns SendRequest(string method)
        {
            _serverReady.Wait();
            return _languageServer.SendRequest(method);
        }

        public async Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            await _serverReady.WaitAsync();
            return await _languageServer.SendRequest(request, cancellationToken);
        }

        public bool TryGetRequest(long id, out string method, out TaskCompletionSource<JToken> pendingTask)
        {
            if (!_serverReady.IsReady)
            {
                method = default;
                pendingTask = default;
                return false;
            }

            return _languageServer.TryGetRequest(id, out method, out pendingTask);
        }

        /// <summary>
        /// Implements a latch (a monotonic manual reset event that starts in the blocking state)
        /// that can be waited on synchronously or asynchronously without wasting thread resources.
        /// </summary>
        private class AsyncLatch
        {
            private readonly ManualResetEvent _resetEvent;

            private readonly Task _awaitLatchOpened;

            private volatile bool _isOpen;

            public AsyncLatch()
            {
                _resetEvent = new ManualResetEvent(/* start in blocking state */ initialState: false);
                _awaitLatchOpened = CreateLatchOpenedAwaiterTask(_resetEvent);
                _isOpen = false;
            }

            public bool IsReady => _isOpen;

            public void Wait() => _resetEvent.WaitOne();

            public Task WaitAsync() => _awaitLatchOpened;

            public void Open()
            {
                // Unblocks the reset event
                _resetEvent.Set();
                _isOpen = true;
            }

            private static Task CreateLatchOpenedAwaiterTask(WaitHandle handle)
            {
                var tcs = new TaskCompletionSource<object>();

                // In a dedicated waiter thread, wait for the reset event and then set the task completion source
                // to turn the reset event wait into a task.
                // From https://stackoverflow.com/a/18766131.
                RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject(handle, (state, timedOut) =>
                {
                    ((TaskCompletionSource<object>)state).TrySetResult(result: null);
                }, tcs, Timeout.Infinite, executeOnlyOnce: true);

                // Register an action to unregister the registration when the reset event task has completed.
                EnsureWaitHandleUnregistered(tcs.Task, registration);

                return tcs.Task;
            }

            private static async Task EnsureWaitHandleUnregistered(Task task, RegisteredWaitHandle handle)
            {
                try
                {
                    await task;
                }
                finally
                {
                    handle.Unregister(waitObject: null);
                }
            }
        }
    }
}
