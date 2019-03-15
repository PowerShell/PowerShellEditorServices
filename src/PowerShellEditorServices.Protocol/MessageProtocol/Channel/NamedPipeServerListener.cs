//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class NamedPipeServerListener : ServerListenerBase<NamedPipeServerChannel>
    {
        // This int will be casted to a PipeOptions enum that only exists in .NET Core 2.1 and up which is why it's not available to us in .NET Standard.
        private const int CurrentUserOnly = 536870912;

        // In .NET Framework, NamedPipeServerStream has a constructor that takes in a PipeSecurity object. We will use reflection to call the constructor,
        // since .NET Framework doesn't have the `CurrentUserOnly` PipeOption.
        // doc: https://docs.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.-ctor?view=netframework-4.7.2#System_IO_Pipes_NamedPipeServerStream__ctor_System_String_System_IO_Pipes_PipeDirection_System_Int32_System_IO_Pipes_PipeTransmissionMode_System_IO_Pipes_PipeOptions_System_Int32_System_Int32_System_IO_Pipes_PipeSecurity_
        private static ConstructorInfo _netFrameworkPipeServerConstructor =
            typeof(NamedPipeServerStream).GetConstructor(new [] { typeof(string), typeof(PipeDirection), typeof(int), typeof(PipeTransmissionMode), typeof(PipeOptions), typeof(int), typeof(int), typeof(PipeSecurity) });

        private ILogger logger;
        private string inOutPipeName;
        private readonly string outPipeName;
        private NamedPipeServerStream inOutPipeServer;
        private NamedPipeServerStream outPipeServer;

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string inOutPipeName,
            ILogger logger)
            : base(messageProtocolType)
        {
            this.logger = logger;
            this.inOutPipeName = inOutPipeName;
        }

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string inPipeName,
            string outPipeName,
            ILogger logger)
            : base(messageProtocolType)
        {
            this.logger = logger;
            this.inOutPipeName = inPipeName;
            this.outPipeName = outPipeName;
        }

        public override void Start()
        {
            try
            {
                // If we're running in Windows PowerShell, we use the constructor via Reflection
                if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework"))
                {
                    PipeSecurity pipeSecurity = new PipeSecurity();

                    WindowsIdentity identity = WindowsIdentity.GetCurrent();
                    WindowsPrincipal principal = new WindowsPrincipal(identity);

                    if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        // Allow the Administrators group full access to the pipe.
                        pipeSecurity.AddAccessRule(new PipeAccessRule(
                            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)),
                            PipeAccessRights.FullControl, AccessControlType.Allow));
                    }
                    else
                    {
                        // Allow the current user read/write access to the pipe.
                        pipeSecurity.AddAccessRule(new PipeAccessRule(
                            WindowsIdentity.GetCurrent().User,
                            PipeAccessRights.ReadWrite, AccessControlType.Allow));
                    }

                    _netFrameworkPipeServerConstructor.Invoke(new object[]
                    {
                        inOutPipeName,
                        PipeDirection.InOut,
                        1, // maxNumberOfServerInstances
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        1024, // inBufferSize
                        1024, // outBufferSize
                        pipeSecurity
                    });

                    if (this.outPipeName != null)
                    {
                        _netFrameworkPipeServerConstructor.Invoke(new object[]
                        {
                            outPipeName,
                            PipeDirection.InOut,
                            1, // maxNumberOfServerInstances
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous,
                            1024, // inBufferSize
                            1024, // outBufferSize
                            pipeSecurity
                        });
                    }
                }
                else
                {
                    this.inOutPipeServer = new NamedPipeServerStream(
                        pipeName: inOutPipeName,
                        direction: PipeDirection.InOut,
                        maxNumberOfServerInstances: 1,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: PipeOptions.Asynchronous | (PipeOptions)CurrentUserOnly);
                    if (this.outPipeName != null)
                    {
                        this.outPipeServer = new NamedPipeServerStream(
                            pipeName: outPipeName,
                            direction: PipeDirection.Out,
                            maxNumberOfServerInstances: 1,
                            transmissionMode: PipeTransmissionMode.Byte,
                            options: (PipeOptions)CurrentUserOnly);
                    }
                }
                ListenForConnection();
            }
            catch (IOException e)
            {
                this.logger.Write(
                    LogLevel.Verbose,
                    "Named pipe server failed to start due to exception:\r\n\r\n" + e.Message);

                throw e;
            }
        }

        public override void Stop()
        {
            if (this.inOutPipeServer != null)
            {
                this.logger.Write(LogLevel.Verbose, "Named pipe server shutting down...");

                this.inOutPipeServer.Dispose();

                this.logger.Write(LogLevel.Verbose, "Named pipe server has been disposed.");
            }
            if (this.outPipeServer != null)
            {
                this.logger.Write(LogLevel.Verbose, $"Named out pipe server {outPipeServer} shutting down...");

                this.outPipeServer.Dispose();

                this.logger.Write(LogLevel.Verbose, $"Named out pipe server {outPipeServer} has been disposed.");
            }
        }

        private void ListenForConnection()
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var connectionTasks = new List<Task> {WaitForConnectionAsync(this.inOutPipeServer)};
                    if (this.outPipeServer != null)
                    {
                        connectionTasks.Add(WaitForConnectionAsync(this.outPipeServer));
                    }

                    await Task.WhenAll(connectionTasks);
                    this.OnClientConnect(new NamedPipeServerChannel(this.inOutPipeServer, this.outPipeServer, this.logger));
                }
                catch (Exception e)
                {
                    this.logger.WriteException(
                        "An unhandled exception occurred while listening for a named pipe client connection",
                        e);

                    throw;
                }
            });
        }

        private static async Task WaitForConnectionAsync(NamedPipeServerStream pipeServerStream)
        {
#if CoreCLR
            await pipeServerStream.WaitForConnectionAsync();
#else
            await Task.Factory.FromAsync(pipeServerStream.BeginWaitForConnection, pipeServerStream.EndWaitForConnection, null);
#endif
            await pipeServerStream.FlushAsync();
        }
    }
}
