//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class DocumentFormattingRequest
    {
        public static readonly RequestType<DocumentFormattingParams, TextEdit[], object, TextDocumentRegistrationOptions> Type = RequestType<DocumentFormattingParams, TextEdit[], object, TextDocumentRegistrationOptions>.Create("textDocument/formatting");
    }

    public class DocumentRangeFormattingRequest
    {
        public static readonly RequestType<DocumentRangeFormattingParams, TextEdit[], object, TextDocumentRegistrationOptions> Type = RequestType<DocumentRangeFormattingParams, TextEdit[], object, TextDocumentRegistrationOptions>.Create("textDocument/rangeFormatting");

    }

    public class DocumentOnTypeFormattingRequest
    {
        public static readonly RequestType<DocumentOnTypeFormattingRequest, TextEdit[], object, TextDocumentRegistrationOptions> Type = RequestType<DocumentOnTypeFormattingRequest, TextEdit[], object, TextDocumentRegistrationOptions>.Create("textDocument/onTypeFormatting");

    }

    public class DocumentRangeFormattingParams
    {
        /// <summary>
        /// The document to format.
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <summary>
        /// The range to format.
        /// </summary>
        /// <returns></returns>
        public Range Range { get; set; }

        /// <summary>
        /// The format options.
        /// </summary>
        public FormattingOptions Options { get; set; }
    }

    public class DocumentOnTypeFormattingParams
    {
        /// <summary>
        /// The document to format.
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <summary>
        /// The position at which this request was sent.
        /// </summary>
        public Position Position { get; set; }

        /// <summary>
        /// The character that has been typed.
        /// </summary>
        public string ch { get; set; }

        /// <summary>
        /// The format options.
        /// </summary>
        public FormattingOptions options { get; set; }
    }

    public class DocumentFormattingParams
    {
        /// <summary>
        /// The document to format.
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <summary>
        /// The format options.
        /// </summary>
        public FormattingOptions options { get; set; }
    }

    public class FormattingOptions
    {
        /// <summary>
        /// Size of a tab in spaces.
        /// </summary>
        public int TabSize { get; set; }

        /// <summary>
        /// Prefer spaces over tabs.
        /// </summary>
        public bool InsertSpaces { get; set; }
    }
}

