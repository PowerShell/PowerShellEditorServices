//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// Defines a base parameter class for identifying a text document.
    /// </summary>
    [DebuggerDisplay("TextDocumentIdentifier = {Uri}")]
    public class TextDocumentIdentifier
    {
        /// <summary>
        /// Gets or sets the URI which identifies the path of the
        /// text document.
        /// </summary>
        public string Uri { get; set; }
    }

    /// <summary>
    /// Defines a position in a text document.
    /// </summary>
    [DebuggerDisplay("TextDocumentPosition = {Position.Line}:{Position.Character}")]
    public class TextDocumentPosition : TextDocumentIdentifier
    {
        /// <summary>
        /// Gets or sets the position in the document.
        /// </summary>
        public Position Position { get; set; }
    }

    public class DidOpenTextDocumentNotification : TextDocumentIdentifier
    {
        public static readonly
            EventType<DidOpenTextDocumentNotification> Type =
            EventType<DidOpenTextDocumentNotification>.Create("textDocument/didOpen");

        /// <summary>
        /// Gets or sets the full content of the opened document.
        /// </summary>
        public string Text { get; set; }
    }

    public class DidCloseTextDocumentNotification
    {
        public static readonly
            EventType<TextDocumentIdentifier> Type =
            EventType<TextDocumentIdentifier>.Create("textDocument/didClose");
    }

    public class DidChangeTextDocumentNotification
    {
        public static readonly
            EventType<DidChangeTextDocumentParams> Type =
            EventType<DidChangeTextDocumentParams>.Create("textDocument/didChange");
    }

    public class DidChangeTextDocumentParams : TextDocumentIdentifier
    {
        /// <summary>
        /// Gets or sets the list of changes to the document content.
        /// </summary>
        public TextDocumentChangeEvent[] ContentChanges { get; set; }
    }

    public class TextDocumentChangeEvent
    {
        /// <summary>
        /// Gets or sets the Range where the document was changed.  Will
        /// be null if the server's TextDocumentSyncKind is Full.
        /// </summary>
        public Range? Range { get; set; }

        /// <summary>
        /// Gets or sets the length of the Range being replaced in the
        /// document.  Will be null if the server's TextDocumentSyncKind is 
        /// Full.
        /// </summary>
        public int? RangeLength { get; set; }

        /// <summary>
        /// Gets or sets the new text of the document.
        /// </summary>
        public string Text { get; set; }
    }

    [DebuggerDisplay("Position = {Line}:{Character}")]
    public class Position
    {
        /// <summary>
        /// Gets or sets the zero-based line number.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Gets or sets the zero-based column number.
        /// </summary>
        public int Character { get; set; }
    }

    [DebuggerDisplay("Start = {Start.Line}:{Start.Character}, End = {End.Line}:{End.Character}")]
    public struct Range
    {
        /// <summary>
        /// Gets or sets the starting position of the range.
        /// </summary>
        public Position Start { get; set; }

        /// <summary>
        /// Gets or sets the ending position of the range.
        /// </summary>
        public Position End { get; set; }
    }

    [DebuggerDisplay("Range = {Range.Start.Line}:{Range.Start.Character} - {Range.End.Line}:{Range.End.Character}, Uri = {Uri}")]
    public class Location
    {
        /// <summary>
        /// Gets or sets the URI indicating the file in which the location refers.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets the Range indicating the range in which location refers.
        /// </summary>
        public Range Range { get; set; }
    }

    public enum FileChangeType
    {
        Created = 1,

        Changed,

        Deleted
    }

    public class FileEvent
    {
        public string Uri { get; set; }

        public FileChangeType Type { get; set; }
    }
}

