//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class OutputEvent
    {
        public static readonly
            NotificationType<OutputEventBody, object> Type =
            NotificationType<OutputEventBody, object>.Create("output");
    }

    public class OutputEventBody
    {
        public string Category { get; set; }

        public string Output { get; set; }
    }
}

