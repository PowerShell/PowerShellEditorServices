//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    public class StartedEvent : EventBase<object>
    {
        public StartedEvent()
        {
            this.EventType = "started";
        }
    }
}
