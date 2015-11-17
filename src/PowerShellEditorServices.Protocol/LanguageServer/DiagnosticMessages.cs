//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class PublishDiagnosticsNotification
    {
        public static readonly
            EventType<PublishDiagnosticsNotification> Type = 
            EventType<PublishDiagnosticsNotification>.Create("textDocument/publishDiagnostics");

        /// <summary>
        /// Gets or sets the URI for which diagnostic information is reported.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets the array of diagnostic information items.
        /// </summary>
        public Diagnostic[] Diagnostics { get; set; }
    }

    public enum DiagnosticSeverity 
    {
        /// <summary>
        /// Indicates that the diagnostic represents an error.
        /// </summary>
        Error = 1,

        /// <summary>
        /// Indicates that the diagnostic represents a warning.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Indicates that the diagnostic represents an informational message.
        /// </summary>
        Information = 3,

        /// <summary>
        /// Indicates that the diagnostic represents a hint.
        /// </summary>
        Hint = 4
    }

    public class Diagnostic
    {
        public Range Range { get; set; }

        /// <summary>
        /// Gets or sets the severity of the diagnostic.  If omitted, the
        /// client should interpret the severity.
        /// </summary>
        public DiagnosticSeverity? Severity { get; set; }

        /// <summary>
        /// Gets or sets the diagnostic's code (optional).
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the diagnostic message.
        /// </summary>
        public string Message { get; set; }
    }
}

