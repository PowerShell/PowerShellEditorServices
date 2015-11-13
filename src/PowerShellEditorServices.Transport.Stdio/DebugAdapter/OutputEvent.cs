//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    [MessageTypeName("output")]
    public class OutputEvent : EventBase<OutputEventBody>
    {
    }

    public class OutputEventBody
    {
        public string Category { get; set; }

        public string Output { get; set; }
    }
}

