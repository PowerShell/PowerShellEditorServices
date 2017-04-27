//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class ExitedEvent
    {
        public static readonly
            NotificationType<ExitedEventBody, object> Type =
            NotificationType<ExitedEventBody, object>.Create("exited");
    }

    public class ExitedEventBody
    {
        public int ExitCode { get; set; }
    }
}

