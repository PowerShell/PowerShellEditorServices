//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides common operations for code folding in a script
    /// </summary>
    internal static class FoldingOperations
    {
        /// <summary>
        /// Extracts all of the unique foldable regions in a script given a script AST and the list tokens
        /// used to generate the AST
        /// </summary>
        internal static FoldingReference[] FoldableRegions(
            Token[] tokens,
            Ast scriptAst)
        {
            var foldableRegions = new List<FoldingReference>();

            // Add regions from AST
            foldableRegions.AddRange(AstOperations.FindFoldsInDocument(scriptAst));

            // Add regions from Tokens
            foldableRegions.AddRange(TokenOperations.FoldableRegions(tokens));

            // Sort the FoldingReferences, starting at the top of the document,
            // and ensure that, in the case of multiple ranges starting the same line,
            // that the largest range (i.e. most number of lines spanned) is sorted
            // first. This is needed to detect duplicate regions. The first in the list
            // will be used and subsequent duplicates ignored.
            foldableRegions.Sort();

            // It's possible to have duplicate or overlapping ranges, that is, regions which have the same starting
            // line number as the previous region. Therefore only emit ranges which have a different starting line
            // than the previous range.
            foldableRegions.RemoveAll( (FoldingReference item) => {
                // Note - I'm not happy with searching here, but as the RemoveAll
                // doesn't expose the index in the List, we need to calculate it. Fortunately the
                // list is sorted at this point, so we can use BinarySearch.
                int index = foldableRegions.BinarySearch(item);
                if (index == 0) { return false; }
                return (item.StartLine == foldableRegions[index - 1].StartLine);
            });

            return foldableRegions.ToArray();
        }
    }
}
