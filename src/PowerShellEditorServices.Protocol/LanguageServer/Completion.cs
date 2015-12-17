//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class CompletionRequest
    {
        public static readonly
            RequestType<TextDocumentPosition, CompletionItem[]> Type =
            RequestType<TextDocumentPosition, CompletionItem[]>.Create("textDocument/completion");
    }

    public class CompletionResolveRequest
    {
        public static readonly
            RequestType<CompletionItem, CompletionItem> Type =
            RequestType<CompletionItem, CompletionItem>.Create("completionItem/resolve");
    }

    public enum CompletionItemKind
    {
        	Text = 1,
            Method = 2,
            Function = 3,
            Constructor = 4,
            Field = 5,
            Variable = 6,
            Class = 7,
            Interface = 8,
            Module = 9,
            Property = 10,
            Unit = 11,
            Value = 12,
            Enum = 13,
            Keyword = 14,
            Snippet = 15,
            Color = 16,
            File = 17,
            Reference = 18
    }

    [DebuggerDisplay("NewText = {NewText}, Range = {Range.Start.Line}:{Range.Start.Character} - {Range.End.Line}:{Range.End.Character}")]
    public class TextEdit 
    {
        public Range Range { get; set; }

        public string NewText { get; set; }
    }

    [DebuggerDisplay("Kind = {Kind.ToString()}, Label = {Label}, Detail = {Detail}")]
    public class CompletionItem
    {
        public string Label { get; set; }

        public CompletionItemKind? Kind { get; set; }

        public string Detail { get; set; }

        /// <summary>
        /// Gets or sets the documentation string for the completion item.
        /// </summary>
        public string Documentation { get; set; }

        public string SortText { get; set; }

        public string FilterText { get; set; }

        public string InsertText { get; set; }

        public TextEdit TextEdit { get; set; }

        /// <summary>
        /// Gets or sets a custom data field that allows the server to mark
        /// each completion item with an identifier that will help correlate
        /// the item to the previous completion request during a completion
        /// resolve request.
        /// </summary>
        public object Data { get; set; }
    }
}

