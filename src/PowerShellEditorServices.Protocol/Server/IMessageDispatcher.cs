//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol
{
    public interface IMessageDispatcher
    {
        Task DispatchMessage(
            Message messageToDispatch,
            MessageWriter messageWriter);
    }
}
