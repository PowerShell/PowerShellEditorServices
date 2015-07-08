//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    public class DiagnosticEvent : EventBase<DiagnosticEventBody>
    {
        public DiagnosticEvent()
        {
            // Initialize to an identifiable uninitialized value.
            // Could either be "syntaxDiag" or "semanticDiag"
            // This should be replaced by the deserialization process.
            this.EventType = "NOT_SUPPLIED";
        }

        public DiagnosticEvent(string eventType)
        {
            this.EventType = eventType;
        }
    }

    public class DiagnosticEventBody
    {
        public string File { get; set; }

        public Diagnostic[] Diagnostics { get; set; }
    }

    public class Location
    {
        public int Line { get; set; }

        public int Offset { get; set; }
    }

    public class Diagnostic
    {
        public Location Start { get; set; }

        public Location End { get; set; }

        public string Text { get; set; }
    }
}
