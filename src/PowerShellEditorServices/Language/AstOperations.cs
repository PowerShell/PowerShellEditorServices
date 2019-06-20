﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices
{
    using System.Management.Automation;
    using System.Management.Automation.Language;

    /// <summary>
    /// Provides common operations for the syntax tree of a parsed script.
    /// </summary>
    internal static class AstOperations
    {
        // TODO: When netstandard is upgraded to 2.0, see if
        //       Delegate.CreateDelegate can be used here instead
        private static readonly MethodInfo s_extentCloneWithNewOffset = typeof(PSObject).GetTypeInfo().Assembly
            .GetType("System.Management.Automation.Language.InternalScriptPosition")
            .GetMethod("CloneWithNewOffset", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly SemaphoreSlim s_completionHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private static PowerShell pwsh = PowerShell.Create();

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
        /// <param name="powerShellContext">
        /// The PowerShellContext to use for gathering completions.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        /// <param name="cancellationToken">
        /// A CancellationToken to cancel completion requests.
        /// </param>
        /// <returns>
        /// A CommandCompletion instance that contains completions for the
        /// symbol at the given offset.
        /// </returns>
        static public async Task<CommandCompletion> GetCompletionsAsync(
            Ast scriptAst,
            Token[] currentTokens,
            int fileOffset,
            PowerShellContext powerShellContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (!s_completionHandle.Wait(0))
            {
                return null;
            }

            if (!RunspaceSynchronizer.IsReadyForEvents)
            {
                RunspaceSynchronizer.InitializeRunspaces(powerShellContext.CurrentRunspace.Runspace, pwsh.Runspace);
            }

            try
            {
                IScriptPosition cursorPosition = (IScriptPosition)s_extentCloneWithNewOffset.Invoke(
                scriptAst.Extent.StartScriptPosition,
                new object[] { fileOffset });

                logger.Write(
                    LogLevel.Verbose,
                    string.Format(
                        "Getting completions at offset {0} (line: {1}, column: {2})",
                        fileOffset,
                        cursorPosition.LineNumber,
                        cursorPosition.ColumnNumber));

                if (!powerShellContext.IsAvailable)
                {
                    return null;
                }

                var stopwatch = new Stopwatch();

                stopwatch.Start();

                try
                {
                return CommandCompletion.CompleteInput(
                    scriptAst,
                    currentTokens,
                    cursorPosition,
                    options: null,
                    powershell: pwsh);
                }
                finally
                {
                    stopwatch.Stop();
                    logger.Write(LogLevel.Verbose, $"IntelliSense completed in {stopwatch.ElapsedMilliseconds}ms.");
                }
                // If the current runspace is out of process we can use
                // CommandCompletion.CompleteInput because PSReadLine won't be taking up the
                // main runspace.
                // if (powerShellContext.IsCurrentRunspaceOutOfProcess())
                // {
                //     using (RunspaceHandle runspaceHandle = await powerShellContext.GetRunspaceHandleAsync(cancellationToken))
                //     using (PowerShell powerShell = PowerShell.Create())
                //     {
                //         powerShell.Runspace = runspaceHandle.Runspace;
                //         stopwatch.Start();
                //         try
                //         {
                //             return CommandCompletion.CompleteInput(
                //                 scriptAst,
                //                 currentTokens,
                //                 cursorPosition,
                //                 options: null,
                //                 powershell: powerShell);
                //         }
                //         finally
                //         {
                //             stopwatch.Stop();
                //             logger.Write(LogLevel.Verbose, $"IntelliSense completed in {stopwatch.ElapsedMilliseconds}ms.");
                //         }
                //     }
                // }

                // CommandCompletion commandCompletion = null;
                // await powerShellContext.InvokeOnPipelineThreadAsync(
                //     pwsh =>
                //     {
                //         stopwatch.Start();
                //         commandCompletion = CommandCompletion.CompleteInput(
                //             scriptAst,
                //             currentTokens,
                //             cursorPosition,
                //             options: null,
                //             powershell: pwsh);
                //     });
                // stopwatch.Stop();
                // logger.Write(LogLevel.Verbose, $"IntelliSense completed in {stopwatch.ElapsedMilliseconds}ms.");

                // return commandCompletion;
            }
            finally
            {
                s_completionHandle.Release();
            }
        }

        /// <summary>
        /// Finds the symbol at a given file location
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <param name="includeFunctionDefinitions">Includes full function definition ranges in the search.</param>
        /// <returns>SymbolReference of found symbol</returns>
        static public SymbolReference FindSymbolAtPosition(
            Ast scriptAst,
            int lineNumber,
            int columnNumber,
            bool includeFunctionDefinitions = false)
        {
            FindSymbolVisitor symbolVisitor =
                new FindSymbolVisitor(
                    lineNumber,
                    columnNumber,
                    includeFunctionDefinitions);

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
            FindDeclarationVisitor declarationVisitor =
                new FindDeclarationVisitor(
                    symbolReference);
            scriptAst.Visit(declarationVisitor);

            return declarationVisitor.FoundDeclaration;
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

        /// <summary>
        /// Finds all files dot sourced in a script
        /// </summary>
        /// <param name="scriptAst">The abstract syntax tree of the given script</param>
        /// <param name="psScriptRoot">Pre-calculated value of $PSScriptRoot</param>
        /// <returns></returns>
        static public string[] FindDotSourcedIncludes(Ast scriptAst, string psScriptRoot)
        {
            FindDotSourcedVisitor dotSourcedVisitor = new FindDotSourcedVisitor(psScriptRoot);
            scriptAst.Visit(dotSourcedVisitor);

            return dotSourcedVisitor.DotSourcedFiles.ToArray();
        }
    }
}
