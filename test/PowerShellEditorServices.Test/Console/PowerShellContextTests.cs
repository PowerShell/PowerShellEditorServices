using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class PowerShellContextTests : IDisposable
    {
        private PowerShellContext powerShellContext;
        private AsyncProducerConsumerQueue<SessionStateChangedEventArgs> stateChangeQueue;
        private Dictionary<OutputType, string> outputPerType = 
            new Dictionary<OutputType, string>();

        // TODO: Use test constant class instead
        const string TestOutputString = "This is a test.";
        private const string DebugTestFilePath =
            @"..\..\..\PowerShellEditorServices.Test.Shared\Debugging\DebugTest.ps1";

        public PowerShellContextTests()
        {
            this.powerShellContext = new PowerShellContext();
            this.powerShellContext.SessionStateChanged += OnSessionStateChanged;
            this.powerShellContext.OutputWritten += OnOutputWritten;
            this.stateChangeQueue = new AsyncProducerConsumerQueue<SessionStateChangedEventArgs>();
        }

        public void Dispose()
        {
            this.powerShellContext.Dispose();
            this.powerShellContext = null;
        }

        [Fact]
        public async Task CanExecutePSCommand()
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddScript("$a = \"foo\"; $a");

            var executeTask =
                this.powerShellContext.ExecuteCommand<string>(psCommand);

            await this.AssertStateChange(PowerShellContextState.Running);
            await this.AssertStateChange(PowerShellContextState.Ready);

            var result = await executeTask;
            Assert.Equal("foo", result.First());
        }

        [Fact]
        public async Task CanQueueParallelRunspaceRequests()
        {
            // Concurrently initiate 4 requests in the session
            this.powerShellContext.ExecuteScriptString("$x = 100");
            Task<RunspaceHandle> handleTask = this.powerShellContext.GetRunspaceHandle();
            this.powerShellContext.ExecuteScriptString("$x += 200");
            this.powerShellContext.ExecuteScriptString("$x = $x / 100");

            PSCommand psCommand = new PSCommand();
            psCommand.AddScript("$x");
            Task<IEnumerable<int>> resultTask = this.powerShellContext.ExecuteCommand<int>(psCommand);

            // Wait for the requested runspace handle and then dispose it
            RunspaceHandle handle = await handleTask;
            handle.Dispose();

            // At this point, the remaining command executions should execute and complete
            int result = (await resultTask).FirstOrDefault();

            // 100 + 200 = 300, then divided by 100 is 3.  We are ensuring that
            // the commands were executed in the sequence they were called.
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task CanAbortExecution()
        {
            var executeTask =
                Task.Run(
                    async () =>
                    {
                        var unusedTask = this.powerShellContext.ExecuteScriptAtPath(DebugTestFilePath);
                        await Task.Delay(50);
                        this.powerShellContext.AbortExecution();
                    });

            await this.AssertStateChange(PowerShellContextState.Running);
            await this.AssertStateChange(PowerShellContextState.Aborting);
            await this.AssertStateChange(PowerShellContextState.Ready);

            await executeTask;
        }

        [Fact]
        public async Task ReceivesNormalOutput()
        {
            await this.powerShellContext.ExecuteScriptString(
                string.Format(
                    "\"{0}\"",
                    TestOutputString));

            Assert.Equal(
                TestOutputString + Environment.NewLine, 
                this.GetOutputForType(OutputType.Normal));
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

        #region Helper Methods

        public string GetOutputForType(OutputType outputLineType)
        {
            string outputString = null;

            this.outputPerType.TryGetValue(outputLineType, out outputString);

            return outputString;
        }

        private async Task AssertStateChange(PowerShellContextState expectedState)
        {
            SessionStateChangedEventArgs newState =
                await this.stateChangeQueue.DequeueAsync();

            Assert.Equal(expectedState, newState.NewSessionState);
        }

        private void OnSessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            this.stateChangeQueue.Enqueue(e);
        }

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

        #endregion
    }
}
