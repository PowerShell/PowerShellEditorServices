//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    /// <summary>
    /// Indentifies the type of a given message.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// The message type is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The message is a request.
        /// </summary>
        Request,

        /// <summary>
        /// The message is a response.
        /// </summary>
        Response,

        /// <summary>
        /// The message is an event.
        /// </summary>
        Event
    }

}

