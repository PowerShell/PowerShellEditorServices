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
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class SemanticTokenTest
    {
        [Fact]
        public async Task TokenizesFunctionElements()
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

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "function":
                    case "param":
                    case "return":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Keyword == sToken.Type);
                        break;
                    case "Get-Sum":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Function == sToken.Type);
                        break;
                    case "$a":
                    case "$b":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Variable == sToken.Type);
                        break;
                    case "[int]":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Type == sToken.Type);
                        break;
                    case "+":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Operator == sToken.Type);
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
            List<SemanticToken> mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(commandToken));
            Assert.Single(mappedTokens, sToken => SemanticTokenType.Function == sToken.Type);

            Token stringExpandableToken = scriptFile.ScriptTokens[1];
            mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(stringExpandableToken));
            Assert.Collection(mappedTokens,
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type),
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type),
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type)
            );
        }

        [Fact]
        public async Task RecognizesTokensWithAsterisk()
        {
            string text = @"
function Get-A*A {
}
Get-A*A
";
            ScriptFile scriptFile = new ScriptFile(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "function":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Keyword == sToken.Type);
                        break;
                    case "Get-A*A":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Function == sToken.Type);
                        break;
                }
            }
        }

        [Fact]
        public async Task RecognizesArrayMemberInExpandableString()
        {
            string text = "\"$(@($Array).Count) OtherText\"";
            ScriptFile scriptFile = new ScriptFile(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "$Array":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Variable == sToken.Type);
                        break;
                    case "Count":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Member == sToken.Type);
                        break;
                }
            }
        }

        [Fact]
        public async Task RecognizesCurlyQuotedString()
        {
            string text = "“^[-'a-z]*”";
            ScriptFile scriptFile = new ScriptFile(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            List<SemanticToken> mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(scriptFile.ScriptTokens[0]));
            Assert.Single(mappedTokens, sToken => SemanticTokenType.String == sToken.Type);
        }

        [Fact]
        public async Task RecognizeEnum()
        {
            string text =  @"
enum MyEnum{
    one
    two
    three
}
";
            ScriptFile scriptFile = new ScriptFile(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "enum":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Keyword == sToken.Type);
                        break;
                    case "MyEnum":
                    case "one":
                    case "two":
                    case "three":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Member == sToken.Type);
                        break;
                }
            }
        }
    }
}
