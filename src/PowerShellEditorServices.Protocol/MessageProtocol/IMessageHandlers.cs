//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public interface IMessageHandlers
    {
        void SetRequestHandler<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            Func<TParams, RequestContext<TResult>, Task> requestHandler);

        void SetRequestHandler<TResult, TError, TRegistrationOptions>(
            RequestType0<TResult, TError, TRegistrationOptions> requestType0,
            Func<RequestContext<TResult>, Task> requestHandler);

        void SetEventHandler<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            Func<TParams, EventContext, Task> eventHandler);
    }
}
