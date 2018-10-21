//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// A class that holds the information for a foldable region of text in a document
    /// </summary>
    public class FoldingReference: IComparable<FoldingReference>
    {
        /// <summary>
        /// The zero-based line number from where the folded range starts.
        /// </summary>
        public int StartLine { get; set; }

        /// <summary>
        /// The zero-based character offset from where the folded range starts. If not defined, defaults to the length of the start line.
        /// </summary>
        public int StartCharacter { get; set; } = 0;

        /// <summary>
        /// The zero-based line number where the folded range ends.
        /// </summary>
        public int EndLine { get; set; }

        /// <summary>
        /// The zero-based character offset before the folded range ends. If not defined, defaults to the length of the end line.
        /// </summary>
        public int EndCharacter { get; set; } = 0;

        /// <summary>
        /// Describes the kind of the folding range such as `comment' or 'region'.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// A custom comparable method which can properly sort FoldingReference objects
        /// </summary>
        public int CompareTo(FoldingReference that) {
            // Initially look at the start line
            if (this.StartLine < that.StartLine) { return -1; }
            if (this.StartLine > that.StartLine) { return 1; }

            // They have the same start line so now consider the end line.
            // The biggest line range is sorted first
            if (this.EndLine > that.EndLine) { return -1; }
            if (this.EndLine < that.EndLine) { return 1; }

            // They have the same lines, but what about character offsets
            if (this.StartCharacter < that.StartCharacter) { return -1; }
            if (this.StartCharacter > that.StartCharacter) { return 1; }
            if (this.EndCharacter < that.EndCharacter) { return -1; }
            if (this.EndCharacter > that.EndCharacter) { return 1; }

            // They're the same range, but what about kind
            return string.Compare(this.Kind, that.Kind);
        }
    }
}
