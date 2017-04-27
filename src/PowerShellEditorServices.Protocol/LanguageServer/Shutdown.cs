//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// Defines a message that is sent from the client to request
    /// that the server shut down.
    /// </summary>
    public class ShutdownRequest
    {
        public static readonly
            RequestType0<object, object, object> Type =
            RequestType0<object, object, object>.Create("shutdown");
    }

    /// <summary>
    /// Defines an event that is sent from the client to notify that
    /// the client is exiting and the server should as well.
    /// </summary>
    public class ExitNotification
    {
        public static readonly
            NotificationType<object, object> Type =
            NotificationType<object, object>.Create("exit");
    }
}

