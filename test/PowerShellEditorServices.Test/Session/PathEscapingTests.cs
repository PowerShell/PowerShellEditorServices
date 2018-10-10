using System;
using Xunit;
using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Session
{
    public class PathEscapingTests
    {
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
        public void CorrectlyGlobEscapesPaths_NoSpaces(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PowerShellContext.GlobEscapePath(unescapedPath);
            Assert.Equal(escapedPath, extensionEscapedPath);
        }

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
        public void CorrectlyGlobEscapesPaths_Spaces(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PowerShellContext.GlobEscapePath(unescapedPath, escapeSpaces: true);
            Assert.Equal(escapedPath, extensionEscapedPath);
        }

        [Theory]
        [InlineData("DebugTest.ps1", "'DebugTest.ps1'")]
        [InlineData("../../DebugTest.ps1", "'../../DebugTest.ps1'")]
        [InlineData("C:\\Users\\me\\Documents\\DebugTest.ps1", "'C:\\Users\\me\\Documents\\DebugTest.ps1'")]
        [InlineData("/home/me/Documents/weird&folder/script.ps1", "'/home/me/Documents/weird&folder/script.ps1'")]
        [InlineData("./path/with some/spaces", "'./path/with some/spaces'")]
        [InlineData("C:\\path\\with[some]brackets\\file.ps1", "'C:\\path\\with`[some`]brackets\\file.ps1'")]
        [InlineData("C:\\look\\an*\\here.ps1", "'C:\\look\\an`*\\here.ps1'")]
        [InlineData("/Users/me/Documents/?here.ps1", "'/Users/me/Documents/`?here.ps1'")]
        [InlineData("/Brackets [and s]paces/path.ps1", "'/Brackets `[and s`]paces/path.ps1'")]
        [InlineData("/file path/that isn't/normal/", "'/file path/that isn''t/normal/'")]
        public void CorrectlyFullyEscapesPaths_Spaces(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PowerShellContext.FullyPowerShellEscapePath(unescapedPath);
            Assert.Equal(escapedPath, extensionEscapedPath);
        }

        [Theory]
        [InlineData("DebugTest.ps1", "DebugTest.ps1")]
        [InlineData("../../DebugTest.ps1", "../../DebugTest.ps1")]
        [InlineData("C:\\Users\\me\\Documents\\DebugTest.ps1", "C:\\Users\\me\\Documents\\DebugTest.ps1")]
        [InlineData("/home/me/Documents/weird&folder/script.ps1", "/home/me/Documents/weird&folder/script.ps1")]
        [InlineData("./path/with` some/spaces", "./path/with some/spaces")]
        [InlineData("C:\\path\\with`[some`]brackets\\file.ps1", "C:\\path\\with[some]brackets\\file.ps1")]
        [InlineData("C:\\look\\an`*\\here.ps1", "C:\\look\\an*\\here.ps1")]
        [InlineData("/Users/me/Documents/`?here.ps1", "/Users/me/Documents/?here.ps1")]
        [InlineData("/Brackets` `[and` s`]paces/path.ps1", "/Brackets [and s]paces/path.ps1")]
        public void CorrectlyUnescapesPaths(string escapedPath, string expectedUnescapedPath)
        {
            string extensionUnescapedPath = PowerShellContext.UnescapeGlobEscapedPath(escapedPath);
            Assert.Equal(expectedUnescapedPath, extensionUnescapedPath);
        }
    }
}
