//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.Client;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class ServerTestsBase
    {
        protected ProtocolClient protocolClient;

        private ConcurrentDictionary<string, AsyncQueue<object>> eventQueuePerType =
            new ConcurrentDictionary<string, AsyncQueue<object>>();

        protected Task<TResult> SendRequest<TParams, TResult>(
            RequestType<TParams, TResult> requestType, 
            TParams requestParams)
        {
            return 
                this.protocolClient.SendRequest(
                    requestType, 
                    requestParams);
        }

        protected Task SendEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            return 
                this.protocolClient.SendEvent(
                    eventType,
                    eventParams);
        }

        protected void QueueEventsForType<TParams>(EventType<TParams> eventType)
        {
            var eventQueue =
                this.eventQueuePerType.AddOrUpdate(
                    eventType.MethodName,
                    new AsyncQueue<object>(),
                    (key, queue) => queue);

            this.protocolClient.SetEventHandler(
                eventType,
                (p, ctx) =>
                {
                    return eventQueue.EnqueueAsync(p);   
                });
        }

        protected async Task<TParams> WaitForEvent<TParams>(
            EventType<TParams> eventType,
            int timeoutMilliseconds = 5000)
        {
            Task<TParams> eventTask = null;

            // Use the event queue if one has been registered
            AsyncQueue<object> eventQueue = null;
            if (this.eventQueuePerType.TryGetValue(eventType.MethodName, out eventQueue))
            {
                eventTask =
                    eventQueue
                        .DequeueAsync()
                        .ContinueWith<TParams>(
                            task => (TParams)task.Result);
            }
            else
            {
                TaskCompletionSource<TParams> eventTaskSource = new TaskCompletionSource<TParams>();

                this.protocolClient.SetEventHandler(
                    eventType,
                    (p, ctx) =>
                    {
                        if (!eventTaskSource.Task.IsCompleted)
                        {
                            eventTaskSource.SetResult(p);
                        }

                        return Task.FromResult(true);
                    },
                    true);  // Override any existing handler

                eventTask = eventTaskSource.Task;
            }

            await 
                Task.WhenAny(
                    eventTask,
                    Task.Delay(timeoutMilliseconds));

            if (!eventTask.IsCompleted)
            {
                throw new TimeoutException(
                    string.Format(
                        "Timed out waiting for '{0}' event!",
                        eventType.MethodName));
            }

            return await eventTask;
        }
    }
}

