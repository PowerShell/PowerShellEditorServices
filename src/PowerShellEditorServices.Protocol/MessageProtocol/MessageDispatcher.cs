//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class MessageDispatcher : IMessageHandlers, IMessageDispatcher
    {
        #region Fields

        private ILogger logger;

        private Dictionary<string, Func<Message, MessageWriter, Task>> requestHandlers =
            new Dictionary<string, Func<Message, MessageWriter, Task>>();

        private Dictionary<string, Func<Message, MessageWriter, Task>> eventHandlers =
            new Dictionary<string, Func<Message, MessageWriter, Task>>();

        #endregion

        #region Constructors

        public MessageDispatcher(ILogger logger)
        {
            this.logger = logger;
        }

        #endregion

        #region Public Methods

        public void SetRequestHandler<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            Func<TParams, RequestContext<TResult>, Task> requestHandler)
        {
            bool overrideExisting = true;

            if (overrideExisting)
            {
                // Remove the existing handler so a new one can be set
                this.requestHandlers.Remove(requestType.Method);
            }

            this.requestHandlers.Add(
                requestType.Method,
                (requestMessage, messageWriter) =>
                {
                    var requestContext =
                        new RequestContext<TResult>(
                            requestMessage,
                            messageWriter);

                    TParams typedParams = default(TParams);
                    if (requestMessage.Contents != null)
                    {
                        // TODO: Catch parse errors!
                        typedParams = requestMessage.Contents.ToObject<TParams>();
                    }

                    return requestHandler(typedParams, requestContext);
                });
        }

        public void SetRequestHandler<TResult, TError, TRegistrationOptions>(
            RequestType0<TResult, TError, TRegistrationOptions> requestType0,
            Func<RequestContext<TResult>, Task> requestHandler)
        {
            this.SetRequestHandler(
                RequestType<Object, TResult, TError, TRegistrationOptions>.ConvertToRequestType(requestType0),
                (param1, requestContext) =>
                {
                    return requestHandler(requestContext);
                });
        }

        public void SetEventHandler<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            Func<TParams, EventContext, Task> eventHandler)
        {
            bool overrideExisting = true;

            if (overrideExisting)
            {
                // Remove the existing handler so a new one can be set
                this.eventHandlers.Remove(eventType.Method);
            }

            this.eventHandlers.Add(
                eventType.Method,
                (eventMessage, messageWriter) =>
                {
                    var eventContext = new EventContext(messageWriter);

                    TParams typedParams = default(TParams);
                    if (eventMessage.Contents != null)
                    {
                        // TODO: Catch parse errors!
                        typedParams = eventMessage.Contents.ToObject<TParams>();
                    }

                    return eventHandler(typedParams, eventContext);
                });
        }

        #endregion

        #region Private Methods

        public async Task DispatchMessageAsync(
            Message messageToDispatch,
            MessageWriter messageWriter)
        {
            Task handlerToAwait = null;

            if (messageToDispatch.MessageType == MessageType.Request)
            {
                Func<Message, MessageWriter, Task> requestHandler = null;
                if (this.requestHandlers.TryGetValue(messageToDispatch.Method, out requestHandler))
                {
                    handlerToAwait = requestHandler(messageToDispatch, messageWriter);
                }
                else
                {
                    // TODO: Message not supported error
                    this.logger.Write(LogLevel.Error, $"MessageDispatcher: No handler registered for Request type '{messageToDispatch.Method}'");
                }
            }
            else if (messageToDispatch.MessageType == MessageType.Event)
            {
                Func<Message, MessageWriter, Task> eventHandler = null;
                if (this.eventHandlers.TryGetValue(messageToDispatch.Method, out eventHandler))
                {
                    handlerToAwait = eventHandler(messageToDispatch, messageWriter);
                }
                else
                {
                    // TODO: Message not supported error
                    this.logger.Write(LogLevel.Error, $"MessageDispatcher: No handler registered for Event type '{messageToDispatch.Method}'");
                }
            }
            else
            {
                // TODO: Return message not supported
                this.logger.Write(LogLevel.Error, $"MessageDispatcher received unknown message type of method '{messageToDispatch.Method}'");
            }

            if (handlerToAwait != null)
            {
                try
                {
                    await handlerToAwait;
                }
                catch (TaskCanceledException)
                {
                    // Some tasks may be cancelled due to legitimate
                    // timeouts so don't let those exceptions go higher.
                }
                catch (AggregateException e)
                {
                    if (!(e.InnerExceptions[0] is TaskCanceledException))
                    {
                        // Cancelled tasks aren't a problem, so rethrow
                        // anything that isn't a TaskCanceledException
                        throw e;
                    }
                }
            }
        }

        #endregion
    }
}
