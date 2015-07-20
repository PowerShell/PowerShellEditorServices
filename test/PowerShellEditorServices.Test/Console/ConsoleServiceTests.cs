//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using System;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class ConsoleServiceTests : IDisposable
    {
        private TestConsoleHost consoleHost;
        private ConsoleService consoleService;

        const string TestOutputString = "This is a test.";

        public ConsoleServiceTests()
        {
            this.consoleHost = new TestConsoleHost();
            this.consoleService =
                new ConsoleService(
                    consoleHost,
                    InitialSessionState.CreateDefault2());
        }

        public void Dispose()
        {
            // After all tests are complete, dispose of the ConsoleService
            this.consoleService.Dispose();
        }

        [Fact]
        public async Task ReceivesNormalOutput()
        {
            await this.consoleService.ExecuteCommand(
                string.Format(
                    "\"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine, 
                this.consoleHost.GetOutputForType(OutputType.Normal));
        }

        [Fact]
        public async Task ReceivesErrorOutput()
        {
            await this.consoleService.ExecuteCommand(
                string.Format(
                    "Write-Error \"{0}\"",
                    TestOutputString));

            string errorString = this.consoleHost.GetOutputForType(OutputType.Error).Split('\r')[0];

            Assert.Equal(
                string.Format("Write-Error \"{0}\" : {0}", TestOutputString),
                errorString);
        }

        [Fact]
        public async Task ReceivesVerboseOutput()
        {
            // Since setting VerbosePreference causes other message to
            // be written out when we run our test, run a command preemptively
            // to flush out unwanted verbose messages
            await this.consoleService.ExecuteCommand("Write-Verbose \"Preloading\"");

            await this.consoleService.ExecuteCommand(
                string.Format(
                    "$VerbosePreference = \"Continue\"; Write-Verbose \"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine,
                this.consoleHost.GetOutputForType(OutputType.Verbose));
        }

        [Fact]
        public async Task ReceivesDebugOutput()
        {
            // Since setting VerbosePreference causes other message to
            // be written out when we run our test, run a command preemptively
            // to flush out unwanted verbose messages
            await this.consoleService.ExecuteCommand("Write-Verbose \"Preloading\"");

            await this.consoleService.ExecuteCommand(
                string.Format(
                    "$DebugPreference = \"Continue\"; Write-Debug \"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine,
                this.consoleHost.GetOutputForType(OutputType.Debug));
        }

        [Fact]
        public async Task ReceivesWarningOutput()
        {
            await this.consoleService.ExecuteCommand(
                string.Format(
                    "Write-Warning \"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine,
                this.consoleHost.GetOutputForType(OutputType.Warning));
        }

        [Fact]
        public async Task ReceivesChoicePrompt()
        {
            string choiceScript =
                @"
                $caption = ""Test Choice"";
                $message = ""Make a selection"";
                $choiceA = new-Object System.Management.Automation.Host.ChoiceDescription ""&A"",""A"";
                $choiceB = new-Object System.Management.Automation.Host.ChoiceDescription ""&B"",""B"";
                $choices = [System.Management.Automation.Host.ChoiceDescription[]]($choiceA,$choiceB);
                $response = $host.ui.PromptForChoice($caption, $message, $choices, 1)
                $response";

            await this.consoleService.ExecuteCommand(choiceScript);

            // TODO: Verify prompt info

            // Verify prompt result written to output
            Assert.Equal(
                "1" + Environment.NewLine,
                this.consoleHost.GetOutputForType(OutputType.Normal));
        }
    }
}
