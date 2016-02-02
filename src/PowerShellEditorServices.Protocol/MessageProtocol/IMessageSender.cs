//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    internal interface IMessageSender
    {
        Task SendEvent<TParams>(
            EventType<TParams> eventType,
            TParams eventParams);

        Task<TResult> SendRequest<TParams, TResult>(
            RequestType<TParams, TResult> requestType,
            TParams requestParams,
            bool waitForResponse);
    }
}

