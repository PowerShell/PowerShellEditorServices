//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    [DebuggerDisplay("RequestType Method = {Method}")]
    public class RequestType<TParams, TResult, TError, TRegistrationOption> : AbstractMessageType
    {
        private RequestType(string method) : base(method, 1)
        {

        }

        public static RequestType<TParams, TResult, TError, TRegistrationOption> ConvertToRequestType(
            RequestType0<TResult, TError, TRegistrationOption> requestType0)
        {
            return RequestType<TParams, TResult, TError, TRegistrationOption>.Create(requestType0.Method);
        }

        public static RequestType<TParams, TResult, TError, TRegistrationOption> Create(string method)
        {
            if (method == null)
            {
                throw new System.ArgumentNullException(nameof(method));
            }

            return new RequestType<TParams, TResult, TError, TRegistrationOption>(method);
        }
    }
}

