//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document.Proposals;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class SemanticTokenTest
    {
        [Fact]
        public async Task MapsFunctionElements()
        {
            string text = @"
function Get-Sum {
    param( [int]$a, [int]$b )
    return $a + $b
}
";
            ScriptFile scriptFile = new ScriptFile(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach(Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = PsesSemanticTokens.ConvertToSemanticTokens(t);
                switch(t.Text)
                {
                    case "function":
                    case "param":
                    case "return":
                        Assert.Single(mappedTokens);
                        Assert.Equal(SemanticTokenType.Keyword, mappedTokens[0].Type);
                        break;
                    case "Get-Sum":
                        Assert.Single(mappedTokens);
                        Assert.Equal(SemanticTokenType.Function, mappedTokens[0].Type);
                        break;
                    case "$a":
                    case "$b":
                        Assert.Single(mappedTokens);
                        Assert.Equal(SemanticTokenType.Variable, mappedTokens[0].Type);
                        break;
                    case "[int]":
                        Assert.Single(mappedTokens);
                        Assert.Equal(SemanticTokenType.Type, mappedTokens[0].Type);
                        break;
                    case "+":
                        Assert.Single(mappedTokens);
                        Assert.Equal(SemanticTokenType.Operator, mappedTokens[0].Type);
                        break;
                }
            }
        }

        [Fact]
        public async Task TokenizesStringExpansion()
        {
            string text = "Write-Host \"$(Test-Property Get-Whatever) $(Get-Whatever)\"";
            ScriptFile scriptFile = new ScriptFile(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            Token commandToken = scriptFile.ScriptTokens[0];
            List<SemanticToken> mappedTokens = PsesSemanticTokens.ConvertToSemanticTokens(commandToken);
            Assert.Single(mappedTokens);
            Assert.Equal(SemanticTokenType.Function, mappedTokens[0].Type);

            Token stringExpandableToken = scriptFile.ScriptTokens[1];
            mappedTokens = PsesSemanticTokens.ConvertToSemanticTokens(stringExpandableToken);
            Assert.Collection(mappedTokens,
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type),
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type),
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type)
            );
        }
    }
}
