//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The visitor used to find the references of a symbol in a script's AST
    /// </summary>
    internal class FindReferencesVisitor : AstVisitor
    {
        private SymbolReference symbolRef;
        private Dictionary<String, List<String>> CmdletToAliasDictionary;
        private Dictionary<String, String> AliasToCmdletDictionary;
        private string symbolRefCommandName;
        private bool needsAliases;

        public List<SymbolReference> FoundReferences { get; set; }

        /// <summary>
        /// Constructor used when searching for aliases is needed
        /// </summary>
        /// <param name="symbolReference">The found symbolReference that other symbols are being compared to</param>
        /// <param name="CmdletToAliasDictionary">Dictionary maping cmdlets to aliases for finding alias references</param>
        /// <param name="AliasToCmdletDictionary">Dictionary maping aliases to cmdlets for finding alias references</param>
        public FindReferencesVisitor(
            SymbolReference symbolReference,
            Dictionary<String, List<String>> CmdletToAliasDictionary,
            Dictionary<String, String> AliasToCmdletDictionary)
        {
            this.symbolRef = symbolReference;
            this.FoundReferences = new List<SymbolReference>();
            this.needsAliases = true;
            this.CmdletToAliasDictionary = CmdletToAliasDictionary;
            this.AliasToCmdletDictionary = AliasToCmdletDictionary;

            // Try to get the symbolReference's command name of an alias,
            // if a command name does not exists (if the symbol isn't an alias to a command)
            // set symbolRefCommandName to and empty string value
            AliasToCmdletDictionary.TryGetValue(symbolReference.ScriptRegion.Text, out symbolRefCommandName);
            if (symbolRefCommandName == null) { symbolRefCommandName = string.Empty; }

        }

        /// <summary>
        /// Constructor used when searching for aliases is not needed
        /// </summary>
        /// <param name="foundSymbol">The found symbolReference that other symbols are being compared to</param>
        public FindReferencesVisitor(SymbolReference foundSymbol)
        {
            this.symbolRef = foundSymbol;
            this.FoundReferences = new List<SymbolReference>();
            this.needsAliases = false;
        }

        /// <summary>
        /// Decides if the current command is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Function
        /// and have the same name as the symbol
        /// </summary>
        /// <param name="commandAst">A CommandAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            Ast commandNameAst = commandAst.CommandElements[0];
            string commandName = commandNameAst.Extent.Text;

            if(symbolRef.SymbolType.Equals(SymbolType.Function))
            {
                if (needsAliases)
                {
                    // Try to get the commandAst's name and aliases,
                    // if a command does not exists (if the symbol isn't an alias to a command)
                    // set command to and empty string value string command
                    // if the aliases do not exist (if the symvol isn't a command that has aliases)
                    // set aliases to an empty List<string>
                    string command;
                    List<string> alaises;
                    CmdletToAliasDictionary.TryGetValue(commandName, out alaises);
                    AliasToCmdletDictionary.TryGetValue(commandName, out command);
                    if (alaises == null) { alaises = new List<string>(); }
                    if (command == null) { command = string.Empty; }

                    if (symbolRef.SymbolType.Equals(SymbolType.Function))
                    {
                        // Check if the found symbol's name is the same as the commandAst's name OR
                        // if the symbol's name is an alias for this commandAst's name (commandAst is a cmdlet) OR
                        // if the symbol's name is the same as the commandAst's cmdlet name (commandAst is a alias)
                        if (commandName.Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase) ||
                        alaises.Contains(symbolRef.ScriptRegion.Text.ToLower()) ||
                        command.Equals(symbolRef.ScriptRegion.Text, StringComparison.CurrentCultureIgnoreCase) ||
                        (!string.IsNullOrEmpty(command) && command.Equals(symbolRefCommandName, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            this.FoundReferences.Add(new SymbolReference(
                                SymbolType.Function,
                                commandNameAst.Extent));
                        }
                    }

                }
                else // search does not include aliases
                {
                    if (commandName.Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        this.FoundReferences.Add(new SymbolReference(
                            SymbolType.Function,
                            commandNameAst.Extent));
                    }
                }

            }
            return base.VisitCommand(commandAst);
        }

        /// <summary>
        /// Decides if the current function definition is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Function and have the same name as the symbol
        /// </summary>
        /// <param name="functionDefinitionAst">A functionDefinitionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            (int startColumnNumber, int startLineNumber) = GetStartColumnAndLineNumbersFromAst(functionDefinitionAst);

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = startLineNumber,
                EndLineNumber = startLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = startColumnNumber + functionDefinitionAst.Name.Length,
                File = functionDefinitionAst.Extent.File
            };

            if (symbolRef.SymbolType.Equals(SymbolType.Function) &&
                nameExtent.Text.Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                this.FoundReferences.Add(new SymbolReference(
                                          SymbolType.Function,
                                          nameExtent));
            }
            return base.VisitFunctionDefinition(functionDefinitionAst);
        }

        /// <summary>
        /// Decides if the current function definition is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Parameter and have the same name as the symbol
        /// </summary>
        /// <param name="commandParameterAst">A commandParameterAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitCommandParameter(CommandParameterAst commandParameterAst)
        {
            if (symbolRef.SymbolType.Equals(SymbolType.Parameter) &&
                commandParameterAst.Extent.Text.Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                this.FoundReferences.Add(new SymbolReference(
                                         SymbolType.Parameter,
                                         commandParameterAst.Extent));
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current function definition is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Variable and have the same name as the symbol
        /// </summary>
        /// <param name="variableExpressionAst">A variableExpressionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            if(symbolRef.SymbolType.Equals(SymbolType.Variable) &&
                variableExpressionAst.Extent.Text.Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                this.FoundReferences.Add(new SymbolReference(
                                         SymbolType.Variable,
                                         variableExpressionAst.Extent));
            }
            return AstVisitAction.Continue;
        }

        // Computes where the start of the actual function name is.
        private static (int, int) GetStartColumnAndLineNumbersFromAst(FunctionDefinitionAst ast)
        {
            int startColumnNumber = ast.Extent.StartColumnNumber;
            int startLineNumber = ast.Extent.StartLineNumber;
            int astOffset = 0;

            if (ast.IsFilter)
            {
                astOffset = "filter".Length;
            }
            else if (ast.IsWorkflow)
            {
                astOffset = "workflow".Length;
            }
            else
            {
                astOffset = "function".Length;
            }

            string astText = ast.Extent.Text;
            // The line offset represents the offset on the line that we're on where as
            // astOffset is the offset on the entire text of the AST.
            int lineOffset = astOffset;
            for (; astOffset < astText.Length; astOffset++, lineOffset++)
            {
                if (astText[astOffset] == '\n')
                {
                    // reset numbers since we are operating on a different line and increment the line number.
                    startColumnNumber = 0;
                    startLineNumber++;
                    lineOffset = 0;
                }
                else if (astText[astOffset] == '\r')
                {
                    // Do nothing with carriage returns... we only look for line feeds since those
                    // are used on every platform.
                }
                else if (!char.IsWhiteSpace(astText[astOffset]))
                {
                    // This is the start of the function name so we've found our start column and line number.
                    break;
                }
            }

            return (startColumnNumber + lineOffset, startLineNumber);
        }
    }
}
