//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Management.Automation;
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
        private bool runDebugAdapter;
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

        #region Constructors

        public MessageLoop(bool runDebugAdapter)
        {
            this.runDebugAdapter = runDebugAdapter;
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

            // Open the standard input/output streams
            this.inputStream = System.Console.OpenStandardInput();
            this.outputStream = System.Console.OpenStandardOutput();

            IMessageSerializer messageSerializer = null;
            IMessageProcessor messageProcessor = null;

            // Use a different serializer and message processor based
            // on whether this instance should host a language server
            // debug adapter.
            if (this.runDebugAdapter)
            {
                DebugAdapter debugAdapter = new DebugAdapter();
                debugAdapter.Initialize();

                messageProcessor = debugAdapter;
                messageSerializer = new V8MessageSerializer();
            }
            else
            {
                // Set up the LanguageServer
                LanguageServer languageServer = new LanguageServer();
                languageServer.Initialize();

                messageProcessor = languageServer;
                messageSerializer = new JsonRpcMessageSerializer();
            }

            // Set up the reader and writer
            this.messageReader = 
                new MessageReader(
                    this.inputStream,
                    messageSerializer);

            this.messageWriter = 
                new MessageWriter(
                    this.outputStream,
                    messageSerializer);

            // Set up the console host which will send events
            // through the MessageWriter
            this.consoleHost = new StdioConsoleHost(messageWriter);

            // Set up the PowerShell session
            this.editorSession = new EditorSession();
            this.editorSession.StartSession(this.consoleHost);
            this.editorSession.PowerShellSession.OutputWritten += PowerShellSession_OutputWritten;

            if (this.runDebugAdapter)
            {
                // Attach to debugger events from the PowerShell session
                this.editorSession.DebugService.DebuggerStopped += DebugService_DebuggerStopped;
            }

            // Run the message loop
            bool isRunning = true;
            while (isRunning)
            {
                Message newMessage = null;

                try
                {
                    // Read a message from stdin
                    newMessage = await this.messageReader.ReadMessage();
                }
                catch (MessageParseException e)
                {
                    // TODO: Write an error response

                    Logger.Write(
                        LogLevel.Error,
                        "Could not parse a message that was received:\r\n\r\n" +
                        e.ToString());

                    // Continue the loop
                    continue;
                }

                // Process the message
                await messageProcessor.ProcessMessage(
                    newMessage,
                    this.editorSession,
                    this.messageWriter);
            }
        }

        void DebugService_DebuggerStopped(object sender, DebuggerStopEventArgs e)
        {
            // Push the write operation to the correct thread
            this.messageLoopSyncContext.Post(
                async (obj) =>
                {
                    await this.messageWriter.WriteEvent(
                        StoppedEvent.Type,
                        new StoppedEventBody
                        {
                            Source = new Source
                            {
                                Path = e.InvocationInfo.ScriptName,
                            },
                            Line = e.InvocationInfo.ScriptLineNumber,
                            Column = e.InvocationInfo.OffsetInLine,
                            ThreadId = 1, // TODO: Change this based on context
                            Reason = "breakpoint" // TODO: Change this based on context
                        });
                }, null);
        }

        async void PowerShellSession_OutputWritten(object sender, OutputWrittenEventArgs e)
        {
            await this.messageWriter.WriteEvent(
                OutputEvent.Type,
                new OutputEventBody
                {
                    Output = e.OutputText + (e.IncludeNewLine ? "\r\n" : string.Empty),
                    Category = (e.OutputType == OutputType.Error) ? "stderr" : "stdout"
                });
        }

        #endregion
    }
}
