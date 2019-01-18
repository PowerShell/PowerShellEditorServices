//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.Client;
using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class DebugAdapterTests : ServerTestsBase, IAsyncLifetime
    {
        private ILogger logger;
        private DebugAdapterClient debugAdapterClient;
        private string DebugScriptPath =
            Path.GetFullPath(TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Debugging/DebugTest.ps1"));

        public async Task InitializeAsync()
        {
            string testLogPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "logs",
                    this.GetType().Name,
                    Guid.NewGuid().ToString().Substring(0, 8));

            this.logger = Logging.CreateLogger()
                            .LogLevel(LogLevel.Verbose)
                            .AddLogFile(testLogPath + "-client.log")
                            .Build();

            testLogPath += "-server.log";
            System.Console.WriteLine("        Output log at path: {0}", testLogPath);

            Tuple<string, string> pipeNames =
                await this.LaunchService(
                    testLogPath,
                    waitForDebugger: false);
                    //waitForDebugger: true);

            this.debugAdapterClient =
                new DebugAdapterClient(
                    await NamedPipeClientChannel.ConnectAsync(
                        pipeNames.Item2,
                        MessageProtocolType.DebugAdapter,
                        this.logger),
                    this.logger);

            this.messageSender = this.debugAdapterClient;
            this.messageHandlers = this.debugAdapterClient;

            await this.debugAdapterClient.StartAsync();
        }

        public Task DisposeAsync()
        {
            this.debugAdapterClient.Stop();
            this.KillService();

            return Task.FromResult(true);
        }

        [Fact]
        public async Task DebugAdapterStopsOnLineBreakpoints()
        {
            await this.SendRequest(
                SetBreakpointsRequest.Type,
                new SetBreakpointsRequestArguments
                {
                    Source = new Source
                    {
                        Path = DebugScriptPath
                    },
                    Breakpoints = new []
                    {
                        new SourceBreakpoint { Line = 5 },
                        new SourceBreakpoint { Line = 7 }
                    }
                });

            Task<StoppedEventBody> breakEventTask = this.WaitForEvent(StoppedEvent.Type);
            await this.LaunchScript(DebugScriptPath);

            // Wait for a couple breakpoints
            StoppedEventBody stoppedDetails = await breakEventTask;
            Assert.Equal(DebugScriptPath, stoppedDetails.Source.Path);

            var stackTraceResponse =
                await this.SendRequest(
                    StackTraceRequest.Type,
                    new StackTraceRequestArguments());

            Assert.Equal(5, stackTraceResponse.StackFrames[0].Line);

            breakEventTask = this.WaitForEvent(StoppedEvent.Type);
            await this.SendRequest(ContinueRequest.Type, new object());
            stoppedDetails = await breakEventTask;
            Assert.Equal(DebugScriptPath, stoppedDetails.Source.Path);

            stackTraceResponse =
                await this.SendRequest(
                    StackTraceRequest.Type,
                    new StackTraceRequestArguments());

            Assert.Equal(7, stackTraceResponse.StackFrames[0].Line);

            // Abort script execution
            await this.SendRequest(DisconnectRequest.Type, new object());
        }

        [Fact]
        public async Task DebugAdapterReceivesOutputEvents()
        {
            OutputReader outputReader = new OutputReader(this.debugAdapterClient);

            await this.LaunchScript(DebugScriptPath);

            // Skip the first 2 lines which just report the script
            // that is being executed
            await outputReader.ReadLines(2);

            // Make sure we're getting output from the script
            Assert.Equal("Output 1", await outputReader.ReadLine());

            // Abort script execution
            await this.SendRequest(DisconnectRequest.Type, new object());
        }

        private async Task LaunchScript(string scriptPath)
        {
            await this.debugAdapterClient.LaunchScriptAsync(scriptPath);
        }
    }
}
