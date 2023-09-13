// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Visitor to find all the keys in Hashtable AST
    /// </summary>
    internal class FindHashtableSymbolsVisitor : AstVisitor
    {
        private readonly ScriptFile _file;

        /// <summary>
        /// List of symbols (keys) found in the hashtable
        /// </summary>
        public List<SymbolReference> SymbolReferences { get; }

        /// <summary>
        /// Initializes a new instance of FindHashtableSymbolsVisitor class
        /// </summary>
        public FindHashtableSymbolsVisitor(ScriptFile file)
        {
            SymbolReferences = new List<SymbolReference>();
            _file = file;
        }

        /// <summary>
        /// Adds keys in the input hashtable to the symbol reference
        /// </summary>
        /// <param name="hashtableAst">A HashtableAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitHashtable(HashtableAst hashtableAst)
        {
            if (hashtableAst.KeyValuePairs == null)
            {
                return AstVisitAction.Continue;
            }

            foreach (System.Tuple<ExpressionAst, StatementAst> kvp in hashtableAst.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst keyStrConstExprAst)
                {
                    IScriptExtent nameExtent = new ScriptExtent()
                    {
                        Text = keyStrConstExprAst.Value,
                        StartLineNumber = kvp.Item1.Extent.StartLineNumber,
                        EndLineNumber = kvp.Item2.Extent.EndLineNumber,
                        StartColumnNumber = kvp.Item1.Extent.StartColumnNumber,
                        EndColumnNumber = kvp.Item2.Extent.EndColumnNumber,
                        File = hashtableAst.Extent.File
                    };

                    SymbolReferences.Add(
                        // TODO: Should we fill this out better?
                        new SymbolReference(
                            SymbolType.HashtableKey,
                            nameExtent.Text,
                            nameExtent.Text,
                            nameExtent,
                            nameExtent,
                            _file,
                            isDeclaration: false));
                }
            }

            return AstVisitAction.Continue;
        }
    }
}
