//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    [MessageTypeName("exited")]
    public class ExitedEvent : EventBase<ExitedEventBody>
    {
    }

    public class ExitedEventBody
    {
        public int ExitCode { get; set; }
    }
}

