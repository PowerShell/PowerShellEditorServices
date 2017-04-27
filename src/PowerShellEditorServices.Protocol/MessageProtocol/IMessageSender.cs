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
            NotificationType<TParams> eventType,
            TParams eventParams);

        Task<TResult> SendRequest<TParams, TResult, TError, TRegistrationOption>(
            RequestType<TParams, TResult, TError, TRegistrationOption> requestType,
            TParams requestParams,
            bool waitForResponse);
    }
}

