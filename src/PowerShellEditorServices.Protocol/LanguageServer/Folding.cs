//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class FoldingRangeRequest
    {
        /// <summary>
        /// A request to provide folding ranges in a document. The request's
        /// parameter is of type [FoldingRangeParams](#FoldingRangeParams), the
        /// response is of type [FoldingRangeList](#FoldingRangeList) or a Thenable
        /// that resolves to such.
        /// Ref: https://github.com/Microsoft/vscode-languageserver-node/blob/5350bc2ffe8afb17357c1a66fbdd3845fa05adfd/protocol/src/protocol.foldingRange.ts#L112-L120
        /// </summary>
        public static readonly
            RequestType<FoldingRangeParams, FoldingRange[], object, object> Type =
            RequestType<FoldingRangeParams, FoldingRange[], object, object>.Create("textDocument/foldingRange");
    }

    /// <summary>
    /// Parameters for a [FoldingRangeRequest](#FoldingRangeRequest).
    /// Ref: https://github.com/Microsoft/vscode-languageserver-node/blob/5350bc2ffe8afb17357c1a66fbdd3845fa05adfd/protocol/src/protocol.foldingRange.ts#L102-L110
    /// </summary>
    public class FoldingRangeParams
    {
        /// <summary>
        /// The text document
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    /// <summary>
    /// Represents a folding range.
    /// Ref: https://github.com/Microsoft/vscode-languageserver-node/blob/5350bc2ffe8afb17357c1a66fbdd3845fa05adfd/protocol/src/protocol.foldingRange.ts#L69-L100
    /// </summary>
    public class FoldingRange
    {
        /// <summary>
        /// The zero-based line number from where the folded range starts.
        /// </summary>
        public int StartLine { get; set; }

        /// <summary>
        /// The zero-based character offset from where the folded range starts. If not defined, defaults to the length of the start line.
        /// </summary>
        public int StartCharacter { get; set; }

        /// <summary>
        /// The zero-based line number where the folded range ends.
        /// </summary>
        public int EndLine { get; set; }

        /// <summary>
        /// The zero-based character offset before the folded range ends. If not defined, defaults to the length of the end line.
        /// </summary>
        public int EndCharacter { get; set; }

        /// <summary>
        /// Describes the kind of the folding range such as `comment' or 'region'. The kind
        /// is used to categorize folding ranges and used by commands like 'Fold all comments'. See
        /// [FoldingRangeKind](#FoldingRangeKind) for an enumeration of standardized kinds.
        /// </summary>
        public string Kind { get; set; }
    }
}
