//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// A class that holds the information for a foldable region of text in a document
    /// </summary>
    internal class FoldingReference: IComparable<FoldingReference>, IEquatable<FoldingReference>
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
        public FoldingRangeKind? Kind { get; set; }

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
            if (this.Kind == null)
            {
                if (that.Kind == null)
                {
                    return 0;
                }
                // that has a kind but this doesn't.
                return 1;
            }

            if (that.Kind != null)
            {
                return that.Kind.Value - this.Kind.Value;
            }

            // this has a kind but that doesn't.
            return -1;
        }

        public bool Equals(FoldingReference other) => this.CompareTo(other) == 0;
    }

    /// <summary>
    /// A class that holds a list of FoldingReferences and ensures that when adding a reference that the
    /// folding rules are obeyed, e.g. Only one fold per start line
    /// </summary>
    internal class FoldingReferenceList
    {
        private readonly Dictionary<int, FoldingReference> references = new Dictionary<int, FoldingReference>();

        /// <summary>
        /// Return all references in the list
        /// </summary>
        public IEnumerable<FoldingReference> References
        {
            get
            {
                return references.Values;
            }
        }

        /// <summary>
        /// Adds a FoldingReference to the list and enforces ordering rules e.g. Only one fold per start line
        /// </summary>
        public void SafeAdd(FoldingReference item)
        {
            if (item == null) { return; }

            // Only add the item if it hasn't been seen before or it's the largest range
            if (references.TryGetValue(item.StartLine, out FoldingReference currentItem))
            {
                if (currentItem.CompareTo(item) == 1) { references[item.StartLine] = item; }
            }
            else
            {
                references[item.StartLine] = item;
            }
        }

        /// <summary>
        /// Helper method to easily convert the Dictionary Values into an array
        /// </summary>
        public FoldingReference[] ToArray()
        {
            var result = new FoldingReference[references.Count];
            references.Values.CopyTo(result, 0);
            return result;
        }
    }
}
