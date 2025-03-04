// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument;

/// <summary>
/// Provides details about a position in a file buffer.  All
/// positions are expressed in 1-based positions (i.e. the
/// first line and column in the file is position 1,1).
/// </summary>
[DebuggerDisplay("Position = {Line}:{Column}")]
internal class BufferPosition
{
    #region Properties

    /// <summary>
    /// Provides an instance that represents a position that has not been set.
    /// </summary>
    public static readonly BufferPosition None = new(-1, -1);

    /// <summary>
    /// Gets the line number of the position in the buffer.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the column number of the position in the buffer.
    /// </summary>
    public int Column { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Creates a new instance of the BufferPosition class.
    /// </summary>
    /// <param name="line">The line number of the position.</param>
    /// <param name="column">The column number of the position.</param>
    public BufferPosition(int line, int column)
    {
        Line = line;
        Column = column;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Compares two instances of the BufferPosition class.
    /// </summary>
    /// <param name="obj">The object to which this instance will be compared.</param>
    /// <returns>True if the positions are equal, false otherwise.</returns>
    public override bool Equals(object obj)
    {
        if (obj is not BufferPosition)
        {
            return false;
        }

        BufferPosition other = (BufferPosition)obj;

        return
            Line == other.Line &&
            Column == other.Column;
    }

    /// <summary>
    /// Calculates a unique hash code that represents this instance.
    /// </summary>
    /// <returns>A hash code representing this instance.</returns>
    public override int GetHashCode() => Line.GetHashCode() ^ Column.GetHashCode();

    /// <summary>
    /// Compares two positions to check if one is greater than the other.
    /// </summary>
    /// <param name="positionOne">The first position to compare.</param>
    /// <param name="positionTwo">The second position to compare.</param>
    /// <returns>True if positionOne is greater than positionTwo.</returns>
    public static bool operator >(BufferPosition positionOne, BufferPosition positionTwo)
    {
        return
            (positionOne != null && positionTwo == null) ||
            (positionOne.Line > positionTwo.Line) ||
            (positionOne.Line == positionTwo.Line &&
             positionOne.Column > positionTwo.Column);
    }

    /// <summary>
    /// Compares two positions to check if one is less than the other.
    /// </summary>
    /// <param name="positionOne">The first position to compare.</param>
    /// <param name="positionTwo">The second position to compare.</param>
    /// <returns>True if positionOne is less than positionTwo.</returns>
    public static bool operator <(BufferPosition positionOne, BufferPosition positionTwo) => positionTwo > positionOne;

    #endregion
}
