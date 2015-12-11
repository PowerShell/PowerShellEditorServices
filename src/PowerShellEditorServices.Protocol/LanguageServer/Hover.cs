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
    public class MarkedString
    {
        public string Language { get; set; }

        public string Value { get; set; }
    }

    public class Hover
    {
        public MarkedString[] Contents { get; set; }

        public Range? Range { get; set; }
    }

    public class HoverRequest
    {
        public static readonly
            RequestType<TextDocumentPosition, Hover> Type =
            RequestType<TextDocumentPosition, Hover>.Create("textDocument/hover");

    }
}

