//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public enum DocumentHighlightKind
    {
        Text = 1,
        Read = 2,
        Write = 3
    }

    public class DocumentHighlight
    {
	    public Range Range { get; set; }

        public DocumentHighlightKind Kind { get; set; }
    }

    public class DocumentHighlightRequest
    {
        public static readonly
            RequestType<TextDocumentPositionParams, DocumentHighlight[], object, object> Type =
            RequestType<TextDocumentPositionParams, DocumentHighlight[], object, object>.Create("textDocument/documentHighlight");
    }
}

