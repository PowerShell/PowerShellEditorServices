// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// Provides an IDocumentSymbolProvider implementation for
    /// enumerating test symbols in Pester test (tests.ps1) files.
    /// </summary>
    internal class PesterDocumentSymbolProvider : IDocumentSymbolProvider
    {
        string IDocumentSymbolProvider.ProviderId => nameof(PesterDocumentSymbolProvider);

        IEnumerable<SymbolReference> IDocumentSymbolProvider.ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            if (!scriptFile.FilePath.EndsWith(".tests.ps1", StringComparison.OrdinalIgnoreCase) &&
                !scriptFile.FilePath.EndsWith(".Koans.ps1", StringComparison.OrdinalIgnoreCase))
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
            return ast is CommandAst commandAst &&
                commandAst.InvocationOperator is not (TokenKind.Dot or TokenKind.Ampersand) &&
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

            // Ensure the first word is a Pester keyword and in Pester-module if using module-qualified call
            string commandName = CommandHelpers.StripModuleQualification(commandAst.GetCommandName(), out ReadOnlyMemory<char> module);
            if (!PesterSymbolReference.PesterKeywords.ContainsKey(commandName) ||
                (!module.IsEmpty && !string.Equals(module.ToString(), "pester", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Ensure that the last argument of the command is a scriptblock
            if (commandAst.CommandElements[commandAst.CommandElements.Count - 1] is not ScriptBlockExpressionAst)
            {
                return false;
            }

            return true;
        }

        private static readonly char[] DefinitionTrimChars = new char[] { ' ', '{' };

        /// <summary>
        /// Convert a CommandAst known to represent a Pester command and a reference to the scriptfile
        /// it is in into symbol representing a Pester call for code lens
        /// </summary>
        /// <param name="scriptFile">the scriptfile the Pester call occurs in</param>
        /// <param name="pesterCommandAst">the CommandAst representing the Pester call</param>
        /// <returns>a symbol representing the Pester call containing metadata for CodeLens to use</returns>
        private static PesterSymbolReference ConvertPesterAstToSymbolReference(ScriptFile scriptFile, CommandAst pesterCommandAst)
        {
            string symbolName = scriptFile
                .GetLine(pesterCommandAst.Extent.StartLineNumber)
                .TrimStart()
                .TrimEnd(DefinitionTrimChars);

            string commandName = CommandHelpers.StripModuleQualification(pesterCommandAst.GetCommandName(), out _);
            PesterCommandType? commandType = PesterSymbolReference.GetCommandType(commandName);
            if (commandType == null)
            {
                return null;
            }

            string testName = null;
            if (PesterSymbolReference.IsPesterTestCommand(commandType.Value))
            {
                // Search for a name for the test
                // If the test has more than one argument for names, we set it to null
                bool alreadySawName = false;
                for (int i = 1; i < pesterCommandAst.CommandElements.Count; i++)
                {
                    CommandElementAst currentCommandElement = pesterCommandAst.CommandElements[i];

                    // Check for an explicit "-Name" parameter
                    if (currentCommandElement is CommandParameterAst)
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
            }

            return new PesterSymbolReference(
                scriptFile,
                commandType.Value,
                symbolName,
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

            return commandElementAst is ExpandableStringExpressionAst;
        }
    }

    /// <summary>
    /// Defines command types for Pester blocks.
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
        It,

        /// <summary>
        /// Identifies an BeforeAll block.
        /// </summary>
        BeforeAll,

        /// <summary>
        /// Identifies an BeforeEach block.
        /// </summary>
        BeforeEach,

        /// <summary>
        /// Identifies an AfterAll block.
        /// </summary>
        AfterAll,

        /// <summary>
        /// Identifies an AfterEach block.
        /// </summary>
        AfterEach,

        /// <summary>
        /// Identifies an BeforeDiscovery block.
        /// </summary>
        BeforeDiscovery
    }

    /// <summary>
    /// Provides a specialization of SymbolReference containing
    /// extra information about Pester test symbols.
    /// </summary>
    internal record PesterSymbolReference : SymbolReference
    {
        /// <summary>
        /// Lookup for Pester keywords we support. Ideally we could extract these from Pester itself
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, PesterCommandType> PesterKeywords =
            Enum.GetValues(typeof(PesterCommandType))
                .Cast<PesterCommandType>()
                .ToDictionary(pct => pct.ToString(), pct => pct, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the name of the test
        /// TODO: We could get rid of this and use DisplayName now, but first attempt didn't work great.
        /// </summary>
        public string TestName { get; }

        /// <summary>
        /// Gets the test's command type.
        /// </summary>
        public PesterCommandType Command { get; }

        internal PesterSymbolReference(
            ScriptFile scriptFile,
            PesterCommandType commandType,
            string symbolName,
            string testName,
            IScriptExtent scriptExtent)
                : base(
                    SymbolType.Function,
                    symbolName,
                    symbolName + " { }",
                    scriptExtent,
                    scriptExtent,
                    scriptFile,
                    isDeclaration: true)
        {
            Command = commandType;
            TestName = testName;
        }

        internal static PesterCommandType? GetCommandType(string commandName)
        {
            if (commandName == null || !PesterKeywords.TryGetValue(commandName, out PesterCommandType pesterCommandType))
            {
                return null;
            }
            return pesterCommandType;
        }

        /// <summary>
        /// Checks if the PesterCommandType is a block with executable tests (Describe/Context/It).
        /// </summary>
        /// <param name="pesterCommandType">the PesterCommandType representing the Pester command</param>
        /// <returns>True if command type is a block used to trigger test run. False if setup/teardown/support-block.</returns>
        internal static bool IsPesterTestCommand(PesterCommandType pesterCommandType)
        {
            return pesterCommandType is
                PesterCommandType.Describe or
                PesterCommandType.Context or
                PesterCommandType.It;
        }
    }
}
