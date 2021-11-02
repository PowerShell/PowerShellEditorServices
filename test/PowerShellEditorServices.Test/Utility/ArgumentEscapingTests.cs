using Xunit;
using Microsoft.PowerShell.EditorServices.Utility;
using System.IO;
using System.Management.Automation;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Test.Session
{
    public class ArgumentEscapingTests
    {
        [Trait("Category", "ArgumentEscaping")]
        [Theory]
        [InlineData(" has spaces", "\" has spaces\"")]
        [InlineData("-Parameter", "-Parameter")]
        [InlineData("' single quote left alone'", "' single quote left alone'")]
        [InlineData("\"double quote left alone\"", "\"double quote left alone\"")]
        [InlineData("/path/to/fi le", "\"/path/to/fi le\"")]
        [InlineData("'/path/to/fi le'", "'/path/to/fi le'")]
        [InlineData("|pipeline", "|pipeline")]
        [InlineData("am&pe rsand", "\"am&pe rsand\"")]
        [InlineData("semicolon ;", "\"semicolon ;\"")]
        [InlineData(": colon", "\": colon\"")]
        [InlineData("$(expressions should be quoted)", "\"$(expressions should be quoted)\"")]
        [InlineData("{scriptBlocks should not have escaped-spaces}", "{scriptBlocks should not have escaped-spaces}")]
        [InlineData("-Parameter test", "\"-Parameter test\"")] //This is invalid, but should be obvious enough looking at the PSIC invocation
        public void CorrectlyEscapesPowerShellArguments(string Arg, string expectedArg)
        {
            string quotedArg = ArgumentEscaping.Escape(Arg);
            Assert.Equal(expectedArg, quotedArg);
        }

        [Trait("Category", "ArgumentEscaping")]
        [Theory]
        [InlineData("/path/t o/file", "/path/t o/file")]
        [InlineData("/path/with/$(echo 'expression')inline", "/path/with/expressioninline")]
        [InlineData("/path/with/$(echo 'expression') inline", "/path/with/expression inline")]
        [InlineData("am&per sand", "am&per sand")]
        [InlineData("'inner\"\"quotes'", "inner\"\"quotes")]
        public void CanEvaluateArguments(string Arg, string expectedOutput)
        {
            var escapedArg = ArgumentEscaping.Escape(Arg);
            var psCommand = new PSCommand().AddScript($"& Write-Output {escapedArg}");
            using var pwsh = System.Management.Automation.PowerShell.Create();
            pwsh.Commands = psCommand;
            var scriptOutput = pwsh.Invoke<string>().First();
            Assert.Equal(expectedOutput, scriptOutput);
        }

        [Trait("Category", "ArgumentEscaping")]
        [Theory]
        [InlineData("NormalScript.ps1")]
        [InlineData("Bad&name4script.ps1")]
        [InlineData("[Truly] b&d Name_4_script.ps1")]
        public void CanDotSourcePath(string rawFileName)
        {
            var ScriptAssetPath = @"..\..\..\..\PowerShellEditorServices.Test.Shared\scriptassets";
            var fullPath = Path.Combine(ScriptAssetPath, rawFileName);
            var escapedPath = PathUtils.WildcardEscapePath(fullPath).ToString();
            var psCommand = new PSCommand().AddScript($"& \"{escapedPath}\"");

            using var pwsh = System.Management.Automation.PowerShell.Create();
            pwsh.Commands = psCommand;
            pwsh.Invoke();
        }
    }
}
