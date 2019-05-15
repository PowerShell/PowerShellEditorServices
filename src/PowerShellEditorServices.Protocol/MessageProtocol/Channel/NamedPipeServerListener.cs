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
        private const int CurrentUserOnly = 0x20000000;

        // In .NET Framework, NamedPipeServerStream has a constructor that takes in a PipeSecurity object. We will use reflection to call the constructor,
        // since .NET Framework doesn't have the `CurrentUserOnly` PipeOption.
        // doc: https://docs.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.-ctor?view=netframework-4.7.2#System_IO_Pipes_NamedPipeServerStream__ctor_System_String_System_IO_Pipes_PipeDirection_System_Int32_System_IO_Pipes_PipeTransmissionMode_System_IO_Pipes_PipeOptions_System_Int32_System_Int32_System_IO_Pipes_PipeSecurity_
        private static readonly ConstructorInfo s_netFrameworkPipeServerConstructor =
            typeof(NamedPipeServerStream).GetConstructor(new [] { typeof(string), typeof(PipeDirection), typeof(int), typeof(PipeTransmissionMode), typeof(PipeOptions), typeof(int), typeof(int), typeof(PipeSecurity) });

        private readonly ILogger _logger;
        private readonly string _inOutPipeName;
        private readonly string _outPipeName;

        private NamedPipeServerStream _inOutPipeServer;
        private NamedPipeServerStream _outPipeServer;

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string inOutPipeName,
            ILogger logger)
            : base(messageProtocolType)
        {
            _logger = logger;
            _inOutPipeName = inOutPipeName;
        }

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string inPipeName,
            string outPipeName,
            ILogger logger)
            : base(messageProtocolType)
        {
            _logger = logger;
            _inOutPipeName = inPipeName;
            _outPipeName = outPipeName;
        }

        public override void Start()
        {
            try
            {
                _inOutPipeServer = ConnectNamedPipe(_inOutPipeName, _outPipeName, out _outPipeServer);
                ListenForConnection();
            }
            catch (IOException e)
            {
                _logger.Write(
                    LogLevel.Verbose,
                    "Named pipe server failed to start due to exception:\r\n\r\n" + e.Message);

                throw e;
            }
        }

        public override void Stop()
        {
            if (_inOutPipeServer != null)
            {
                _logger.Write(LogLevel.Verbose, "Named pipe server shutting down...");

                _inOutPipeServer.Dispose();

                _logger.Write(LogLevel.Verbose, "Named pipe server has been disposed.");
            }

            if (_outPipeServer != null)
            {
                _logger.Write(LogLevel.Verbose, $"Named out pipe server {_outPipeServer} shutting down...");

                _outPipeServer.Dispose();

                _logger.Write(LogLevel.Verbose, $"Named out pipe server {_outPipeServer} has been disposed.");
            }
        }

        private static NamedPipeServerStream ConnectNamedPipe(
            string inOutPipeName,
            string outPipeName,
            out NamedPipeServerStream outPipe)
        {
            // .NET Core implementation is simplest so try that first
            if (Utils.IsNetCore)
            {
                outPipe = outPipeName == null
                    ? null
                    : new NamedPipeServerStream(
                        pipeName: outPipeName,
                        direction: PipeDirection.Out,
                        maxNumberOfServerInstances: 1,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: (PipeOptions)CurrentUserOnly);

                return new NamedPipeServerStream(
                    pipeName: inOutPipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous | (PipeOptions)CurrentUserOnly);
            }

            // Now deal with Windows PowerShell
            // We need to use reflection to get a nice constructor

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

            outPipe = outPipeName == null
                ? null
                : (NamedPipeServerStream)s_netFrameworkPipeServerConstructor.Invoke(
                    new object[] {
                        outPipeName,
                        PipeDirection.InOut,
                        1, // maxNumberOfServerInstances
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        1024, // inBufferSize
                        1024, // outBufferSize
                        pipeSecurity
                    });

            return (NamedPipeServerStream)s_netFrameworkPipeServerConstructor.Invoke(
                new object[] {
                    inOutPipeName,
                    PipeDirection.InOut,
                    1, // maxNumberOfServerInstances
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    1024, // inBufferSize
                    1024, // outBufferSize
                    pipeSecurity
                });
        }

        private void ListenForConnection()
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var connectionTasks = new List<Task> {WaitForConnectionAsync(_inOutPipeServer)};
                    if (_outPipeServer != null)
                    {
                        connectionTasks.Add(WaitForConnectionAsync(_outPipeServer));
                    }

                    await Task.WhenAll(connectionTasks);
                    OnClientConnect(new NamedPipeServerChannel(_inOutPipeServer, _outPipeServer, _logger));
                }
                catch (Exception e)
                {
                    _logger.WriteException(
                        "An unhandled exception occurred while listening for a named pipe client connection",
                        e);

                    throw;
                }
            });
        }

        private static async Task WaitForConnectionAsync(NamedPipeServerStream pipeServerStream)
        {
            await pipeServerStream.WaitForConnectionAsync();
            await pipeServerStream.FlushAsync();
        }
    }
}
