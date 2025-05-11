// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.Language
{
    public class SemanticTokenTest
    {
        [Fact]
        public void TokenizesFunctionElements()
        {
            const string text = @"
function Get-Sum {
    param( [parameter()] [int]$a, [int]$b )
    :loopLabel while (0) {break loopLabel}
    return $a + $b
}
";
            ScriptFile scriptFile = ScriptFile.Create(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "function":
                    case "param":
                    case "return":
                    case "while":
                    case "break":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Keyword == sToken.Type);
                        break;
                    case "parameter":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Decorator == sToken.Type);
                        break;
                    case "0":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Number == sToken.Type);
                        break;
                    case ":loopLabel":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Label == sToken.Type);
                        break;
                    case "loopLabel":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Property == sToken.Type);
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
        public void TokenizesStringExpansion()
        {
            const string text = "Write-Host \"$(Test-Property Get-Whatever) $(Get-Whatever)\"";
            ScriptFile scriptFile = ScriptFile.Create(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            Token commandToken = scriptFile.ScriptTokens[0];
            List<SemanticToken> mappedTokens = new(PsesSemanticTokensHandler.ConvertToSemanticTokens(commandToken));
            Assert.Single(mappedTokens, sToken => SemanticTokenType.Function == sToken.Type);

            Token stringExpandableToken = scriptFile.ScriptTokens[1];
            mappedTokens = new List<SemanticToken>(PsesSemanticTokensHandler.ConvertToSemanticTokens(stringExpandableToken));
            Assert.Collection(mappedTokens,
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type),
                sToken => Assert.Equal(SemanticTokenType.Function, sToken.Type)
            );
        }

        [Fact]
        public void RecognizesTokensWithAsterisk()
        {
            const string text = @"
function Get-A*A {
}
Get-A*A
";
            ScriptFile scriptFile = ScriptFile.Create(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "function":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Keyword == sToken.Type);
                        break;
                    case "Get-A*A":
                        if (t.TokenFlags.HasFlag(TokenFlags.CommandName))
                        {
                            Assert.Single(mappedTokens, sToken => SemanticTokenType.Function == sToken.Type);
                        }

                        break;
                }
            }
        }

        [Fact]
        public void RecognizesArrayPropertyInExpandableString()
        {
            const string text = "\"$(@($Array).Count) OtherText\"";
            ScriptFile scriptFile = ScriptFile.Create(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "$Array":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Variable == sToken.Type);
                        break;
                    case "Count":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Property == sToken.Type);
                        break;
                }
            }
        }

        [Fact]
        public void RecognizesCurlyQuotedString()
        {
            const string text = "“^[-'a-z]*”";
            ScriptFile scriptFile = ScriptFile.Create(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            List<SemanticToken> mappedTokens = new(PsesSemanticTokensHandler.ConvertToSemanticTokens(scriptFile.ScriptTokens[0]));
            Assert.Single(mappedTokens, sToken => SemanticTokenType.String == sToken.Type);
        }

        [Fact]
        public void RecognizeEnum()
        {
            const string text = @"
enum MyEnum{
    one
    two
    three
}
";
            ScriptFile scriptFile = ScriptFile.Create(
                // Use any absolute path. Even if it doesn't exist.
                DocumentUri.FromFileSystemPath(Path.Combine(Path.GetTempPath(), "TestFile.ps1")),
                text,
                Version.Parse("5.0"));

            foreach (Token t in scriptFile.ScriptTokens)
            {
                List<SemanticToken> mappedTokens = new(PsesSemanticTokensHandler.ConvertToSemanticTokens(t));
                switch (t.Text)
                {
                    case "enum":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Keyword == sToken.Type);
                        break;
                    case "MyEnum":
                    case "one":
                    case "two":
                    case "three":
                        Assert.Single(mappedTokens, sToken => SemanticTokenType.Property == sToken.Type);
                        break;
                }
            }
        }
    }
}
