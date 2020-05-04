//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    public class FileScriptPosition : IScriptPosition, IFilePosition
    {
        public static FileScriptPosition Empty { get; } = new FileScriptPosition(null, 0, 0, 0);

        public static FileScriptPosition FromPosition(FileContext file, int lineNumber, int columnNumber)
        {
            int offset = 0;
            int currLine = 1;
            string fileText = file.Ast.Extent.Text;
            while (offset < fileText.Length && currLine < lineNumber)
            {
                offset = fileText.IndexOf('\n', offset);
                currLine++;
            }

            offset += columnNumber - 1;

            return new FileScriptPosition(file, lineNumber, columnNumber, offset);
        }

        public static FileScriptPosition FromOffset(FileContext file, int offset)
        {

            int line = 1;
            string fileText = file.Ast.Extent.Text;

            if (offset >= fileText.Length)
            {
                throw new ArgumentException(nameof(offset), "Offset greater than file length");
            }

            int lastLineOffset = -1;
            for (int i = 0; i < offset; i++)
            {
                if (fileText[i] == '\n')
                {
                    lastLineOffset = i;
                    line++;
                }
            }

            int column = offset - lastLineOffset;

            return new FileScriptPosition(file, line, column, offset);
        }

        private readonly FileContext _file;

        internal FileScriptPosition(FileContext file, int lineNumber, int columnNumber, int offset)
        {
            _file = file;
            Line = file.GetTextLines()[lineNumber - 1];
            ColumnNumber = columnNumber;
            LineNumber = lineNumber;
            Offset = offset;
        }

        public int ColumnNumber { get; }

        public string File { get; }

        public string Line { get; }

        public int LineNumber { get; }

        public int Offset { get; }

        int IFilePosition.Column => ColumnNumber;

        int IFilePosition.Line => LineNumber;

        public string GetFullScript() => _file.GetText();
    }

    public class FileScriptExtent : IScriptExtent, IFileRange
    {
        public static bool IsEmpty(FileScriptExtent extent)
        {
            return extent == Empty
                || (extent.StartOffset == 0 && extent.EndOffset == 0);
        }

        public static FileScriptExtent Empty { get; } = new FileScriptExtent(null, FileScriptPosition.Empty, FileScriptPosition.Empty);

        public static FileScriptExtent FromOffsets(FileContext file, int startOffset, int endOffset)
        {
            return new FileScriptExtent(
                file,
                FileScriptPosition.FromOffset(file, startOffset),
                FileScriptPosition.FromOffset(file, endOffset));
        }

        public static FileScriptExtent FromPositions(FileContext file, int startLine, int startColumn, int endLine, int endColumn)
        {
            return new FileScriptExtent(
                file,
                FileScriptPosition.FromPosition(file, startLine, startColumn),
                FileScriptPosition.FromPosition(file, endLine, endColumn));
        }

        private readonly FileContext _file;
        private readonly FileScriptPosition _start;
        private readonly FileScriptPosition _end;

        public FileScriptExtent(FileContext file, FileScriptPosition start, FileScriptPosition end)
        {
            _file = file;
            _start = start;
            _end = end;
        }

        public int EndColumnNumber => _end.ColumnNumber;

        public int EndLineNumber => _end.LineNumber;

        public int EndOffset => _end.Offset;

        public IScriptPosition EndScriptPosition => _end;

        public string File => _file.Path;

        public int StartColumnNumber => _start.ColumnNumber;

        public int StartLineNumber => _start.LineNumber;

        public int StartOffset => _start.Offset;

        public IScriptPosition StartScriptPosition => _start;

        public string Text => _file.GetText().Substring(_start.Offset, _end.Offset - _start.Offset);

        IFilePosition IFileRange.Start => _start;

        IFilePosition IFileRange.End => _end;
    }

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
        int Line { get; }

        /// <summary>
        /// The character offset from the line of the position.
        /// </summary>
        int Character { get; }
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

        public int Line => _position.Line;

        public int Character => _position.Character;

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
        public LspFilePosition(int line, int column)
        {
            Line = line;
            Character = column;
        }

        public int Line { get; }

        public int Character { get; }
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
            return new FilePosition(position.Line + 1, position.Character + 1);
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
