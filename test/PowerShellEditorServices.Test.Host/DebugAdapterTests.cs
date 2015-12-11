//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.Client;
using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class DebugAdapterTests : IAsyncLifetime
    {
        private DebugAdapterClient debugAdapterClient;
        private string DebugScriptPath = 
            Path.GetFullPath(@"..\..\..\PowerShellEditorServices.Test.Shared\Debugging\DebugTest.ps1");

        public Task InitializeAsync()
        {
            string testLogPath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logs",
                    this.GetType().Name,
                    Guid.NewGuid().ToString().Substring(0, 8) + ".log");

            Console.WriteLine("        Output log at path: {0}", testLogPath);

            this.debugAdapterClient =
                new DebugAdapterClient(
                    new StdioClientChannel(
                        "Microsoft.PowerShell.EditorServices.Host.exe",
                        "/debugAdapter",
                        "/logPath:\"" + testLogPath + "\""));

            return this.debugAdapterClient.Start();
        }

        public Task DisposeAsync()
        {
            return this.debugAdapterClient.Stop();
        }

        [Fact]
        public async Task DebugAdapterStopsOnBreakpoints()
        {
            await this.SendRequest(
                SetBreakpointsRequest.Type,
                new SetBreakpointsRequestArguments
                {
                    Source = new Source
                    {
                        Path = DebugScriptPath
                    },
                    Lines = new int[] { 5, 9 }
                });

            Task<StoppedEventBody> breakEventTask = this.WaitForEvent(StoppedEvent.Type);
            await this.LaunchScript(DebugScriptPath);

            // Wait for a couple breakpoints
            StoppedEventBody stoppedDetails = await breakEventTask;
            Assert.Equal(DebugScriptPath, stoppedDetails.Source.Path);
            Assert.Equal(5, stoppedDetails.Line);

            breakEventTask = this.WaitForEvent(StoppedEvent.Type);
            await this.SendRequest(ContinueRequest.Type, new object());
            stoppedDetails = await breakEventTask;
            Assert.Equal(DebugScriptPath, stoppedDetails.Source.Path);
            Assert.Equal(9, stoppedDetails.Line);

            // Abort script execution
            Task terminatedEvent = this.WaitForEvent(TerminatedEvent.Type);
            await this.SendRequest(DisconnectRequest.Type, new object());
            await terminatedEvent;
        }

        private Task LaunchScript(string scriptPath)
        {
            return this.debugAdapterClient.LaunchScript(scriptPath);
        }

        private Task<TResult> SendRequest<TParams, TResult>(
            RequestType<TParams, TResult> requestType, 
            TParams requestParams)
        {
            return 
                this.debugAdapterClient.SendRequest(
                    requestType, 
                    requestParams);
        }

        private Task SendEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            return 
                this.debugAdapterClient.SendEvent(
                    eventType,
                    eventParams);
        }

        private Task<TParams> WaitForEvent<TParams>(EventType<TParams> eventType)
        {
            TaskCompletionSource<TParams> eventTask = new TaskCompletionSource<TParams>();

            this.debugAdapterClient.SetEventHandler(
                eventType,
                (p, ctx) =>
                {
                    eventTask.SetResult(p);
                    return Task.FromResult(true);
                },
                true);  // Override any existing handler

            return eventTask.Task;
        }
    }
}

