//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class RequestType<TParams, TResult, TError>
    {
        public string TypeName { get; private set; }

        public static RequestType<TParams, TResult, TError> Create(string typeName)
        {
            return new RequestType<TParams,TResult,TError>()
            {
                TypeName = typeName
            };
        }
    }
}

