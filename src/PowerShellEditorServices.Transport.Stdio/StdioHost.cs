//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using Nito.AsyncEx;
using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio
{
    public class StdioHost : IHost
    {
        #region Private Fields

        private IConsoleHost consoleHost;
        private EditorSession editorSession;
        private SynchronizationContext syncContext;
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

        void IHost.Start()
        {
            // Start a new EditorSession
            // TODO: Allow multiple sessions?
            this.editorSession = new EditorSession();

            // Start the main message loop
            AsyncContext.Run((Func<Task>)this.StartMessageLoop);
        }

        #endregion

        #region Private Methods

        private async Task StartMessageLoop()
        {
            // Hold on to the current synchronization context
            this.syncContext = SynchronizationContext.Current;

            // Start the message listener on another thread
            this.messageLoopThread = new AsyncContextThread(true);
            await this.messageLoopThread.Factory.Run(() => this.ListenForMessages());
        }

        //private async Task ListenForMessages()
        //{
        //    // Ensure that the console is using UTF-8 encoding
        //    System.Console.InputEncoding = Encoding.UTF8;
        //    System.Console.OutputEncoding = Encoding.UTF8;

        //    // Set up the reader and writer
        //    MessageReader messageReader = 
        //        new MessageReader(
        //            System.Console.In, 
        //            MessageFormat.WithoutContentLength);

        //    MessageWriter messageWriter = 
        //        new MessageWriter(
        //            System.Console.Out, 
        //            MessageFormat.WithContentLength);

        //    this.ConsoleHost = new StdioConsoleHost(messageWriter);

        //    // Set up the PowerShell session
        //    // TODO: Do this elsewhere
        //    EditorSession editorSession = new EditorSession();
        //    editorSession.StartSession(this.ConsoleHost);

        //    // Send a "started" event
        //    messageWriter.WriteMessage(
        //        new Event<object>
        //        { 
        //            EventType = "started" 
        //        });

        //    // Run the message loop
        //    bool isRunning = true;
        //    while(isRunning)
        //    {
        //        // Read a message
        //        Message newMessage = await messageReader.ReadMessage();

        //        // Is the message a request?
        //        IMessageProcessor messageProcessor = newMessage as IMessageProcessor;
        //        if (messageProcessor != null)
        //        {
        //            // Process the request on the host thread
        //            messageProcessor.ProcessMessage(
        //                editorSession,
        //                messageWriter);
        //        }
        //        else
        //        {
        //            if (newMessage != null)
        //            {
        //                // Return an error response to keep the client moving
        //                messageWriter.WriteMessage(
        //                    new Response<object>
        //                    {
        //                        Command = request != null ? request.Command : string.Empty,
        //                        RequestSeq = newMessage.Seq,
        //                        Success = false,
        //                    });
        //            }
        //        }
        //    }
        //}
        async Task ListenForMessages()
        {
            // Ensure that the console is using UTF-8 encoding
            System.Console.InputEncoding = Encoding.UTF8;
            System.Console.OutputEncoding = Encoding.UTF8;

            // Find all message types in this assembly
            MessageTypeResolver messageTypeResolver = new MessageTypeResolver();
            messageTypeResolver.ScanForMessageTypes(Assembly.GetExecutingAssembly());

            // Set up the reader and writer
            MessageReader messageReader = 
                new MessageReader(
                    System.Console.In, 
                    MessageFormat.WithContentLength,
                    messageTypeResolver);

            MessageWriter messageWriter = 
                new MessageWriter(
                    System.Console.Out, 
                    MessageFormat.WithContentLength,
                    messageTypeResolver);

            // Set up the console host which will send events
            // through the MessageWriter
            this.consoleHost = new StdioConsoleHost(messageWriter);

            // Set up the PowerShell session
            // TODO: Do this elsewhere
            EditorSession editorSession = new EditorSession();
            editorSession.StartSession(this.consoleHost);

            // Send a "started" event
            messageWriter.WriteMessage(
                new StartedEvent());

            // Run the message loop
            bool isRunning = true;
            while (isRunning)
            {
                MessageBase newMessage = null;

                try
                {
                    // Read a message from stdin
                    newMessage = await messageReader.ReadMessage();
                }
                catch (MessageParseException e)
                {
                    // Write an error response
                    messageWriter.WriteMessage(
                        MessageErrorResponse.CreateParseErrorResponse(e));

                    // Continue the loop
                    continue;
                }

                // Is the message a request?
                IMessageProcessor messageProcessor = newMessage as IMessageProcessor;
                if (messageProcessor != null)
                {
                    // Process the message.  The processor will take care
                    // of writing responses throguh the messageWriter.
                    messageProcessor.ProcessMessage(
                        editorSession,
                        messageWriter);
                }
                else
                {
                    if (newMessage != null)
                    {
                        // Return an error response to keep the client moving
                        messageWriter.WriteMessage(
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

        #endregion
    }
}
