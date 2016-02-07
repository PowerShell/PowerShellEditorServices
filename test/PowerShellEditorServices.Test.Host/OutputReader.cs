//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    internal class OutputReader
    {
        private AsyncQueue<OutputEventBody> outputQueue = new AsyncQueue<OutputEventBody>();

        private string currentOutputCategory;
        private Queue<string> bufferedOutput = new Queue<string>();

        public OutputReader(ProtocolEndpoint protocolClient)
        {
            protocolClient.SetEventHandler(
                OutputEvent.Type,
                this.OnOutputEvent);
        }

        public async Task<string> ReadLine(string expectedOutputCategory = "stdout")
        {
            if (this.bufferedOutput.Count > 0)
            {
                Assert.Equal(expectedOutputCategory, this.currentOutputCategory);

                return this.bufferedOutput.Dequeue();
            }

            // Execution reaches this point if a buffered line wasn't available
            OutputEventBody nextOutputEvent = await this.outputQueue.DequeueAsync();

            Assert.Equal(expectedOutputCategory, nextOutputEvent.Category);
            this.currentOutputCategory = nextOutputEvent.Category;

            string[] outputLines = 
                nextOutputEvent.Output.Split(
                    new string[] { "\n", "\r\n" },
                    StringSplitOptions.None);

            // Buffer remaining lines
            if (outputLines.Length > 1)
            {
                for (int i = 1; i < outputLines.Length; i++)
                {
                    this.bufferedOutput.Enqueue(outputLines[i]);
                }
            }

            return outputLines[0];
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

