//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    [MessageTypeName("replWriteOutput")]
    public class ReplWriteOutputEvent : EventBase<ReplWriteOutputEventBody>
    {
    }

    public class ReplWriteOutputEventBody
    {
        public string LineContents { get; set; }

        public bool IncludeNewLine { get; set; }

        public OutputType LineType { get; set; }

        public ConsoleColor ForegroundColor { get; set; }

        public ConsoleColor BackgroundColor { get; set; }
    }
}
