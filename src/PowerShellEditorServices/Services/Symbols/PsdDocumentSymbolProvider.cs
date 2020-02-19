//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Provides an IDocumentSymbolProvider implementation for
    /// enumerating symbols in .psd1 files.
    /// </summary>
    internal class PsdDocumentSymbolProvider : IDocumentSymbolProvider
    {
        string IDocumentSymbolProvider.ProviderId => nameof(PsdDocumentSymbolProvider);

        IEnumerable<ISymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            if ((scriptFile.FilePath != null &&
                 scriptFile.FilePath.EndsWith(".psd1", StringComparison.OrdinalIgnoreCase)) ||
                 IsPowerShellDataFileAst(scriptFile.ScriptAst))
            {
                var findHashtableSymbolsVisitor = new FindHashtableSymbolsVisitor();
                scriptFile.ScriptAst.Visit(findHashtableSymbolsVisitor);
                return findHashtableSymbolsVisitor.SymbolReferences;
            }

            return Enumerable.Empty<SymbolReference>();
        }

        /// <summary>
        /// Checks if a given ast represents the root node of a *.psd1 file.
        /// </summary>
        /// <param name="ast">The abstract syntax tree of the given script</param>
        /// <returns>true if the AST represts a *.psd1 file, otherwise false</returns>
        static public bool IsPowerShellDataFileAst(Ast ast)
        {
            // sometimes we don't have reliable access to the filename
            // so we employ heuristics to check if the contents are
            // part of a psd1 file.
            return IsPowerShellDataFileAstNode(
                        new { Item = ast, Children = new List<dynamic>() },
                        new Type[] {
                            typeof(ScriptBlockAst),
                            typeof(NamedBlockAst),
                            typeof(PipelineAst),
                            typeof(CommandExpressionAst),
                            typeof(HashtableAst) },
                        0);
        }

        static private bool IsPowerShellDataFileAstNode(dynamic node, Type[] levelAstMap, int level)
        {
            var levelAstTypeMatch = node.Item.GetType().Equals(levelAstMap[level]);
            if (!levelAstTypeMatch)
            {
                return false;
            }

            if (level == levelAstMap.Length - 1)
            {
                return levelAstTypeMatch;
            }

            var astsFound = (node.Item as Ast).FindAll(a => a is Ast, false);
            if (astsFound != null)
            {
                foreach (var astFound in astsFound)
                {
                    if (!astFound.Equals(node.Item)
                        && node.Item.Equals(astFound.Parent)
                        && IsPowerShellDataFileAstNode(
                            new { Item = astFound, Children = new List<dynamic>() },
                            levelAstMap,
                            level + 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
