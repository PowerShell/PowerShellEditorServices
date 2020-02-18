using Xunit;
using System.IO;
using Microsoft.PowerShell.EditorServices.Services;

namespace Microsoft.PowerShell.EditorServices.Test.Session
{
    public class PathEscapingTests
    {
        private const string ScriptAssetPath = @"..\..\..\..\PowerShellEditorServices.Test.Shared\scriptassets";

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
        public void CorrectlyWildcardEscapesPaths_NoSpaces(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PowerShellContextService.WildcardEscapePath(unescapedPath);
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
        public void CorrectlyWildcardEscapesPaths_Spaces(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PowerShellContextService.WildcardEscapePath(unescapedPath, escapeSpaces: true);
            Assert.Equal(escapedPath, extensionEscapedPath);
        }

        [Trait("Category", "PathEscaping")]
        [Theory]
        [InlineData("DebugTest.ps1", "'DebugTest.ps1'")]
        [InlineData("../../DebugTest.ps1", "'../../DebugTest.ps1'")]
        [InlineData("C:\\Users\\me\\Documents\\DebugTest.ps1", "'C:\\Users\\me\\Documents\\DebugTest.ps1'")]
        [InlineData("/home/me/Documents/weird&folder/script.ps1", "'/home/me/Documents/weird&folder/script.ps1'")]
        [InlineData("./path/with some/spaces", "'./path/with some/spaces'")]
        [InlineData("C:\\path\\with[some]brackets\\file.ps1", "'C:\\path\\with[some]brackets\\file.ps1'")]
        [InlineData("C:\\look\\an*\\here.ps1", "'C:\\look\\an*\\here.ps1'")]
        [InlineData("/Users/me/Documents/?here.ps1", "'/Users/me/Documents/?here.ps1'")]
        [InlineData("/Brackets [and s]paces/path.ps1", "'/Brackets [and s]paces/path.ps1'")]
        [InlineData("/file path/that isn't/normal/", "'/file path/that isn''t/normal/'")]
        [InlineData("/CJK.chars/脚本/hello.ps1", "'/CJK.chars/脚本/hello.ps1'")]
        [InlineData("/CJK chars/脚本/[hello].ps1", "'/CJK chars/脚本/[hello].ps1'")]
        [InlineData("C:\\Animal s\\утка\\quack.ps1", "'C:\\Animal s\\утка\\quack.ps1'")]
        [InlineData("C:\\&nimals\\утка\\qu*ck?.ps1", "'C:\\&nimals\\утка\\qu*ck?.ps1'")]
        public void CorrectlyQuoteEscapesPaths(string unquotedPath, string expectedQuotedPath)
        {
            string extensionQuotedPath = PowerShellContextService.QuoteEscapeString(unquotedPath);
            Assert.Equal(expectedQuotedPath, extensionQuotedPath);
        }

        [Trait("Category", "PathEscaping")]
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
        [InlineData("/CJK.chars/脚本/hello.ps1", "'/CJK.chars/脚本/hello.ps1'")]
        [InlineData("/CJK chars/脚本/[hello].ps1", "'/CJK chars/脚本/`[hello`].ps1'")]
        [InlineData("C:\\Animal s\\утка\\quack.ps1", "'C:\\Animal s\\утка\\quack.ps1'")]
        [InlineData("C:\\&nimals\\утка\\qu*ck?.ps1", "'C:\\&nimals\\утка\\qu`*ck`?.ps1'")]
        public void CorrectlyFullyEscapesPaths(string unescapedPath, string escapedPath)
        {
            string extensionEscapedPath = PowerShellContextService.FullyPowerShellEscapePath(unescapedPath);
            Assert.Equal(escapedPath, extensionEscapedPath);
        }

        [Trait("Category", "PathEscaping")]
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
        [InlineData("/CJK` chars/脚本/hello.ps1", "/CJK chars/脚本/hello.ps1")]
        [InlineData("/CJK` chars/脚本/`[hello`].ps1", "/CJK chars/脚本/[hello].ps1")]
        [InlineData("C:\\Animal` s\\утка\\quack.ps1", "C:\\Animal s\\утка\\quack.ps1")]
        [InlineData("C:\\&nimals\\утка\\qu`*ck`?.ps1", "C:\\&nimals\\утка\\qu*ck?.ps1")]
        public void CorrectlyUnescapesPaths(string escapedPath, string expectedUnescapedPath)
        {
            string extensionUnescapedPath = PowerShellContextService.UnescapeWildcardEscapedPath(escapedPath);
            Assert.Equal(expectedUnescapedPath, extensionUnescapedPath);
        }

        [Trait("Category", "PathEscaping")]
        [Theory]
        [InlineData("NormalScript.ps1")]
        [InlineData("Bad&name4script.ps1")]
        [InlineData("[Truly] b&d Name_4_script.ps1")]
        public void CanDotSourcePath(string rawFileName)
        {
            string fullPath = Path.Combine(ScriptAssetPath, rawFileName);
            string quotedPath = PowerShellContextService.QuoteEscapeString(fullPath);

            var psCommand = new System.Management.Automation.PSCommand().AddScript($". {quotedPath}");

            using (var pwsh = System.Management.Automation.PowerShell.Create())
            {
                pwsh.Commands = psCommand;
                pwsh.Invoke();
            }
        }
    }
}
