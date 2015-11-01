//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Host
{
    public class MessageLoop : IHost
    {
        #region Private Fields

        private Stream inputStream;
        private Stream outputStream;
        private IConsoleHost consoleHost;
        private EditorSession editorSession;
        private MessageReader messageReader;
        private MessageWriter messageWriter;
        private SynchronizationContext applicationSyncContext;
        private SynchronizationContext messageLoopSyncContext;
        private AsyncContextThread messageLoopThread;

        #endregion

        #region IHost Implementation

        string IHost.Name
        {
            get { throw new NotImplementedException(); }
        }

        Version IHost.Version
        {
            get { throw new NotImplementedException(); }
        }

        public void Start()
        {
            // Start the main message loop
            AsyncContext.Run((Func<Task>)this.StartMessageLoop);
        }

        #endregion

        #region Private Methods

        private async Task StartMessageLoop()
        {
            // Hold on to the current synchronization context
            this.applicationSyncContext = SynchronizationContext.Current;

            // Start the message listener on another thread
            this.messageLoopThread = new AsyncContextThread(true);
            await this.messageLoopThread.Factory.Run(() => this.ListenForMessages());
        }

        async Task ListenForMessages()
        {
            this.messageLoopSyncContext = SynchronizationContext.Current;

            // Ensure that the console is using UTF-8 encoding
            System.Console.InputEncoding = Encoding.UTF8;
            System.Console.OutputEncoding = Encoding.UTF8;

            // Find all message types in this assembly
            MessageTypeResolver messageTypeResolver = new MessageTypeResolver();
            messageTypeResolver.ScanForMessageTypes(typeof(StartedEvent).Assembly);

            // Open the standard input/output streams
            this.inputStream = System.Console.OpenStandardInput();
            this.outputStream = System.Console.OpenStandardOutput();

            // Set up the reader and writer
            this.messageReader = 
                new MessageReader(
                    this.inputStream,
                    messageTypeResolver);

            this.messageWriter = 
                new MessageWriter(
                    this.outputStream,
                    messageTypeResolver);

            // Set up the console host which will send events
            // through the MessageWriter
            this.consoleHost = new StdioConsoleHost(messageWriter);

            // Set up the PowerShell session
            this.editorSession = new EditorSession();
            this.editorSession.StartSession(this.consoleHost);

            // Attach to events from the PowerShell session
            this.editorSession.PowerShellSession.OutputWritten += PowerShellSession_OutputWritten;
            this.editorSession.PowerShellSession.BreakpointUpdated += PowerShellSession_BreakpointUpdated;
            this.editorSession.DebugService.DebuggerStopped += DebugService_DebuggerStopped;

            // Send a "started" event
            await this.messageWriter.WriteMessage(
                new StartedEvent());

            // Run the message loop
            bool isRunning = true;
            while (isRunning)
            {
                MessageBase newMessage = null;

                try
                {
                    // Read a message from stdin
                    newMessage = await this.messageReader.ReadMessage();
                }
                catch (MessageParseException e)
                {
                    // Write an error response
                    this.messageWriter.WriteMessage(
                        MessageErrorResponse.CreateParseErrorResponse(e)).Wait();

                    // Continue the loop
                    continue;
                }

                // Is the message a request?
                IMessageProcessor messageProcessor = newMessage as IMessageProcessor;
                if (messageProcessor != null)
                {
                    // Process the message.  The processor will take care
                    // of writing responses throguh the messageWriter.
                    await messageProcessor.ProcessMessage(
                        this.editorSession,
                        this.messageWriter);
                }
                else
                {
                    if (newMessage != null)
                    {
                        // Return an error response to keep the client moving
                        await this.messageWriter.WriteMessage(
                            MessageErrorResponse.CreateUnhandledMessageResponse(
                                newMessage));
                    }
                    else
                    {
                        // TODO: Some other problem must have occurred,
                        // design a message response for this case.
                    }
                }
            }
        }

        async void DebugService_DebuggerStopped(object sender, DebuggerStopEventArgs e)
        {
            // Push the write operation to the correct thread
            this.messageLoopSyncContext.Post(
                async (obj) =>
                {
                    await this.messageWriter.WriteMessage(
                        new StoppedEvent
                        {
                            Body = new StoppedEventBody
                            {
                                Source = new Source
                                {
                                    Path = e.InvocationInfo.ScriptName,
                                },
                                Line = e.InvocationInfo.ScriptLineNumber,
                                Column = e.InvocationInfo.OffsetInLine,
                                ThreadId = 1, // TODO: Change this based on context
                                Reason = "breakpoint" // TODO: Change this based on context
                            }
                        });
                }, null);
        }

        void PowerShellSession_BreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
        }

        void PowerShellSession_OutputWritten(object sender, OutputWrittenEventArgs e)
        {
            // TODO: change this to use the OutputEvent!

            //await this.messageWriter.WriteMessage(
            //    new ReplWriteOutputEvent
            //    {
            //        Body = new ReplWriteOutputEventBody
            //        {
            //            LineContents = e.OutputText,
            //            LineType = e.OutputType,
            //            IncludeNewLine = e.IncludeNewLine,
            //            ForegroundColor = e.ForegroundColor,
            //            BackgroundColor = e.BackgroundColor
            //        }
            //    });
        }

        #endregion
    }
}
