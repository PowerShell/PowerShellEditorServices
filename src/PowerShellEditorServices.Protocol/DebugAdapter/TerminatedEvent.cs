//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class TerminatedEvent
    {
        public static readonly
            NotificationType<TerminatedEvent, object> Type =
            NotificationType<TerminatedEvent, object>.Create("terminated");

        public bool Restart { get; set; }
    }
}

