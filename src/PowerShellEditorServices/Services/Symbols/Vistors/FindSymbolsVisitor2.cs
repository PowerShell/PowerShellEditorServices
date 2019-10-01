//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    // TODO: Restore this when we figure out how to support multiple
    //       PS versions in the new PSES-as-a-module world (issue #276)

    ///// <summary>
    ///// The visitor used to find all the symbols (function and class defs) in the AST.
    ///// </summary>
    ///// <remarks>
    ///// Requires PowerShell v5 or higher
    ///// </remarks>
    /////
    //internal class FindSymbolsVisitor2 : AstVisitor2
    //{
    //    private FindSymbolsVisitor findSymbolsVisitor;

    //    public List<SymbolReference> SymbolReferences
    //    {
    //        get
    //        {
    //            return this.findSymbolsVisitor.SymbolReferences;
    //        }
    //    }

    //    public FindSymbolsVisitor2()
    //    {
    //        this.findSymbolsVisitor = new FindSymbolsVisitor();
    //    }

    //    /// <summary>
    //    /// Adds each function definition as a
    //    /// </summary>
    //    /// <param name="functionDefinitionAst">A functionDefinitionAst object in the script's AST</param>
    //    /// <returns>A decision to stop searching if the right symbol was found,
    //    /// or a decision to continue if it wasn't found</returns>
    //    public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
    //    {
    //        return this.findSymbolsVisitor.VisitFunctionDefinition(functionDefinitionAst);
    //    }

    //    /// <summary>
    //    ///  Checks to see if this variable expression is the symbol we are looking for.
    //    /// </summary>
    //    /// <param name="variableExpressionAst">A VariableExpressionAst object in the script's AST</param>
    //    /// <returns>A decision to stop searching if the right symbol was found,
    //    /// or a decision to continue if it wasn't found</returns>
    //    public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
    //    {
    //        return this.findSymbolsVisitor.VisitVariableExpression(variableExpressionAst);
    //    }

    //    public override AstVisitAction VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst)
    //    {
    //        IScriptExtent nameExtent = new ScriptExtent()
    //        {
    //            Text = configurationDefinitionAst.InstanceName.Extent.Text,
    //            StartLineNumber = configurationDefinitionAst.Extent.StartLineNumber,
    //            EndLineNumber = configurationDefinitionAst.Extent.EndLineNumber,
    //            StartColumnNumber = configurationDefinitionAst.Extent.StartColumnNumber,
    //            EndColumnNumber = configurationDefinitionAst.Extent.EndColumnNumber
    //        };

    //        this.findSymbolsVisitor.SymbolReferences.Add(
    //            new SymbolReference(
    //                SymbolType.Configuration,
    //                nameExtent));

    //        return AstVisitAction.Continue;
    //    }
    //}
}

