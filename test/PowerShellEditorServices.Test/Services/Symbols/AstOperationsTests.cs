// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Services.Symbols
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
";
        private static readonly ScriptBlockAst s_ast = (ScriptBlockAst)ScriptBlock.Create(s_scriptString).Ast;

        [Theory]
        [InlineData(2, 3, "BasicFunction")]
        [InlineData(7, 18, "FunctionWithExtraSpace")]
        [InlineData(22, 13, "FunctionNameOnDifferentLine")]
        public void CanFindSymbolAtPostion(int lineNumber, int columnNumber, string expectedName)
        {
            SymbolReference reference = AstOperations.FindSymbolAtPosition(s_ast, lineNumber, columnNumber);
            Assert.NotNull(reference);
            Assert.Equal(expectedName, reference.SymbolName);
        }

        [Theory]
        [MemberData(nameof(FindReferencesOfSymbolAtPostionData))]
        public void CanFindReferencesOfSymbolAtPostion(int lineNumber, int columnNumber, Position[] positions)
        {
            SymbolReference symbol = AstOperations.FindSymbolAtPosition(s_ast, lineNumber, columnNumber);

            IEnumerable<SymbolReference> references = AstOperations.FindReferencesOfSymbol(s_ast, symbol);

            int positionsIndex = 0;
            foreach (SymbolReference reference in references)
            {
                Assert.Equal(positions[positionsIndex].Line, reference.ScriptRegion.StartLineNumber);
                Assert.Equal(positions[positionsIndex].Character, reference.ScriptRegion.StartColumnNumber);

                positionsIndex++;
            }
        }

        public static object[][] FindReferencesOfSymbolAtPostionData { get; } = new object[][]
        {
            new object[] { 2, 3, new[] { new Position(1, 10), new Position(2, 1) } },
            new object[] { 7, 18, new[] { new Position(4, 19), new Position(7, 3) } },
            new object[] { 22, 13, new[] { new Position(12, 8), new Position(22, 5) } },
        };
    }
}
