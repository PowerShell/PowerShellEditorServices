//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// A 1-based file position, referring to a point in a file.
    /// </summary>
    public interface IFilePosition
    {
        /// <summary>
        /// The line number of the file position.
        /// </summary>
        int Line { get; }

        /// <summary>
        /// The column number of the file position.
        /// </summary>
        int Column { get; }
    }

    /// <summary>
    /// A 1-based file range, referring to a range within a file.
    /// </summary>
    public interface IFileRange
    {
        /// <summary>
        /// The start position of the range.
        /// </summary>
        IFilePosition Start { get; }

        /// <summary>
        /// The end position of the range.
        /// </summary>
        IFilePosition End { get; }
    }

    /// <summary>
    /// A snapshot of a file, including the URI of the file
    /// and its textual contents when accessed.
    /// </summary>
    public interface IFileContext
    {
        /// <summary>
        /// The URI of the file.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// The content of the file when it was accessed.
        /// </summary>
        string Content { get; }
    }

    /// <summary>
    /// 0-based position within a file, conformant with the Language Server Protocol.
    /// </summary>
    public interface ILspFilePosition
    {
        /// <summary>
        /// The line index of the position within the file.
        /// </summary>
        long Line { get; }

        /// <summary>
        /// The character offset from the line of the position.
        /// </summary>
        long Character { get; }
    }

    /// <summary>
    /// 0-based range within a file, conformant with the Language Server Protocol.
    /// </summary>
    public interface ILspFileRange
    {
        /// <summary>
        /// The start position of the range.
        /// </summary>
        ILspFilePosition Start { get; }

        /// <summary>
        /// The end position of the range.
        /// </summary>
        ILspFilePosition End { get; }
    }

    /// <summary>
    /// Snapshot of a file in focus in the editor.
    /// </summary>
    public interface ILspCurrentFileContext : IFileContext
    {
        /// <summary>
        /// The language the editor associates with this file.
        /// </summary>
        string Language { get; }

        /// <summary>
        /// The position of the cursor within the file when it was accessed.
        /// If the cursor is not in the file, values may be negative.
        /// </summary>
        ILspFilePosition CursorPosition { get; }

        /// <summary>
        /// The currently selected range when the file was accessed.
        /// If no selection is made, values may be negative.
        /// </summary>
        ILspFileRange SelectionRange { get; }
    }

    internal struct OmnisharpLspPosition : ILspFilePosition, IEquatable<OmnisharpLspPosition>
    {
        private readonly Position _position;

        public OmnisharpLspPosition(Position position)
        {
            _position = position;
        }

        public long Line => _position.Line;

        public long Character => _position.Character;

        public bool Equals(OmnisharpLspPosition other)
        {
            return _position == other._position;
        }
    }

    internal struct OmnisharpLspRange : ILspFileRange, IEquatable<OmnisharpLspRange>
    {
        private readonly Range _range;

        public OmnisharpLspRange(Range range)
        {
            _range = range;
        }

        public ILspFilePosition Start => new OmnisharpLspPosition(_range.Start);

        public ILspFilePosition End => new OmnisharpLspPosition(_range.End);

        public bool Equals(OmnisharpLspRange other)
        {
            return _range == other._range;
        }
    }

    internal struct BufferFilePosition : IFilePosition, IEquatable<BufferFilePosition>
    {
        private readonly BufferPosition _position;

        public BufferFilePosition(BufferPosition position)
        {
            _position = position;
        }

        public int Line => _position.Line;

        public int Column => _position.Column;

        public bool Equals(BufferFilePosition other)
        {
            return _position == other._position
                || _position.Equals(other._position);
        }
    }

    internal struct BufferFileRange : IFileRange, IEquatable<BufferFileRange>
    {
        private readonly BufferRange _range;

        public BufferFileRange(BufferRange range)
        {
            _range = range;
        }

        public IFilePosition Start => new BufferFilePosition(_range.Start);

        public IFilePosition End => new BufferFilePosition(_range.End);

        public bool Equals(BufferFileRange other)
        {
            return _range == other._range
                || _range.Equals(other._range);
        }
    }

    /// <summary>
    /// A 1-based file position.
    /// </summary>
    public class FilePosition : IFilePosition
    {
        public FilePosition(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }

        public int Column { get; }
    }

    /// <summary>
    /// A 0-based file position.
    /// </summary>
    public class LspFilePosition : ILspFilePosition
    {
        public LspFilePosition(long line, long column)
        {
            Line = line;
            Character = column;
        }

        public long Line { get; }

        public long Character { get; }
    }

    /// <summary>
    /// A 1-based file range.
    /// </summary>
    public class FileRange : IFileRange
    {
        public FileRange(IFilePosition start, IFilePosition end)
            : this(start, end, file: null)
        {
        }

        public FileRange(IFilePosition start, IFilePosition end, string file)
        {
            Start = start;
            End = end;
            File = file;
        }

        public IFilePosition Start { get; }

        public IFilePosition End { get; }

        public string File { get; }
    }

    /// <summary>
    /// A 0-based file range.
    /// </summary>
    public class LspFileRange : ILspFileRange
    {
        public LspFileRange(ILspFilePosition start, ILspFilePosition end)
        {
            Start = start;
            End = end;
        }

        public ILspFilePosition Start { get; }

        public ILspFilePosition End { get; }
    }

    internal class LspCurrentFileContext : ILspCurrentFileContext
    {
        private readonly ClientEditorContext _editorContext;

        public LspCurrentFileContext(ClientEditorContext editorContext)
        {
            _editorContext = editorContext;
            Uri = new Uri(editorContext.CurrentFilePath);
        }

        public string Language => _editorContext.CurrentFileLanguage;

        public ILspFilePosition CursorPosition => new OmnisharpLspPosition(_editorContext.CursorPosition);

        public ILspFileRange SelectionRange => new OmnisharpLspRange(_editorContext.SelectionRange);

        public Uri Uri { get; }

        public string Content => _editorContext.CurrentFileContent;
    }

    /// <summary>
    /// Extension methods to conveniently convert between file position and range types.
    /// </summary>
    public static class FileObjectExtensionMethods
    {

        /// <summary>
        /// Convert a 1-based file position to a 0-based file position.
        /// </summary>
        /// <param name="position">The 1-based file position to convert.</param>
        /// <returns>An equivalent 0-based file position.</returns>
        public static ILspFilePosition ToLspPosition(this IFilePosition position)
        {
            return new LspFilePosition(position.Line - 1, position.Column - 1);
        }

        /// <summary>
        /// Convert a 1-based file range to a 0-based file range.
        /// </summary>
        /// <param name="range">The 1-based file range to convert.</param>
        /// <returns>An equivalent 0-based file range.</returns>
        public static ILspFileRange ToLspRange(this IFileRange range)
        {
            return new LspFileRange(range.Start.ToLspPosition(), range.End.ToLspPosition());
        }

        /// <summary>
        /// Convert a 0-based file position to a 1-based file position.
        /// </summary>
        /// <param name="position">The 0-based file position to convert.</param>
        /// <returns>An equivalent 1-based file position.</returns>
        public static IFilePosition ToFilePosition(this ILspFilePosition position)
        {
            return new FilePosition((int)position.Line + 1, (int)position.Character + 1);
        }

        /// <summary>
        /// Convert a 0-based file range to a 1-based file range.
        /// </summary>
        /// <param name="range">The 0-based file range to convert.</param>
        /// <returns>An equivalent 1-based file range.</returns>
        public static IFileRange ToFileRange(this ILspFileRange range)
        {
            return new FileRange(range.Start.ToFilePosition(), range.End.ToFilePosition());
        }

        internal static bool HasRange(this IFileRange range)
        {
            return range.Start.Line != 0
                && range.Start.Column != 0
                && range.End.Line != 0
                && range.End.Column != 0;
        }
        internal static ILspFilePosition ToLspPosition(this Position position)
        {
            return new OmnisharpLspPosition(position);
        }

        internal static ILspFileRange ToLspRange(this Range range)
        {
            return new OmnisharpLspRange(range);
        }

        internal static Position ToOmnisharpPosition(this ILspFilePosition position)
        {
            return new Position(position.Line, position.Character);
        }

        internal static Range ToOmnisharpRange(this ILspFileRange range)
        {
            return new Range(range.Start.ToOmnisharpPosition(), range.End.ToOmnisharpPosition());
        }

        internal static BufferPosition ToBufferPosition(this IFilePosition position)
        {
            return new BufferPosition(position.Line, position.Column);
        }

        internal static BufferRange ToBufferRange(this IFileRange range)
        {
            return new BufferRange(range.Start.ToBufferPosition(), range.End.ToBufferPosition());
        }
    }
}
