using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class MessageDispatcher<TSession>
    {
        private Dictionary<string, Func<Message, TSession, MessageWriter, Task>> requestHandlers =
            new Dictionary<string, Func<Message, TSession, MessageWriter, Task>>();

        private Dictionary<string, Func<Message, TSession, MessageWriter, Task>> eventHandlers =
            new Dictionary<string, Func<Message, TSession, MessageWriter, Task>>();

        public void AddRequestHandler<TParams, TResult, TError>(
            RequestType<TParams, TResult, TError> requestType,
            Func<TParams, TSession, RequestContext<TResult, TError>, Task> requestHandler)
        {
            // TODO: Error or replace existing handler?

            this.requestHandlers.Add(
                requestType.TypeName,
                (requestMessage, session, messageWriter) =>
                {
                    var requestContext =
                        new RequestContext<TResult, TError>(
                            requestMessage,
                            messageWriter);

                    TParams typedParams = default(TParams);
                    if (requestMessage.Contents != null)
                    {
                        // TODO: Catch parse errors!
                        typedParams = requestMessage.Contents.ToObject<TParams>();
                    }

                    return requestHandler(typedParams, session, requestContext);
                });
        }

        public void AddEventHandler<TParams>(
            EventType<TParams> eventType,
            Func<TParams, TSession, EventContext, Task> eventHandler)
        {
            this.eventHandlers.Add(
                eventType.MethodName,
                (eventMessage, session, messageWriter) =>
                {
                    var eventContext = new EventContext(messageWriter);

                    TParams typedParams = default(TParams);
                    if (eventMessage.Contents != null)
                    {
                        // TODO: Catch parse errors!
                        typedParams = eventMessage.Contents.ToObject<TParams>();
                    }

                    return eventHandler(typedParams, session, eventContext);
                });
        }

        public async Task DispatchMessage(
            Message messageToDispatch, 
            TSession sessionContext, 
            MessageWriter messageWriter)
        {
            if (messageToDispatch.MessageType == MessageType.Request)
            {
                Func<Message, TSession, MessageWriter, Task> requestHandler = null;
                if (this.requestHandlers.TryGetValue(messageToDispatch.Method, out requestHandler))
                {
                    await requestHandler(messageToDispatch, sessionContext, messageWriter);
                }
                else
                {
                    // TODO: Message not supported error
                }
            }
            else if (messageToDispatch.MessageType == MessageType.Event)
            {
                Func<Message, TSession, MessageWriter, Task> eventHandler = null;
                if (this.eventHandlers.TryGetValue(messageToDispatch.Method, out eventHandler))
                {
                    await eventHandler(messageToDispatch, sessionContext, messageWriter);
                }
                else
                {
                    // TODO: Message not supported error
                }
            }
            else
            {
                // TODO: Return message not supported
            }
        }
    }
}
