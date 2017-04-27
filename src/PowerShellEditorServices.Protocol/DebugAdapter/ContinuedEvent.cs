//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class ContinuedEvent
    {
        public static readonly
            NotificationType<ContinuedEvent, object> Type =
            NotificationType<ContinuedEvent, object>.Create("continued");

        public int ThreadId { get; set; }

        public bool AllThreadsContinued { get; set; }
    }
}
