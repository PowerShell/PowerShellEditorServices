// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        private readonly SymbolReference _symbolRef;
        private readonly IDictionary<string, List<string>> _cmdletToAliasDictionary;
        private readonly IDictionary<string, string> _aliasToCmdletDictionary;
        private readonly string _symbolRefCommandName;
        private readonly bool _needsAliases;

        public List<SymbolReference> FoundReferences { get; set; }

        /// <summary>
        /// Constructor used when searching for aliases is needed
        /// </summary>
        /// <param name="symbolReference">The found symbolReference that other symbols are being compared to</param>
        /// <param name="cmdletToAliasDictionary">Dictionary maping cmdlets to aliases for finding alias references</param>
        /// <param name="aliasToCmdletDictionary">Dictionary maping aliases to cmdlets for finding alias references</param>
        public FindReferencesVisitor(
            SymbolReference symbolReference,
            IDictionary<string, List<string>> cmdletToAliasDictionary = default,
            IDictionary<string, string> aliasToCmdletDictionary = default)
        {
            _symbolRef = symbolReference;
            FoundReferences = new List<SymbolReference>();

            if (cmdletToAliasDictionary is null || aliasToCmdletDictionary is null)
            {
                _needsAliases = false;
                return;
            }

            _needsAliases = true;
            _cmdletToAliasDictionary = cmdletToAliasDictionary;
            _aliasToCmdletDictionary = aliasToCmdletDictionary;

            // Try to get the symbolReference's command name of an alias. If a command name does not
            // exists (if the symbol isn't an alias to a command) set symbolRefCommandName to an
            // empty string.
            aliasToCmdletDictionary.TryGetValue(symbolReference.ScriptRegion.Text, out _symbolRefCommandName);

            if (_symbolRefCommandName == null)
            {
                _symbolRefCommandName = string.Empty;
            }
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

            if (_symbolRef.SymbolType.Equals(SymbolType.Function))
            {
                if (_needsAliases)
                {
                    // Try to get the commandAst's name and aliases.
                    //
                    // If a command does not exist (if the symbol isn't an alias to a command) set
                    // command to an empty string value string command.
                    //
                    // If the aliases do not exist (if the symbol isn't a command that has aliases)
                    // set aliases to an empty List<string>
                    _cmdletToAliasDictionary.TryGetValue(commandName, out List<string> aliases);
                    _aliasToCmdletDictionary.TryGetValue(commandName, out string command);
                    if (aliases == null) { aliases = new List<string>(); }
                    if (command == null) { command = string.Empty; }

                    // Check if the found symbol's name is the same as the commandAst's name OR
                    // if the symbol's name is an alias for this commandAst's name (commandAst is a cmdlet) OR
                    // if the symbol's name is the same as the commandAst's cmdlet name (commandAst is a alias)
                    if (commandName.Equals(_symbolRef.SymbolName, StringComparison.OrdinalIgnoreCase)
                        // Note that PowerShell command names and aliases are case insensitive.
                        || aliases.Exists((match) => string.Equals(match, _symbolRef.ScriptRegion.Text, StringComparison.OrdinalIgnoreCase))
                        || command.Equals(_symbolRef.ScriptRegion.Text, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(command)
                            && command.Equals(_symbolRefCommandName, StringComparison.OrdinalIgnoreCase)))
                    {
                        FoundReferences.Add(new SymbolReference(SymbolType.Function, commandNameAst.Extent));
                    }
                }
                else // search does not include aliases
                {
                    if (commandName.Equals(_symbolRef.SymbolName, StringComparison.OrdinalIgnoreCase))
                    {
                        FoundReferences.Add(new SymbolReference(SymbolType.Function, commandNameAst.Extent));
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

            if (_symbolRef.SymbolType.Equals(SymbolType.Function) &&
                nameExtent.Text.Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                FoundReferences.Add(new SymbolReference(SymbolType.Function, nameExtent));
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
            if (_symbolRef.SymbolType.Equals(SymbolType.Parameter) &&
                commandParameterAst.Extent.Text.Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                FoundReferences.Add(new SymbolReference(SymbolType.Parameter, commandParameterAst.Extent));
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
            if (_symbolRef.SymbolType.Equals(SymbolType.Variable)
                && variableExpressionAst.Extent.Text.Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                FoundReferences.Add(new SymbolReference(SymbolType.Variable, variableExpressionAst.Extent));
            }
            return AstVisitAction.Continue;
        }

        // Computes where the start of the actual function name is.
        private static (int, int) GetStartColumnAndLineNumbersFromAst(FunctionDefinitionAst ast)
        {
            int startColumnNumber = ast.Extent.StartColumnNumber;
            int startLineNumber = ast.Extent.StartLineNumber;
            int astOffset = ast.IsFilter ? "filter".Length : ast.IsWorkflow ? "workflow".Length : "function".Length;
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
