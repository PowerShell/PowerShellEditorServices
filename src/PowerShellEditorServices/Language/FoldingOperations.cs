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
        internal static FoldingReferenceList FoldableRegions(
            Token[] tokens,
            Ast scriptAst)
        {
            var foldableRegions = new FoldingReferenceList();

            // Add regions from AST
            AstOperations.FindFoldsInDocument(scriptAst, ref foldableRegions);

            // Add regions from Tokens
            TokenOperations.FoldableRegions(tokens, ref foldableRegions);

            return foldableRegions;
        }
    }
}
