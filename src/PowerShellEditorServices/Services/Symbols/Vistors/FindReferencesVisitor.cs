// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The visitor used to find the references of a symbol in a script's AST
    /// </summary>
    internal class FindReferencesVisitor : AstVisitor2
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
        /// <param name="functionDefinitionAst">A FunctionDefinitionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            // Extent for constructors and method trigger both this and VisitFunctionMember(). Covered in the latter.
            // This will not exclude nested functions as they have ScriptBlockAst as parent
            if (functionDefinitionAst.Parent is FunctionMemberAst)
            {
                return AstVisitAction.Continue;
            }

            if (_symbolRef.SymbolType.Equals(SymbolType.Function) &&
                functionDefinitionAst.Name.Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // We only want the function name
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionDefinitionAst);
                FoundReferences.Add(new SymbolReference(SymbolType.Function, nameExtent));
            }
            return base.VisitFunctionDefinition(functionDefinitionAst);
        }

        /// <summary>
        /// Decides if the current command parameter is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Parameter and have the same name as the symbol
        /// </summary>
        /// <param name="commandParameterAst">A CommandParameterAst in the script's AST</param>
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
        /// Decides if the current variable expression is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Variable and have the same name as the symbol
        /// </summary>
        /// <param name="variableExpressionAst">A VariableExpressionAst in the script's AST</param>
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

        /// <summary>
        /// Decides if the current type definition is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Class or SymbolType.Enum and have the same name as the symbol
        /// </summary>
        /// <param name="typeDefinitionAst">A TypeDefinitionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            SymbolType symbolType =
                typeDefinitionAst.IsEnum ?
                    SymbolType.Enum : SymbolType.Class;

            if ((_symbolRef.SymbolType is SymbolType.Type || _symbolRef.SymbolType.Equals(symbolType)) &&
                typeDefinitionAst.Name.Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // We only want the type name. Get start-location for name
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(typeDefinitionAst);
                FoundReferences.Add(new SymbolReference(symbolType, nameExtent));
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current type expression is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Type and have the same name as the symbol
        /// </summary>
        /// <param name="typeExpressionAst">A TypeExpressionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitTypeExpression(TypeExpressionAst typeExpressionAst)
        {
            // We don't know if we're looking at a class or enum, but name is likely unique
            if (IsTypeSymbol(_symbolRef.SymbolType) &&
                typeExpressionAst.TypeName.Name.Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                FoundReferences.Add(new SymbolReference(SymbolType.Type, typeExpressionAst.Extent));
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current type constraint is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Type and have the same name as the symbol
        /// </summary>
        /// <param name="typeConstraintAst">A TypeConstraintAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitTypeConstraint(TypeConstraintAst typeConstraintAst)
        {
            // We don't know if we're looking at a class or enum, but name is likely unique
            if (IsTypeSymbol(_symbolRef.SymbolType) &&
                typeConstraintAst.TypeName.Name.Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                FoundReferences.Add(new SymbolReference(SymbolType.Type, typeConstraintAst.Extent));
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current function member is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Constructor or SymbolType.Method and have the same name as the symbol
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            SymbolType symbolType =
                functionMemberAst.IsConstructor ?
                    SymbolType.Constructor : SymbolType.Method;

            if (_symbolRef.SymbolType.Equals(symbolType) &&
                VisitorUtils.GetMemberOverloadName(functionMemberAst).Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // We only want the method/ctor name. Get start-location for name
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionMemberAst);
                FoundReferences.Add(new SymbolReference(symbolType, nameExtent));
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current property member is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Property or SymbolType.EnumMember
        /// and have the same name as the symbol.
        /// </summary>
        /// <param name="propertyMemberAst">A PropertyMemberAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            SymbolType symbolType =
                propertyMemberAst.Parent is TypeDefinitionAst typeAst && typeAst.IsEnum ?
                    SymbolType.EnumMember : SymbolType.Property;

            if (_symbolRef.SymbolType.Equals(symbolType) &&
                VisitorUtils.GetMemberOverloadName(propertyMemberAst).Equals(_symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // We only want the property name. Get start-location for name
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(propertyMemberAst);
                FoundReferences.Add(new SymbolReference(SymbolType.Property, nameExtent));
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Tests if symbol type is a type (class/enum) definition or type reference.
        /// </summary>
        private static bool IsTypeSymbol(SymbolType symbolType)
            => symbolType is SymbolType.Class or SymbolType.Enum or SymbolType.Type;
    }
}
