//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Symbols
{
    /// <summary>
    /// Provides an IDocumentSymbolProvider implementation for
    /// enumerating symbols in .psd1 files.
    /// </summary>
    public class PsdDocumentSymbolProvider : FeatureProviderBase, IDocumentSymbolProvider
    {
        IEnumerable<SymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            if ((scriptFile.FilePath != null &&
                 scriptFile.FilePath.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase)) ||
                 AstOperations.IsPowerShellDataFileAst(scriptFile.ScriptAst))
            {
                var findHashtableSymbolsVisitor = new FindHashtableSymbolsVisitor();
                scriptFile.ScriptAst.Visit(findHashtableSymbolsVisitor);
                return findHashtableSymbolsVisitor.SymbolReferences;
            }

            return Enumerable.Empty<SymbolReference>();
        }
    }
}
