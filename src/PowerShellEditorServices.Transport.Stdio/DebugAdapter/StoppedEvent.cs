//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class StoppedEvent
    {
        public static readonly
            EventType<StoppedEventBody> Type =
            EventType<StoppedEventBody>.Create("stopped");
    }

    public class StoppedEventBody
    {
        /// <summary>
        /// A value such as "step", "breakpoint", "exception", or "pause"
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the current thread ID, if any.
        /// </summary>
        public int? ThreadId { get; set; }

        public Source Source { get; set; } 

        public int Line { get; set; }

        public int Column { get; set; }

        /// <summary>
        /// Gets or sets additional information such as an error message.
        /// </summary>
        public string Text { get; set; }
    }
}

