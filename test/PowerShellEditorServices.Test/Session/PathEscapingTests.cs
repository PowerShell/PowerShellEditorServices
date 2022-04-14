// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Test.Session
{
    public class PathEscapingTests
    {
        [Trait("Category", "PathEscaping")]
        [Theory]
        [InlineData("DebugTest.ps1", "DebugTest.ps1")]
        [InlineData("../../DebugTest.ps1", "../../DebugTest.ps1")]
        [InlineData("C:\\Users\\me\\Documents\\DebugTest.ps1", "C:\\Users\\me\\Documents\\DebugTest.ps1")]
        [InlineData("/home/me/Documents/weird&folder/script.ps1", "/home/me/Documents/weird&folder/script.ps1")]
        [InlineData("./path/with some/spaces", "./path/with some/spaces")]
        [InlineData("C:\\path\\with[some]brackets\\file.ps1", "C:\\path\\with`[some`]brackets\\file.ps1")]
        [InlineData("C:\\look\\an*\\here.ps1", "C:\\look\\an`*\\here.ps1")]
        [InlineData("/Users/me/Documents/?here.ps1", "/Users/me/Documents/`?here.ps1")]
        [InlineData("/Brackets [and s]paces/path.ps1", "/Brackets `[and s`]paces/path.ps1")]
        [InlineData("/CJK.chars/脚本/hello.ps1", "/CJK.chars/脚本/hello.ps1")]
        [InlineData("/CJK.chars/脚本/[hello].ps1", "/CJK.chars/脚本/`[hello`].ps1")]
        [InlineData("C:\\Animals\\утка\\quack.ps1", "C:\\Animals\\утка\\quack.ps1")]
        [InlineData("C:\\&nimals\\утка\\qu*ck?.ps1", "C:\\&nimals\\утка\\qu`*ck`?.ps1")]
        public void CorrectlyWildcardEscapesPathsNoSpaces(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PathUtils.WildcardEscapePath(unescapedPath);
            Assert.Equal(escapedPath, extensionEscapedPath);
        }

        [Trait("Category", "PathEscaping")]
        [Theory]
        [InlineData("DebugTest.ps1", "DebugTest.ps1")]
        [InlineData("../../DebugTest.ps1", "../../DebugTest.ps1")]
        [InlineData("C:\\Users\\me\\Documents\\DebugTest.ps1", "C:\\Users\\me\\Documents\\DebugTest.ps1")]
        [InlineData("/home/me/Documents/weird&folder/script.ps1", "/home/me/Documents/weird&folder/script.ps1")]
        [InlineData("./path/with some/spaces", "./path/with` some/spaces")]
        [InlineData("C:\\path\\with[some]brackets\\file.ps1", "C:\\path\\with`[some`]brackets\\file.ps1")]
        [InlineData("C:\\look\\an*\\here.ps1", "C:\\look\\an`*\\here.ps1")]
        [InlineData("/Users/me/Documents/?here.ps1", "/Users/me/Documents/`?here.ps1")]
        [InlineData("/Brackets [and s]paces/path.ps1", "/Brackets` `[and` s`]paces/path.ps1")]
        [InlineData("/CJK chars/脚本/hello.ps1", "/CJK` chars/脚本/hello.ps1")]
        [InlineData("/CJK chars/脚本/[hello].ps1", "/CJK` chars/脚本/`[hello`].ps1")]
        [InlineData("C:\\Animal s\\утка\\quack.ps1", "C:\\Animal` s\\утка\\quack.ps1")]
        [InlineData("C:\\&nimals\\утка\\qu*ck?.ps1", "C:\\&nimals\\утка\\qu`*ck`?.ps1")]
        public void CorrectlyWildcardEscapesPathsSpaces(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PathUtils.WildcardEscapePath(unescapedPath, escapeSpaces: true);
            Assert.Equal(escapedPath, extensionEscapedPath);
        }
    }
}
