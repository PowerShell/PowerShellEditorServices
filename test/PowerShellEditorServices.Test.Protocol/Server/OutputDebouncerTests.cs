//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Protocol.Server
{
    public class OutputDebouncerTests
    {
        [Fact]
        public async Task OutputDebouncerAggregatesOutputEvents()
        {
            TestMessageSender messageSender = new TestMessageSender();
            OutputDebouncer debouncer = new OutputDebouncer(messageSender);

            await SendOutput(debouncer, "This ");
            await SendOutput(debouncer, "is a ");
            await SendOutput(debouncer, "test", true);
            await SendOutput(debouncer, "Another line");

            // Make sure no output events have been written yet
            Assert.Empty(messageSender.OutputEvents);

            // Wait for the output to be flushed
            await Task.Delay(OutputDebouncer.OutputFlushInterval + 100);

            // Write some more output after the first flush
            await SendOutput(debouncer, "Another test line", true);
            await SendOutput(debouncer, "for great justice");

            // Assert that there's only one event with the expected string
            Assert.Single(messageSender.OutputEvents);
            Assert.Equal(
                TestUtilities.NormalizeNewlines("This is a test\nAnother line"),
                messageSender.OutputEvents[0].Output);

            // Wait for the next output to be flushed
            await Task.Delay(OutputDebouncer.OutputFlushInterval + 100);

            // Assert that there's only one event with the expected string
            Assert.Equal(2, messageSender.OutputEvents.Count);
            Assert.Equal(
                TestUtilities.NormalizeNewlines("Another test line\nfor great justice"),
                messageSender.OutputEvents[1].Output);
        }

        [Fact]
        public async Task OutputDebouncerDoesNotDuplicateOutput()
        {
            TestMessageSender messageSender = new TestMessageSender();
            OutputDebouncer debouncer = new OutputDebouncer(messageSender);

            // Send many messages in quick succession to ensure that
            // output is not being duplicated in subsequent events.
            for (int i = 1; i <= 50; i++)
            {
                await SendOutput(debouncer, "Output " + i, true);

                if (i == 25)
                {
                    // Artificially insert a delay to force another event
                    await Task.Delay(OutputDebouncer.OutputFlushInterval + 100);
                }
            }

            // Wait for the final event to be written
            await Task.Delay(OutputDebouncer.OutputFlushInterval + 100);

            // Ensure that the two events start with the correct lines
            Assert.Equal(2, messageSender.OutputEvents.Count);
            Assert.Equal("Output 1", messageSender.OutputEvents[0].Output.Split('\n')[0].Trim('\r'));
            Assert.Equal("Output 26", messageSender.OutputEvents[1].Output.Split('\n')[0].Trim('\r'));
        }

        private static Task SendOutput(
            OutputDebouncer debouncer,
            string outputText,
            bool includeNewLine = false)
        {
            return debouncer.InvokeAsync(
                new OutputWrittenEventArgs(
                    outputText,
                    includeNewLine,
                    OutputType.Normal,
                    ConsoleColor.White,
                    ConsoleColor.Black));
        }
    }

    internal class TestMessageSender : IMessageSender
    {
        public List<OutputEventBody> OutputEvents { get; } = new List<OutputEventBody>();

        public Task SendEventAsync<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            TParams eventParams)
        {
            OutputEventBody outputEvent = eventParams as OutputEventBody;

            if (outputEvent != null)
            {
                this.OutputEvents.Add(outputEvent);
            }

            return Task.FromResult(true);
        }

        public Task<TResult> SendRequestAsync<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            TParams requestParams, bool waitForResponse)
        {
            // Legitimately not implemented for these tests.
            throw new NotImplementedException();
        }

        public Task<TResult> SendRequestAsync<TResult, TError, TRegistrationOptions>(RequestType0<TResult, TError, TRegistrationOptions> requestType0)
        {
            // Legitimately not implemented for these tests.
            throw new NotImplementedException();
        }
    }
}

