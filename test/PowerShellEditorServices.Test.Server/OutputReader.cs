//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    internal class OutputReader
    {
        private AsyncQueue<OutputEventBody> outputQueue = new AsyncQueue<OutputEventBody>();

        private string currentOutputCategory;
        private Queue<Tuple<string, bool>> bufferedOutput = new Queue<Tuple<string, bool>>();

        public OutputReader(IMessageHandlers messageHandlers)
        {
            messageHandlers.SetEventHandler(
                OutputEvent.Type,
                this.OnOutputEvent);
        }

        public async Task<string> ReadLine(string expectedOutputCategory = "stdout", bool waitForNewLine = true)
        {
            try
            {
                bool lineHasNewLine = false;
                string[] outputLines = null;
                string nextOutputString = string.Empty;

                // Wait no longer than 7 seconds for output to come back
                CancellationToken cancellationToken =
                    Debugger.IsAttached
                        ? CancellationToken.None
                        : new CancellationTokenSource(7000).Token;

                // Any lines in the buffer?
                if (this.bufferedOutput.Count > 0)
                {
                    Assert.Equal(expectedOutputCategory, this.currentOutputCategory);

                    // Return the first buffered line
                    var lineTuple = this.bufferedOutput.Dequeue();
                    nextOutputString = lineTuple.Item1;
                    lineHasNewLine = lineTuple.Item2;
                }

                // Loop until we get a full line of output
                while (!lineHasNewLine)
                {
                    // Execution reaches this point if a buffered line wasn't available
                    Task<OutputEventBody> outputTask =
                        this.outputQueue.DequeueAsync(
                            cancellationToken);
                    OutputEventBody nextOutputEvent = await outputTask;

                    // Verify that the output is of the expected type
                    Assert.Equal(expectedOutputCategory, nextOutputEvent.Category);
                    this.currentOutputCategory = nextOutputEvent.Category;

                    // Split up the output into multiple lines
                    outputLines =
                        nextOutputEvent.Output.Split(
                            new string[] { "\n", "\r\n" },
                            StringSplitOptions.None);

                    // Add the first bit of output to the existing string
                    nextOutputString += outputLines[0];

                    // Have we found a newline now?
                    lineHasNewLine =
                        outputLines.Length > 1 ||
                        nextOutputEvent.Output.EndsWith("\n");

                    // Buffer any remaining lines for future reads
                    if (outputLines.Length > 1)
                    {
                        for (int i = 1; i < outputLines.Length; i++)
                        {
                            this.bufferedOutput.Enqueue(
                                new Tuple<string, bool>(
                                    outputLines[i],

                                    // The line has a newline if it's not the last segment or
                                    // if the last segment is not an empty string and the
                                    // complete output string ends with a newline
                                    i < outputLines.Length - 1 ||
                                    (outputLines[outputLines.Length - 1].Length > 0 &&
                                     nextOutputEvent.Output.EndsWith("\n"))));
                        }
                    }

                    // At this point, the state of lineHasNewLine will determine
                    // whether the loop continues to wait for another output
                    // event that completes the current line.
                    if (!waitForNewLine)
                    {
                        break;
                    }
                }

                return nextOutputString;
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("Timed out waiting for an input line.");
            }
        }

        public async Task<string[]> ReadLines(int lineCount, string expectedOutputCategory = "stdout")
        {
            List<string> outputLines = new List<string>();

            for (int i = 0; i < lineCount; i++)
            {
                outputLines.Add(
                    await this.ReadLine(
                        expectedOutputCategory));
            }

            return outputLines.ToArray();
        }

        private async Task OnOutputEvent(OutputEventBody outputEvent, EventContext context)
        {
            await this.outputQueue.EnqueueAsync(outputEvent);
        }
    }
}

