//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class ConsoleServiceTests : IDisposable
    {
        private ConsoleService consoleService;
        private PowerShellContext powerShellContext;
        private TestConsolePromptHandlerContext promptHandlerContext;

        private Dictionary<OutputType, string> outputPerType = 
            new Dictionary<OutputType, string>();

        const string TestOutputString = "This is a test.";

        const string PromptCaption = "Test Prompt";
        const string PromptMessage = "Make a selection";
        const int PromptDefault = 1;

        static readonly Tuple<string, string>[] PromptChoices =
            new Tuple<string, string>[]
            {
                new Tuple<string, string>("&Apple", "Help for Apple"),
                new Tuple<string, string>("Ba&nana", "Help for Banana"),
                new Tuple<string, string>("Orange", "Help for Orange")
            };

        static readonly Tuple<string, Type>[] PromptFields =
            new Tuple<string, Type>[]
            {
                new Tuple<string, Type>("Name", typeof(string)),
                new Tuple<string, Type>("Age", typeof(int)),
                new Tuple<string, Type>("Books", typeof(string[])),
            };

        public ConsoleServiceTests()
        {
            this.powerShellContext = new PowerShellContext();

            this.promptHandlerContext = 
                new TestConsolePromptHandlerContext();

            this.consoleService =
                new ConsoleService(
                    this.powerShellContext,
                    promptHandlerContext);

            this.consoleService.OutputWritten += OnOutputWritten;
            promptHandlerContext.ConsoleHost = this.consoleService;
        }

        public void Dispose()
        {
        }

        [Fact]
        public async Task ReceivesNormalOutput()
        {
            await this.powerShellContext.ExecuteScriptString(
                string.Format(
                    "\"{0}\"",
                    TestOutputString));

            // Prompt strings are returned as normal output, ignore the prompt
            string[] normalOutputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            // The output should be 3 lines: the expected string,
            // an empty line, and the prompt string.
            Assert.Equal(3, normalOutputLines.Length);
            Assert.Equal(
                TestOutputString,
                normalOutputLines[0]);
        }

        [Fact]
        public async Task ReceivesErrorOutput()
        {
            await this.powerShellContext.ExecuteScriptString(
                string.Format(
                    "Write-Error \"{0}\"",
                    TestOutputString));

            string errorString = this.GetOutputForType(OutputType.Error).Split('\r')[0];

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
            await this.powerShellContext.ExecuteScriptString("Write-Verbose \"Preloading\"");

            await this.powerShellContext.ExecuteScriptString(
                string.Format(
                    "$VerbosePreference = \"Continue\"; Write-Verbose \"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine,
                this.GetOutputForType(OutputType.Verbose));
        }

        [Fact]
        public async Task ReceivesDebugOutput()
        {
            // Since setting VerbosePreference causes other message to
            // be written out when we run our test, run a command preemptively
            // to flush out unwanted verbose messages
            await this.powerShellContext.ExecuteScriptString("Write-Verbose \"Preloading\"");

            await this.powerShellContext.ExecuteScriptString(
                string.Format(
                    "$DebugPreference = \"Continue\"; Write-Debug \"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine,
                this.GetOutputForType(OutputType.Debug));
        }

        [Fact]
        public async Task ReceivesWarningOutput()
        {
            await this.powerShellContext.ExecuteScriptString(
                string.Format(
                    "Write-Warning \"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine,
                this.GetOutputForType(OutputType.Warning));
        }

        [Fact]
        public async Task ReceivesChoicePrompt()
        {
            string choiceScript =
                this.GetChoicePromptString(
                    PromptCaption,
                    PromptMessage,
                    PromptChoices,
                    PromptDefault);

            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString(choiceScript);

            // Wait for the prompt to be shown
            await promptTask;

            // Respond to the prompt and wait for the prompt to complete
            this.consoleService.ReceiveInputString("apple", false);
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            Assert.Equal(PromptCaption, outputLines[0]);
            Assert.Equal(PromptMessage, outputLines[1]);
            Assert.Equal("[A] Apple [N] Banana [] Orange [?] Help (default is \"Banana\"):", outputLines[2]);
            Assert.Equal("0", outputLines[3]);
        }

        [Fact]
        public async Task CancelsChoicePrompt()
        {
            string choiceScript =
                this.GetChoicePromptString(
                    PromptCaption,
                    PromptMessage,
                    PromptChoices,
                    PromptDefault);

            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString(choiceScript);

            // Wait for the prompt to be shown
            await promptTask;

            // Cancel the prompt and wait for the execution to complete
            this.consoleService.SendControlC();
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            Assert.Equal(PromptCaption, outputLines[0]);
            Assert.Equal(PromptMessage, outputLines[1]);
            Assert.Equal("[A] Apple [N] Banana [] Orange [?] Help (default is \"Banana\"):", outputLines[2]);
            Assert.Equal("", outputLines[3]);
            Assert.True(outputLines[4].StartsWith("PS "));
        }

        [Fact]
        public async Task ReceivesChoicePromptHelp()
        {
            string choiceScript =
                this.GetChoicePromptString(
                    PromptCaption,
                    PromptMessage,
                    PromptChoices,
                    PromptDefault);

            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString(choiceScript);

            // Wait for the prompt to be shown
            await promptTask;

            // Respond to the prompt and wait for the help prompt to appear
            this.consoleService.ReceiveInputString("?", false);
            this.consoleService.ReceiveInputString("A", false);
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            // Help lines start after initial prompt, skip 3 lines
            Assert.Equal("A - Help for Apple", outputLines[3]);
            Assert.Equal("N - Help for Banana", outputLines[4]);
            Assert.Equal("Orange - Help for Orange", outputLines[5]);
        }

        [Fact]
        public async Task ReceivesInputPrompt()
        {
            string inputScript =
                this.GetInputPromptString(
                    PromptCaption,
                    PromptMessage,
                    PromptFields);

            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString(inputScript);

            // Wait for the prompt to be shown
            await promptTask;

            // Respond to the prompt and wait for execution to complete
            this.consoleService.ReceiveInputString("John", true);
            this.consoleService.ReceiveInputString("40", true);
            this.consoleService.ReceiveInputString("Windows PowerShell In Action", true);
            this.consoleService.ReceiveInputString("", false);
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            Assert.Equal(PromptCaption, outputLines[0]);
            Assert.Equal(PromptMessage, outputLines[1]);
            Assert.Equal("Name: John", outputLines[2]);
            Assert.Equal("Age: 40", outputLines[3]);
            Assert.Equal("Books[0]: Windows PowerShell In Action", outputLines[4]);
            Assert.Equal("Books[1]: ", outputLines[5]);
            Assert.Equal("Name  John", outputLines[8].Trim());
            Assert.Equal("Age   40", outputLines[9].Trim());
            Assert.Equal("Books {Windows PowerShell In Action}", outputLines[10].Trim());
        }

        [Fact]
        public async Task CancelsInputPrompt()
        {
            string inputScript =
                this.GetInputPromptString(
                    PromptCaption,
                    PromptMessage,
                    PromptFields);

            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString(inputScript);

            // Wait for the prompt to be shown
            await promptTask;

            // Cancel the prompt and wait for execution to complete
            this.consoleService.SendControlC();
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            Assert.Equal(PromptCaption, outputLines[0]);
            Assert.Equal(PromptMessage, outputLines[1]);
            Assert.Equal("Name: ", outputLines[2]);
            Assert.True(outputLines[3].StartsWith("PS "));
        }

        [Fact]
        public async Task ReceivesReadHostPrompt()
        {
            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString("Read-Host");

            // Wait for the prompt to be shown
            await promptTask;

            // Respond to the prompt and wait for execution to complete
            this.consoleService.ReceiveInputString("John", true);
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            Assert.Equal("John", outputLines[0]);
            Assert.Equal("John", outputLines[1]);
        }

        [Fact]
        public async Task CancelsReadHostPrompt()
        {
            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString("Read-Host");

            // Wait for the prompt to be shown
            await promptTask;

            // Cancel the prompt and wait for execution to complete
            this.consoleService.SendControlC();
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            Assert.Equal("", outputLines[0]);
            Assert.True(outputLines[1].StartsWith("PS "));
        }

        [Fact]
        public async Task ReceivesReadHostPromptWithFieldName()
        {
            var promptTask = this.promptHandlerContext.WaitForPrompt();
            var executeTask = this.powerShellContext.ExecuteScriptString("Read-Host -Prompt \"Name\"");

            // Wait for the prompt to be shown
            await promptTask;

            // Respond to the prompt and wait for execution to complete
            this.consoleService.ReceiveInputString("John", true);
            await executeTask;

            string[] outputLines =
                this.GetOutputForType(OutputType.Normal)
                    .Split(
                        new string[] { Environment.NewLine },
                        StringSplitOptions.None);

            Assert.Equal("Name: John", outputLines[0]);
            Assert.Equal("John", outputLines[1]);
        }

        #region Helper Methods

        void OnOutputWritten(object sender, OutputWrittenEventArgs e)
        {
            string storedOutputString = null;
            if (!this.outputPerType.TryGetValue(e.OutputType, out storedOutputString))
            {
                this.outputPerType.Add(e.OutputType, null);
            }

            if (storedOutputString == null)
            {
                storedOutputString = e.OutputText;
            }
            else
            {
                storedOutputString += e.OutputText;
            }

            if (e.IncludeNewLine)
            {
                storedOutputString += Environment.NewLine;
            }

            this.outputPerType[e.OutputType] = storedOutputString;
        }

        private string GetOutputForType(OutputType outputLineType)
        {
            string outputString = null;

            this.outputPerType.TryGetValue(outputLineType, out outputString);

            return outputString;
        }

        private string GetChoicePromptString(
            string caption,
            string message,
            Tuple<string, string>[] choices,
            int defaultChoice)
        {
            StringBuilder scriptBuilder = new StringBuilder();

            scriptBuilder.AppendFormat(
                "$caption = {0}\r\n",
                caption != null ?
                    "\"" + caption + "\"" :
                    "$null");

            scriptBuilder.AppendFormat(
                "$message = {0}\r\n",
                message != null ?
                    "\"" + message + "\"" :
                    "$null");

            scriptBuilder.AppendLine("$choices = [System.Management.Automation.Host.ChoiceDescription[]](");

            List<string> choiceItems = new List<string>();
            foreach (var choice in choices)
            {
                choiceItems.Add(
                    string.Format(
                        "  (new-Object System.Management.Automation.Host.ChoiceDescription \"{0}\",\"{1}\")",
                        choice.Item1,
                        choice.Item2));
            }

            scriptBuilder.AppendFormat(
                "{0})\r\n",
                string.Join(",\r\n", choiceItems));

            scriptBuilder.AppendFormat(
                "$host.ui.PromptForChoice($caption, $message, $choices, {0})\r\n",
                defaultChoice);

            return scriptBuilder.ToString();
        }

        private string GetInputPromptString(
            string caption,
            string message,
            Tuple<string, Type>[] fields)
        {
            StringBuilder scriptBuilder = new StringBuilder();

            scriptBuilder.AppendFormat(
                "$caption = {0}\r\n",
                caption != null ?
                    "\"" + caption + "\"" :
                    "$null");

            scriptBuilder.AppendFormat(
                "$message = {0}\r\n",
                message != null ?
                    "\"" + message + "\"" :
                    "$null");

            foreach (var field in fields)
            {
                scriptBuilder.AppendFormat(
                    "${0}Field = New-Object System.Management.Automation.Host.FieldDescription \"{0}\"\r\n${0}Field.SetParameterType([{1}])\r\n",
                    field.Item1,
                    field.Item2.FullName);
            }

            scriptBuilder.AppendFormat(
                "$fields = [System.Management.Automation.Host.FieldDescription[]]({0})\r\n",
                string.Join(
                    ", ",
                    fields.Select(
                        f => string.Format("${0}Field", f.Item1))));

            scriptBuilder.AppendLine(
                "$host.ui.Prompt($caption, $message, $fields)");

            return scriptBuilder.ToString();
        }

        #endregion
    }

    internal class TestConsolePromptHandlerContext : IPromptHandlerContext
    {
        private TaskCompletionSource<bool> promptShownTask;

        public IConsoleHost ConsoleHost { get; set; }

        public ChoicePromptHandler GetChoicePromptHandler()
        {
            return new TestConsoleChoicePromptHandler(
                this.ConsoleHost,
                this.promptShownTask);
        }

        public InputPromptHandler GetInputPromptHandler()
        {
            return new TestConsoleInputPromptHandler(
                this.ConsoleHost,
                this.promptShownTask);
        }

        public Task<bool> WaitForPrompt()
        {
            this.promptShownTask = new TaskCompletionSource<bool>();
            return this.promptShownTask.Task;
        }
    }

    internal class TestConsoleChoicePromptHandler : ConsoleChoicePromptHandler
    {
        private TaskCompletionSource<bool> promptShownTask;

        public TestConsoleChoicePromptHandler(
            IConsoleHost consoleHost,
            TaskCompletionSource<bool> promptShownTask) 
            : base(consoleHost)
        {
            this.promptShownTask = promptShownTask;
        }

        protected override void ShowPrompt(PromptStyle promptStyle)
        {
            base.ShowPrompt(promptStyle);

            if (this.promptShownTask != null &&
                this.promptShownTask.Task.Status != TaskStatus.RanToCompletion)
            {
                this.promptShownTask.SetResult(true);
            }
        }
    }

    internal class TestConsoleInputPromptHandler : ConsoleInputPromptHandler
    {
        private TaskCompletionSource<bool> promptShownTask;

        public TestConsoleInputPromptHandler(
            IConsoleHost consoleHost,
            TaskCompletionSource<bool> promptShownTask) 
            : base(consoleHost)
        {
            this.promptShownTask = promptShownTask;
        }

        protected override void ShowFieldPrompt(FieldDetails fieldDetails)
        {
            base.ShowFieldPrompt(fieldDetails);

            // Raise the task for the first field prompt shown
            if (this.promptShownTask != null &&
                this.promptShownTask.Task.Status == TaskStatus.WaitingForActivation)
            {
                this.promptShownTask.SetResult(true);
            }
        }
    }
}
