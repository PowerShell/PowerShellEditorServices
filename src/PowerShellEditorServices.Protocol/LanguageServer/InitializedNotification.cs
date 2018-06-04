//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// The initialized notification is sent from the client to the server after the client received the result 
    /// of the initialize request but before the client is sending any other request or notification to the server. 
    /// The server can use the initialized notification for example to dynamically register capabilities. 
    /// The initialized notification may only be sent once.
    /// </summary>
    public class InitializedNotification
    {
        public static readonly
            NotificationType<InitializedParams, object> Type =
            NotificationType<InitializedParams, object>.Create("initialized");
    }

    /// <summary>
    /// Currently, the initialized message has no parameters.
    /// </summary>
    public class InitializedParams
    {
    }
}
