//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    [DebuggerDisplay("RequestType MethodName = {MethodName}")]
    public class RequestType<TParams, TResult>
    {
        public string MethodName { get; private set; }

        public static RequestType<TParams, TResult> Create(string typeName)
        {
            return new RequestType<TParams, TResult>()
            {
                MethodName = typeName
            };
        }
    }
}

