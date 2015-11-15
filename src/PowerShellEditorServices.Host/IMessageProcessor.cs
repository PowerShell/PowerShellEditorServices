//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Host
{
    /// <summary>
    /// Provides an interface for classes that can process an incoming
    /// message of some type.
    /// </summary>
    public interface IMessageProcessor
    {
        /// <summary>
        /// Performs some action
        /// </summary>
        /// <param name="editorSession"></param>
        /// <param name="messageWriter"></param>
        Task ProcessMessage(
            Message messageToProcess,
            EditorSession editorSession,
            MessageWriter messageWriter);
    }
}
