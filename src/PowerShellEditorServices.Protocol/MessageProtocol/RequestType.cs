//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    [DebuggerDisplay("RequestType MethodName = {MethodName}")]
    public class RequestType<TParams, TResult, TError, TRegistrationOption>
    {
        public string MethodName { get; private set; }

        public static RequestType<TParams, TResult, TError, TRegistrationOption> ConvertToRequestType(
            RequestType0<TResult, TError, TRegistrationOption> requestType0)
        {
            return RequestType<TParams, TResult, TError, TRegistrationOption>.Create(requestType0.Method);
        }

        public static RequestType<TParams, TResult, TError, TRegistrationOption> Create(string typeName)
        {
            if (typeName == null)
            {
                throw new System.ArgumentNullException(nameof(typeName));
            }

            return new RequestType<TParams, TResult, TError, TRegistrationOption>()
            {
                MethodName = typeName
            };
        }
    }
}

