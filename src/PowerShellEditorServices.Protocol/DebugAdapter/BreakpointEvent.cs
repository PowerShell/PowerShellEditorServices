//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class BreakpointEvent
    {
        public static readonly
            NotificationType<BreakpointEvent, object> Type =
            NotificationType<BreakpointEvent, object>.Create("breakpoint");

        public string Reason { get; set; }

        public Breakpoint Breakpoint { get; set; }
    }
}
