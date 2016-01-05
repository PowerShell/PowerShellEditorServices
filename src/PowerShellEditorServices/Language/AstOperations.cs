//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides common operations for the syntax tree of a parsed script.
    /// </summary>
    internal static class AstOperations
    {
        /// <summary>
        /// Gets completions for the symbol found in the Ast at 
        /// the given file offset.
        /// </summary>
        /// <param name="scriptAst">
        /// The Ast which will be traversed to find a completable symbol.
        /// </param>
        /// <param name="currentTokens">
        /// The array of tokens corresponding to the scriptAst parameter.
        /// </param>
        /// <param name="fileOffset">
        /// The 1-based file offset at which a symbol will be located.
        /// </param>
        /// <param name="runspace">
        /// The Runspace to use for gathering completions.
        /// </param>
        /// <returns>
        /// A CommandCompletion instance that contains completions for the
        /// symbol at the given offset.
        /// </returns>
        static public CommandCompletion GetCompletions(
            Ast scriptAst, 
            Token[] currentTokens, 
            int fileOffset,
            Runspace runspace)
        {
            var type = scriptAst.Extent.StartScriptPosition.GetType();
            var method = 
                type.GetMethod(
                    "CloneWithNewOffset",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int) }, null);

            IScriptPosition cursorPosition = 
                (IScriptPosition)method.Invoke(
                    scriptAst.Extent.StartScriptPosition, 
                    new object[] { fileOffset });

            CommandCompletion commandCompletion = null;
            if (runspace.RunspaceAvailability == RunspaceAvailability.Available)
            {
                using (System.Management.Automation.PowerShell powerShell = 
                        System.Management.Automation.PowerShell.Create())
                {
                    powerShell.Runspace = runspace;

                    commandCompletion = 
                        CommandCompletion.CompleteInput(
                            scriptAst, 
                            currentTokens, 
                            cursorPosition, 
                            null, 
                            powerShell); 
                }
            }

            return commandCompletion;
        }

        /// <summary>
        /// Finds the symbol at a given file location 
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>SymbolReference of found symbol</returns>
        static public SymbolReference FindSymbolAtPosition(Ast scriptAst, int lineNumber, int columnNumber)
        {
            FindSymbolVisitor symbolVisitor = new FindSymbolVisitor(lineNumber, columnNumber);
            scriptAst.Visit(symbolVisitor);

            return symbolVisitor.FoundSymbolReference;
        }

        /// <summary>
        /// Finds the symbol (always Command type) at a given file location
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The column number of the cursor for the given script</param>
        /// <returns>SymbolReference of found command</returns>
        static public SymbolReference FindCommandAtPosition(Ast scriptAst, int lineNumber, int columnNumber)
        {
            FindCommandVisitor commandVisitor = new FindCommandVisitor(lineNumber, columnNumber);
            scriptAst.Visit(commandVisitor);

            return commandVisitor.FoundCommandReference;
        }

        /// <summary>
        /// Finds all references (including aliases) in a script for the given symbol
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="symbolReference">The symbol that we are looking for referneces of</param>
        /// <param name="CmdletToAliasDictionary">Dictionary maping cmdlets to aliases for finding alias references</param>
        /// <param name="AliasToCmdletDictionary">Dictionary maping aliases to cmdlets for finding alias references</param>
        /// <returns></returns>
        static public IEnumerable<SymbolReference> FindReferencesOfSymbol(
            Ast scriptAst, 
            SymbolReference symbolReference, 
            Dictionary<String, List<String>> CmdletToAliasDictionary,
            Dictionary<String, String> AliasToCmdletDictionary)
        {
            // find the symbol evaluators for the node types we are handling
            FindReferencesVisitor referencesVisitor = 
                new FindReferencesVisitor(
                    symbolReference, 
                    CmdletToAliasDictionary,
                    AliasToCmdletDictionary);
            scriptAst.Visit(referencesVisitor);

            return referencesVisitor.FoundReferences;
        }

        /// <summary>
        /// Finds all references (not including aliases) in a script for the given symbol
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="foundSymbol">The symbol that we are looking for referneces of</param>
        /// <param name="needsAliases">If this reference search needs aliases.
        /// This should always be false and used for occurence requests</param>
        /// <returns>A collection of SymbolReference objects that are refrences to the symbolRefrence
        /// not including aliases</returns>
        static public IEnumerable<SymbolReference> FindReferencesOfSymbol(
            ScriptBlockAst scriptAst, 
            SymbolReference foundSymbol, 
            bool needsAliases)
        {
            FindReferencesVisitor referencesVisitor =
                new FindReferencesVisitor(foundSymbol);
            scriptAst.Visit(referencesVisitor);

            return referencesVisitor.FoundReferences;
        }

        /// <summary>
        /// Finds the definition of the symbol 
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="symbolReference">The symbol that we are looking for the definition of</param>
        /// <returns>A SymbolReference of the definition of the symbolReference</returns>
        static public SymbolReference FindDefinitionOfSymbol(
            Ast scriptAst,
            SymbolReference symbolReference)
        {
            FindDeclartionVisitor declarationVisitor = 
                new FindDeclartionVisitor(
                    symbolReference);
            scriptAst.Visit(declarationVisitor);

            return declarationVisitor.FoundDeclartion;
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

            if (powerShellVersion >= new Version(5,0))
            {
#if PowerShellv5
                FindSymbolsVisitor2 findSymbolsVisitor = new FindSymbolsVisitor2();
                scriptAst.Visit(findSymbolsVisitor);
                symbolReferences = findSymbolsVisitor.SymbolReferences;
#endif
            }
            else
            {
                FindSymbolsVisitor findSymbolsVisitor = new FindSymbolsVisitor();
                scriptAst.Visit(findSymbolsVisitor);
                symbolReferences = findSymbolsVisitor.SymbolReferences;
            }

            return symbolReferences;
        }

        /// <summary>
        /// Finds all files dot sourced in a script
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <returns></returns>
        static public string[] FindDotSourcedIncludes(Ast scriptAst)
        {
            FindDotSourcedVisitor dotSourcedVisitor = new FindDotSourcedVisitor();
            scriptAst.Visit(dotSourcedVisitor);

            return dotSourcedVisitor.DotSourcedFiles.ToArray();
        }
    }
}
