//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    internal interface IEventWriter
    {
        Task SendEvent<TParams>(
            EventType<TParams> eventType,
            TParams eventParams);
    }
}

