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
    /// enumerating test symbols in Pester test (tests.ps1) files.
    /// </summary>
    public class PesterDocumentSymbolProvider : FeatureProviderBase, IDocumentSymbolProvider
    {

        IEnumerable<SymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            if (!scriptFile.FilePath.EndsWith(
                    "tests.ps1",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Empty<SymbolReference>();
            }

            var commandAsts = scriptFile.ScriptAst.FindAll(ast =>
            {
            CommandAst commandAst = ast as CommandAst;

                return
                    commandAst != null &&
                    commandAst.InvocationOperator != TokenKind.Dot &&
                    PesterSymbolReference.GetCommandType(commandAst.GetCommandName()).HasValue &&
                    commandAst.CommandElements.Count >= 2;
            },
            true);

            return commandAsts.Select(
                ast =>
                {
                    // By this point we know the Ast is a CommandAst with 2 or more CommandElements
                    int testNameParamIndex = 1;
                    CommandAst testAst = (CommandAst)ast;

                    // The -Name parameter
                    for (int i = 1; i < testAst.CommandElements.Count; i++)
                    {
                        CommandParameterAst paramAst = testAst.CommandElements[i] as CommandParameterAst;
                        if (paramAst != null &&
                            paramAst.ParameterName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                        {
                            testNameParamIndex = i + 1;
                            break;
                        }
                    }

                    if (testNameParamIndex > testAst.CommandElements.Count - 1)
                    {
                        return null;
                    }

                    StringConstantExpressionAst stringAst =
                        testAst.CommandElements[testNameParamIndex] as StringConstantExpressionAst;

                    if (stringAst == null)
                    {
                        return null;
                    }

                    string testDefinitionLine =
                        scriptFile.GetLine(
                            ast.Extent.StartLineNumber);

                    return
                        new PesterSymbolReference(
                            scriptFile,
                            testAst.GetCommandName(),
                            testDefinitionLine,
                            stringAst.Value,
                            ast.Extent);

                }).Where(s => s != null);
        }
    }

    /// <summary>
    /// Defines command types for Pester test blocks.
    /// </summary>
    public enum PesterCommandType
    {
        /// <summary>
        /// Identifies a Describe block.
        /// </summary>
        Describe,

        /// <summary>
        /// Identifies a Context block.
        /// </summary>
        Context,

        /// <summary>
        /// Identifies an It block.
        /// </summary>
        It
    }

    /// <summary>
    /// Provides a specialization of SymbolReference containing
    /// extra information about Pester test symbols.
    /// </summary>
    public class PesterSymbolReference : SymbolReference
    {
        private static char[] DefinitionTrimChars = new char[] { ' ', '{' };

        /// <summary>
        /// Gets the name of the test
        /// </summary>
        public string TestName { get; private set; }

        /// <summary>
        /// Gets the test's command type.
        /// </summary>
        public PesterCommandType Command { get; private set; }

        internal PesterSymbolReference(
            ScriptFile scriptFile,
            string commandName,
            string testLine,
            string testName,
            IScriptExtent scriptExtent)
                : base(
                    SymbolType.Function,
                    testLine.TrimEnd(DefinitionTrimChars),
                    scriptExtent,
                    scriptFile.FilePath,
                    testLine)
        {
            this.Command = GetCommandType(commandName).Value;
            this.TestName = testName;
        }

        internal static PesterCommandType? GetCommandType(string commandName)
        {
            if (commandName == null)
            {
                return null;
            }

            switch (commandName.ToLower())
            {
                case "describe":
                    return PesterCommandType.Describe;

                case "context":
                    return PesterCommandType.Context;

                case "it":
                    return PesterCommandType.It;

                default:
                    return null;
            }
        }
    }
}
