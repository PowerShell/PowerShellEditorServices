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
    /// enumerating test symbols in Pester test (tests.ps1) files.
    /// </summary>
    internal class PesterDocumentSymbolProvider : IDocumentSymbolProvider
    {
        string IDocumentSymbolProvider.ProviderId => nameof(PesterDocumentSymbolProvider);

        IEnumerable<ISymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            if (!scriptFile.FilePath.EndsWith(
                    "tests.ps1",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Empty<SymbolReference>();
            }

            // Find plausible Pester commands
            IEnumerable<Ast> commandAsts = scriptFile.ScriptAst.FindAll(IsNamedCommandWithArguments, true);

            return commandAsts.OfType<CommandAst>()
                              .Where(IsPesterCommand)
                              .Select(ast => ConvertPesterAstToSymbolReference(scriptFile, ast));
        }

        /// <summary>
        /// Test if the given Ast is a regular CommandAst with arguments
        /// </summary>
        /// <param name="ast">the PowerShell Ast to test</param>
        /// <returns>true if the Ast represents a PowerShell command with arguments, false otherwise</returns>
        private static bool IsNamedCommandWithArguments(Ast ast)
        {
            CommandAst commandAst = ast as CommandAst;

            return commandAst != null &&
                commandAst.InvocationOperator != TokenKind.Dot &&
                PesterSymbolReference.GetCommandType(commandAst.GetCommandName()).HasValue &&
                commandAst.CommandElements.Count >= 2;
        }

        /// <summary>
        /// Test whether the given CommandAst represents a Pester command
        /// </summary>
        /// <param name="commandAst">the CommandAst to test</param>
        /// <returns>true if the CommandAst represents a Pester command, false otherwise</returns>
        private static bool IsPesterCommand(CommandAst commandAst)
        {
            if (commandAst == null)
            {
                return false;
            }

            // Ensure the first word is a Pester keyword
            if (!PesterSymbolReference.PesterKeywords.ContainsKey(commandAst.GetCommandName()))
            {
                return false;
            }

            // Ensure that the last argument of the command is a scriptblock
            if (!(commandAst.CommandElements[commandAst.CommandElements.Count-1] is ScriptBlockExpressionAst))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert a CommandAst known to represent a Pester command and a reference to the scriptfile
        /// it is in into symbol representing a Pester call for code lens
        /// </summary>
        /// <param name="scriptFile">the scriptfile the Pester call occurs in</param>
        /// <param name="pesterCommandAst">the CommandAst representing the Pester call</param>
        /// <returns>a symbol representing the Pester call containing metadata for CodeLens to use</returns>
        private static PesterSymbolReference ConvertPesterAstToSymbolReference(ScriptFile scriptFile, CommandAst pesterCommandAst)
        {
            string testLine = scriptFile.GetLine(pesterCommandAst.Extent.StartLineNumber);
            PesterCommandType? commandName = PesterSymbolReference.GetCommandType(pesterCommandAst.GetCommandName());
            if (commandName == null)
            {
                return null;
            }

            // Search for a name for the test
            // If the test has more than one argument for names, we set it to null
            string testName = null;
            bool alreadySawName = false;
            for (int i = 1; i < pesterCommandAst.CommandElements.Count; i++)
            {
                CommandElementAst currentCommandElement = pesterCommandAst.CommandElements[i];

                // Check for an explicit "-Name" parameter
                if (currentCommandElement is CommandParameterAst parameterAst)
                {
                    // Found -Name parameter, move to next element which is the argument for -TestName
                    i++;

                    if (!alreadySawName && TryGetTestNameArgument(pesterCommandAst.CommandElements[i], out testName))
                    {
                        alreadySawName = true;
                    }

                    continue;
                }

                // Otherwise, if an argument is given with no parameter, we assume it's the name
                // If we've already seen a name, we set the name to null
                if (!alreadySawName && TryGetTestNameArgument(pesterCommandAst.CommandElements[i], out testName))
                {
                    alreadySawName = true;
                }
            }

            return new PesterSymbolReference(
                scriptFile,
                commandName.Value,
                testLine,
                testName,
                pesterCommandAst.Extent
            );
        }

        private static bool TryGetTestNameArgument(CommandElementAst commandElementAst, out string testName)
        {
            testName = null;

            if (commandElementAst is StringConstantExpressionAst testNameStrAst)
            {
                testName = testNameStrAst.Value;
                return true;
            }

            return (commandElementAst is ExpandableStringExpressionAst);
        }
    }

    /// <summary>
    /// Defines command types for Pester test blocks.
    /// </summary>
    internal enum PesterCommandType
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
    internal class PesterSymbolReference : SymbolReference
    {
        /// <summary>
        /// Lookup for Pester keywords we support. Ideally we could extract these from Pester itself
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, PesterCommandType> PesterKeywords =
            Enum.GetValues(typeof(PesterCommandType))
                .Cast<PesterCommandType>()
                .ToDictionary(pct => pct.ToString(), pct => pct, StringComparer.OrdinalIgnoreCase);

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
            PesterCommandType commandType,
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
            this.Command = commandType;
            this.TestName = testName;
        }

        internal static PesterCommandType? GetCommandType(string commandName)
        {
            PesterCommandType pesterCommandType;
            if (commandName == null || !PesterKeywords.TryGetValue(commandName, out pesterCommandType))
            {
                return null;
            }
            return pesterCommandType;
        }
    }
}
