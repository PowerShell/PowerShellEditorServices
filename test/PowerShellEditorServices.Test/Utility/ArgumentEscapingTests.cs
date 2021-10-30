using Xunit;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Management.Automation;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Test.Session
{
    public class ArgumentEscapingTests
    {
        [Trait("Category", "ArgumentEscaping")]
        [Theory]
        [InlineData("/path/to/file", "/path/to/file")]
        [InlineData("'/path/to/file'", "'/path/to/file'")]
        [InlineData("not|allowed|pipeline", "'not|allowed|pipeline'")]
        [InlineData("doublequote\"inmiddle", "'doublequote\"inmiddle'")]
        [InlineData("am&persand", "'am&persand'")]
        [InlineData("semicolon;", "'semicolon;'")]
        [InlineData(":colon", "':colon'")]
        [InlineData(" has space s", "' has space s'")]
        [InlineData("[brackets]areOK", "[brackets]areOK")]
        [InlineData("$(expressionsAreOK)", "$(expressionsAreOK)")]
        [InlineData("{scriptBlocksAreOK}", "{scriptBlocksAreOK}")]
        public void CorrectlyEscapesPowerShellArguments(string Arg, string expectedArg)
        {
            string quotedArg = StringEscaping.EscapePowershellArgument(Arg);
            Assert.Equal(expectedArg, quotedArg);
        }

        [Trait("Category", "ArgumentEscaping")]
        [Theory]
        [InlineData("/path/to/file", "/path/to/file")]
        [InlineData("'/path/to/file'", "/path/to/file")]
        [InlineData("not|allowed|pipeline", "not|allowed|pipeline")]
        [InlineData("doublequote\"inmiddle", "doublequote\"inmiddle")]
        [InlineData("am&persand", "am&persand")]
        [InlineData("semicolon;", "semicolon;")]
        [InlineData(":colon", ":colon")]
        [InlineData(" has space s", " has space s")]
        [InlineData("[brackets]areOK", "[brackets]areOK")]
        // [InlineData("$(echo 'expressionsAreOK')", "expressionsAreOK")]
        // [InlineData("{scriptBlocksAreOK}", "{scriptBlocksAreOK}")]
        public void CanEvaluateArgumentsSafely(string Arg, string expectedOutput)
        {
            var escapedArg = StringEscaping.EscapePowershellArgument(Arg);
            var psCommand = new PSCommand().AddScript($"& Write-Output {escapedArg}");
            using var pwsh = System.Management.Automation.PowerShell.Create();
            pwsh.Commands = psCommand;
            var scriptOutput = pwsh.Invoke<string>().SingleOrDefault();
            Assert.Equal(expectedOutput, scriptOutput);
        }
    }
}
