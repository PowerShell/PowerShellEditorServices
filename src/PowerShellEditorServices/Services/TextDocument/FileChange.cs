//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// Contains details relating to a content change in an open file.
    /// </summary>
    public sealed class FileChange
    {
        /// <summary>
        /// The string which is to be inserted in the file.
        /// </summary>
        public string InsertString { get; set; }

        /// <summary>
        /// The 1-based line number where the change starts.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// The 1-based column offset where the change starts.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// The 1-based line number where the change ends.
        /// </summary>
        public int EndLine { get; set; }

        /// <summary>
        /// The 1-based column offset where the change ends.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// Indicates that the InsertString is an overwrite
        /// of the content, and all stale content and metadata
        /// should be discarded.
        /// </summary>
        public bool IsReload { get; set; }
    }
}
