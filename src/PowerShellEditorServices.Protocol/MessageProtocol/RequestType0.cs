//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    [DebuggerDisplay("RequestType0 Method = {Method}")]
    public class RequestType0<TResult, TError, TRegistrationOption> : AbstractMessageType
    {
        public RequestType0(string method) : base(method, 0)
        {
        }

        public static RequestType0<TResult, TError, TRegistrationOption> Create(string method)
        {
            return new RequestType0<TResult, TError, TRegistrationOption>(method);
        }
    }
}
