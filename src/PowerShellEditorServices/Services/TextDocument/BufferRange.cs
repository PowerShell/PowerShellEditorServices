//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// Provides details about a range between two positions in
    /// a file buffer.
    /// </summary>
    [DebuggerDisplay("Start = {Start.Line}:{Start.Column}, End = {End.Line}:{End.Column}")]
    internal sealed class BufferRange
    {
        #region Properties

        /// <summary>
        /// Provides an instance that represents a range that has not been set.
        /// </summary>
        public static readonly BufferRange None = new BufferRange(0, 0, 0, 0);

        /// <summary>
        /// Gets the start position of the range in the buffer.
        /// </summary>
        public BufferPosition Start { get; private set; }

        /// <summary>
        /// Gets the end position of the range in the buffer.
        /// </summary>
        public BufferPosition End { get; private set; }

        /// <summary>
        /// Returns true if the current range is non-zero, i.e.
        /// contains valid start and end positions.
        /// </summary>
        public bool HasRange
        {
            get
            {
                return this.Equals(BufferRange.None);
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the BufferRange class.
        /// </summary>
        /// <param name="start">The start position of the range.</param>
        /// <param name="end">The end position of the range.</param>
        public BufferRange(BufferPosition start, BufferPosition end)
        {
            if (start > end)
            {
                throw new ArgumentException(
                    string.Format(
                        "Start position ({0}, {1}) must come before or be equal to the end position ({2}, {3}).",
                        start.Line, start.Column,
                        end.Line, end.Column));
            }

            this.Start = start;
            this.End = end;
        }

        /// <summary>
        /// Creates a new instance of the BufferRange class.
        /// </summary>
        /// <param name="startLine">The 1-based starting line number of the range.</param>
        /// <param name="startColumn">The 1-based starting column number of the range.</param>
        /// <param name="endLine">The 1-based ending line number of the range.</param>
        /// <param name="endColumn">The 1-based ending column number of the range.</param>
        public BufferRange(
            int startLine,
            int startColumn,
            int endLine,
            int endColumn)
        {
            this.Start = new BufferPosition(startLine, startColumn);
            this.End = new BufferPosition(endLine, endColumn);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Compares two instances of the BufferRange class.
        /// </summary>
        /// <param name="obj">The object to which this instance will be compared.</param>
        /// <returns>True if the ranges are equal, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is BufferRange))
            {
                return false;
            }

            BufferRange other = (BufferRange)obj;

            return
                this.Start.Equals(other.Start) &&
                this.End.Equals(other.End);
        }

        /// <summary>
        /// Calculates a unique hash code that represents this instance.
        /// </summary>
        /// <returns>A hash code representing this instance.</returns>
        public override int GetHashCode()
        {
            return this.Start.GetHashCode() ^ this.End.GetHashCode();
        }

        #endregion
    }
}

