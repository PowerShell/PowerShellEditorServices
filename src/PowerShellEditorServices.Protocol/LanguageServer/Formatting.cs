//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class DocumentFormattingRequest
    {
        public static readonly
            RequestType<DocumentFormattingParams, TextEdit[], object,TextDocumentRegistrationOptions> Type = RequestType<DocumentFormattingParams, TextEdit[], object,TextDocumentRegistrationOptions>.Create("textDocument/formatting");
    }

    public class DocumentFormattingParams
    {
        public TextDocumentIdentifier TextDocument { get; set; }
        public FormattingOptions options { get; set; }
    }

    public class FormattingOptions
    {
        int TabSize { get; set; }
        bool InsertSpaces { get; set; }
    }
}

