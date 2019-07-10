//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Symbols
{
    /// <summary>
    /// Provides an IDocumentSymbolProvider implementation for
    /// enumerating symbols in script (.psd1, .psm1) files.
    /// </summary>
    public class ScriptDocumentSymbolProvider : IDocumentSymbolProvider
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
                    FindSymbolsInDocument(
                        scriptFile.ScriptAst,
                        this.powerShellVersion);
            }

            return Enumerable.Empty<SymbolReference>();
        }

        /// <summary>
        /// Finds all symbols in a script
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="powerShellVersion">The PowerShell version the Ast was generated from</param>
        /// <returns>A collection of SymbolReference objects</returns>
        static public IEnumerable<SymbolReference> FindSymbolsInDocument(Ast scriptAst, Version powerShellVersion)
        {
            IEnumerable<SymbolReference> symbolReferences = null;

            // TODO: Restore this when we figure out how to support multiple
            //       PS versions in the new PSES-as-a-module world (issue #276)
            //            if (powerShellVersion >= new Version(5,0))
            //            {
            //#if PowerShellv5
            //                FindSymbolsVisitor2 findSymbolsVisitor = new FindSymbolsVisitor2();
            //                scriptAst.Visit(findSymbolsVisitor);
            //                symbolReferences = findSymbolsVisitor.SymbolReferences;
            //#endif
            //            }
            //            else

            FindSymbolsVisitor findSymbolsVisitor = new FindSymbolsVisitor();
            scriptAst.Visit(findSymbolsVisitor);
            symbolReferences = findSymbolsVisitor.SymbolReferences;
            return symbolReferences;
        }
    }
}
