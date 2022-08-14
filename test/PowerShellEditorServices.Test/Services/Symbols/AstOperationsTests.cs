// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.Services.Symbols
{
    [Trait("Category", "AstOperations")]
    public class AstOperationsTests
    {
        private const string s_scriptString = @"function BasicFunction {}
BasicFunction

function          FunctionWithExtraSpace
{

} FunctionWithExtraSpace

function


       FunctionNameOnDifferentLine






           {}


    FunctionNameOnDifferentLine

            function IndentedFunction  { } IndentedFunction
";
        private static readonly ScriptBlockAst s_ast = (ScriptBlockAst)ScriptBlock.Create(s_scriptString).Ast;

        [Theory]
        [InlineData(1, 15, "BasicFunction")]
        [InlineData(2, 3, "BasicFunction")]
        [InlineData(4, 31, "FunctionWithExtraSpace")]
        [InlineData(7, 18, "FunctionWithExtraSpace")]
        [InlineData(12, 22, "FunctionNameOnDifferentLine")]
        [InlineData(22, 13, "FunctionNameOnDifferentLine")]
        [InlineData(24, 30, "IndentedFunction")]
        [InlineData(24, 52, "IndentedFunction")]
        public void CanFindSymbolAtPostion(int lineNumber, int columnNumber, string expectedName)
        {
            SymbolReference reference = AstOperations.FindSymbolAtPosition(s_ast, lineNumber, columnNumber);
            Assert.NotNull(reference);
            Assert.Equal(expectedName, reference.SymbolName);
        }

        [Theory]
        [MemberData(nameof(FindReferencesOfSymbolAtPostionData))]
        public void CanFindReferencesOfSymbolAtPostion(int lineNumber, int columnNumber, Range[] symbolRange)
        {
            SymbolReference symbol = AstOperations.FindSymbolAtPosition(s_ast, lineNumber, columnNumber);

            IEnumerable<SymbolReference> references = AstOperations.FindReferencesOfSymbol(s_ast, symbol);

            int positionsIndex = 0;
            foreach (SymbolReference reference in references)
            {
                Assert.Equal(symbolRange[positionsIndex].Start.Line, reference.ScriptRegion.StartLineNumber);
                Assert.Equal(symbolRange[positionsIndex].Start.Character, reference.ScriptRegion.StartColumnNumber);
                Assert.Equal(symbolRange[positionsIndex].End.Line, reference.ScriptRegion.EndLineNumber);
                Assert.Equal(symbolRange[positionsIndex].End.Character, reference.ScriptRegion.EndColumnNumber);

                positionsIndex++;
            }
        }

        public static object[][] FindReferencesOfSymbolAtPostionData { get; } = new object[][]
        {
            new object[] { 1, 15, new[] { new Range(1, 10, 1, 23), new Range(2, 1, 2, 14) } },
            new object[] { 2, 3, new[] { new Range(1, 10, 1, 23), new Range(2, 1, 2, 14) } },
            new object[] { 4, 31, new[] { new Range(4, 19, 4, 41), new Range(7, 3, 7, 25) } },
            new object[] { 7, 18, new[] { new Range(4, 19, 4, 41), new Range(7, 3, 7, 25) } },
            new object[] { 22, 13, new[] { new Range(12, 8, 12, 35), new Range(22, 5, 22, 32) } },
            new object[] { 12, 22, new[] { new Range(12, 8, 12, 35), new Range(22, 5, 22, 32) } },
            new object[] { 24, 30, new[] { new Range(24, 22, 24, 38), new Range(24, 44, 24, 60) } },
            new object[] { 24, 52, new[] { new Range(24, 22, 24, 38), new Range(24, 44, 24, 60) } },
        };
    }
}
