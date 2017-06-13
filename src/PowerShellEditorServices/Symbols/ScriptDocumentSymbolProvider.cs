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
    /// enumerating symbols in script (.psd1, .psm1) files.
    /// </summary>
    public class ScriptDocumentSymbolProvider : FeatureProviderBase, IDocumentSymbolProvider
    {
        private Version powerShellVersion;

        /// <summary>
        /// Creates an instance of the ScriptDocumentSymbolProvider to
        /// target the specified PowerShell version.
        /// </summary>
        /// <param name="powerShellVersion">The target PowerShell version.</param>
        public ScriptDocumentSymbolProvider(Version powerShellVersion)
        {
            this.powerShellVersion = powerShellVersion;
        }

        IEnumerable<SymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            if (scriptFile != null &&
                scriptFile.FilePath != null &&
                (scriptFile.FilePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                    scriptFile.FilePath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase)))
            {
                return
                    AstOperations.FindSymbolsInDocument(
                        scriptFile.ScriptAst,
                        this.powerShellVersion);
            }

            return Enumerable.Empty<SymbolReference>();
        }
    }
}
