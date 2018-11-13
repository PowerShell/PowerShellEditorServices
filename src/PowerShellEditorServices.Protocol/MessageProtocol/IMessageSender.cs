//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public interface IMessageSender
    {
        Task SendEventAsync<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            TParams eventParams);

        Task<TResult> SendRequestAsync<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            TParams requestParams,
            bool waitForResponse);

        Task<TResult> SendRequestAsync<TResult, TError, TRegistrationOptions>(
            RequestType0<TResult, TError, TRegistrationOptions> requestType0);
    }
}

