//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    /// <summary>
    /// Throttles output written via OutputEvents by batching all output
    /// written within a short time window and writing it all out at once.
    /// </summary>
    internal class OutputDebouncer : AsyncDebouncer<OutputWrittenEventArgs>
    {
        #region Private Fields

        private IMessageSender messageSender;
        private bool currentOutputIsError = false;
        private string currentOutputString = null;

        #endregion

        #region Constants

        // Set a really short window for output flushes.  This
        // gives the appearance of fast output without the crushing
        // overhead of sending an OutputEvent for every single line
        // written.  At this point it seems that around 10-20 lines get
        // batched for each flush when Get-Process is called.
        public const int OutputFlushInterval = 200;

        #endregion

        #region Constructors

        public OutputDebouncer(IMessageSender messageSender)
            : base(OutputFlushInterval, false)
        {
            this.messageSender = messageSender;
        }

        #endregion

        #region Private Methods

        protected override async Task OnInvoke(OutputWrittenEventArgs output)
        {
            bool outputIsError = output.OutputType == OutputType.Error;

            if (this.currentOutputIsError != outputIsError)
            {
                if (this.currentOutputString != null)
                {
                    // Flush the output
                    await this.OnFlush();
                }

                this.currentOutputString = string.Empty;
                this.currentOutputIsError = outputIsError;
            }

            // Output string could be null if the last output was already flushed
            if (this.currentOutputString == null)
            {
                this.currentOutputString = string.Empty;
            }

            // Add to string (and include newline)
            this.currentOutputString +=
                output.OutputText +
                (output.IncludeNewLine ? 
                    System.Environment.NewLine :
                    string.Empty);
        }

        protected override async Task OnFlush()
        {
            // Only flush output if there is some to flush
            if (this.currentOutputString != null)
            {
                // Send an event for the current output
                await this.messageSender.SendEvent(
                    OutputEvent.Type,
                    new OutputEventBody
                    {
                        Output = this.currentOutputString,
                        Category = (this.currentOutputIsError) ? "stderr" : "stdout"
                    });

                // Clear the output string for the next batch
                this.currentOutputString = null;
            }
        }

        #endregion
    }
}

