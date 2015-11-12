//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Model
{
    public class Message
    {
        public int Id { get; set; }

//        /** A format string for the message. Embedded variables have the form '{name}'.
//            If variable name starts with an underscore character, the variable does not contain user data (PII) and can be safely used for telemetry purposes. */
        public string Format { get; set; }

//        /** An object used as a dictonary for looking up the variables in the format string. */
//        variables?: { [key: string]: string };
        public Dictionary<string, string> Variables { get; set; }
    }
}

