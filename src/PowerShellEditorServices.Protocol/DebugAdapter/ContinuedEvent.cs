//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class ContinuedEvent
    {
        public static readonly
            EventType<ContinuedEvent> Type =
            EventType<ContinuedEvent>.Create("continued");

        public int ThreadId { get; set; }

        public bool AllThreadsContinued { get; set; }
    }
}
