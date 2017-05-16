//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class StartDebuggerEvent
    {
        public static readonly
            NotificationType<object, object> Type =
            NotificationType<object, object>.Create("powerShell/startDebugger");
    }
}