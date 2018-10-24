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
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class NamedPipeServerListener : ServerListenerBase<NamedPipeServerChannel>
    {
        private ILogger logger;
        private string pipeName;
        private readonly string writePipeName;
        private NamedPipeServerStream pipeServer;
        private NamedPipeServerStream writePipeServer;

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string pipeName,
            ILogger logger)
            : base(messageProtocolType)
        {
            this.logger = logger;
            this.pipeName = pipeName;
            this.writePipeName = null;
        }

        public NamedPipeServerListener(
            MessageProtocolType messageProtocolType,
            string readPipeName,
            string writePipeName,
            ILogger logger)
            : base(messageProtocolType)
        {
            this.logger = logger;
            this.pipeName = readPipeName;
            this.writePipeName = writePipeName;
        }

        public override void Start()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

                    // Unfortunately, .NET Core does not support passing in a PipeSecurity object into the constructor for
                    // NamedPipeServerStream so we are creating native Named Pipes and securing them using native APIs. The
                    // issue on .NET Core regarding Named Pipe security is here: https://github.com/dotnet/corefx/issues/30170
                    // 99% of this code was borrowed from PowerShell here:
                    // https://github.com/PowerShell/PowerShell/blob/master/src/System.Management.Automation/engine/remoting/common/RemoteSessionNamedPipe.cs#L124-L256
                    this.pipeServer = NamedPipeNative.CreateNamedPipe(pipeName, pipeSecurity);
                    if (this.writePipeName != null)
                    {
                        this.writePipeServer = NamedPipeNative.CreateNamedPipe(writePipeName, pipeSecurity);
                    }
                }
                else
                {
                    // This handles the Unix case since PipeSecurity is not supported on Unix.
                    // Instead, we use chmod in Start-EditorServices.ps1
                    this.pipeServer = new NamedPipeServerStream(
                        pipeName: pipeName,
                        direction: PipeDirection.InOut,
                        maxNumberOfServerInstances: 1,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: PipeOptions.Asynchronous);
                    if (this.writePipeName != null)
                    {
                        this.writePipeServer = new NamedPipeServerStream(
                            pipeName: writePipeName,
                            direction: PipeDirection.InOut,
                            maxNumberOfServerInstances: 1,
                            transmissionMode: PipeTransmissionMode.Byte,
                            options: PipeOptions.None);
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
            if (this.pipeServer != null)
            {
                this.logger.Write(LogLevel.Verbose, "Named pipe server shutting down...");

                this.pipeServer.Dispose();

                this.logger.Write(LogLevel.Verbose, "Named pipe server has been disposed.");
            }
            if (this.writePipeServer != null)
            {
                this.logger.Write(LogLevel.Verbose, $"Named write pipe server {writePipeServer} shutting down...");

                this.writePipeServer.Dispose();

                this.logger.Write(LogLevel.Verbose, $"Named write pipe server {writePipeServer} has been disposed.");
            }
        }

        private void ListenForConnection()
        {
            List<Task> connectionTasks = new List<Task> {WaitForConnectionAsync(this.pipeServer)};
            if (this.writePipeServer != null)
            {
                connectionTasks.Add(WaitForConnectionAsync(this.writePipeServer));
            }

            Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(connectionTasks);
                    this.OnClientConnect(new NamedPipeServerChannel(this.pipeServer, this.writePipeServer, this.logger));
                }
                catch (Exception e)
                {
                    this.logger.WriteException(
                        "An unhandled exception occurred while listening for a named pipe client connection",
                        e);

                    throw e;
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

    /// <summary>
    /// Native API for Named Pipes
    /// This code was borrowed from PowerShell here:
    /// https://github.com/PowerShell/PowerShell/blob/master/src/System.Management.Automation/engine/remoting/common/RemoteSessionNamedPipe.cs#L124-L256
    /// </summary>
    internal static class NamedPipeNative
    {
        #region Pipe constants

        // Pipe open mode
        internal const uint PIPE_ACCESS_DUPLEX = 0x00000003;

        // Pipe modes
        internal const uint PIPE_TYPE_BYTE = 0x00000000;
        internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        internal const uint FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000;
        internal const uint PIPE_READMODE_BYTE = 0x00000000;

        #endregion

        #region Data structures

        [StructLayout(LayoutKind.Sequential)]
        internal class SECURITY_ATTRIBUTES
        {
            /// <summary>
            /// The size, in bytes, of this structure. Set this value to the size of the SECURITY_ATTRIBUTES structure.
            /// </summary>
            public int NLength;

            /// <summary>
            /// A pointer to a security descriptor for the object that controls the sharing of it.
            /// </summary>
            public IntPtr LPSecurityDescriptor = IntPtr.Zero;

            /// <summary>
            /// A Boolean value that specifies whether the returned handle is inherited when a new process is created.
            /// </summary>
            public bool InheritHandle;

            /// <summary>
            /// Initializes a new instance of the SECURITY_ATTRIBUTES class
            /// </summary>
            public SECURITY_ATTRIBUTES()
            {
                this.NLength = 12;
            }
        }

        #endregion

        #region Pipe methods

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafePipeHandle CreateNamedPipe(
           string lpName,
           uint dwOpenMode,
           uint dwPipeMode,
           uint nMaxInstances,
           uint nOutBufferSize,
           uint nInBufferSize,
           uint nDefaultTimeOut,
           SECURITY_ATTRIBUTES securityAttributes);

        internal static SECURITY_ATTRIBUTES GetSecurityAttributes(GCHandle securityDescriptorPinnedHandle, bool inheritHandle = false)
        {
            SECURITY_ATTRIBUTES securityAttributes = new NamedPipeNative.SECURITY_ATTRIBUTES();
            securityAttributes.InheritHandle = inheritHandle;
            securityAttributes.NLength = (int)Marshal.SizeOf(securityAttributes);
            securityAttributes.LPSecurityDescriptor = securityDescriptorPinnedHandle.AddrOfPinnedObject();
            return securityAttributes;
        }

        /// <summary>
        /// Helper method to create a PowerShell transport named pipe via native API, along
        /// with a returned .Net NamedPipeServerStream object wrapping the named pipe.
        /// </summary>
        /// <param name="pipeName">Named pipe core name.</param>
        /// <param name="securityDesc"></param>
        /// <returns>NamedPipeServerStream</returns>
        internal static NamedPipeServerStream CreateNamedPipe(
            string pipeName,
            PipeSecurity pipeSecurity)

        {
            string fullPipeName = @"\\.\pipe\" + pipeName;
            CommonSecurityDescriptor securityDesc = new CommonSecurityDescriptor(false, false, pipeSecurity.GetSecurityDescriptorBinaryForm(), 0);

            // Create optional security attributes based on provided PipeSecurity.
            NamedPipeNative.SECURITY_ATTRIBUTES securityAttributes = null;
            GCHandle? securityDescHandle = null;
            if (securityDesc != null)
            {
                byte[] securityDescBuffer = new byte[securityDesc.BinaryLength];
                securityDesc.GetBinaryForm(securityDescBuffer, 0);

                securityDescHandle = GCHandle.Alloc(securityDescBuffer, GCHandleType.Pinned);
                securityAttributes = NamedPipeNative.GetSecurityAttributes(securityDescHandle.Value);
            }

            // Create named pipe.
            SafePipeHandle pipeHandle = NamedPipeNative.CreateNamedPipe(
                fullPipeName,
                NamedPipeNative.PIPE_ACCESS_DUPLEX | NamedPipeNative.FILE_FLAG_FIRST_PIPE_INSTANCE | NamedPipeNative.FILE_FLAG_OVERLAPPED,
                NamedPipeNative.PIPE_TYPE_BYTE | NamedPipeNative.PIPE_READMODE_BYTE,
                1,
                1024,
                1024,
                0,
                securityAttributes);

            int lastError = Marshal.GetLastWin32Error();
            if (securityDescHandle != null)
            {
                securityDescHandle.Value.Free();
            }

            if (pipeHandle.IsInvalid)
            {
                throw new InvalidOperationException();
            }
            // Create the .Net NamedPipeServerStream wrapper.
            try
            {
                return new NamedPipeServerStream(
                    PipeDirection.InOut,
                    true,                       // IsAsync
                    false,                      // IsConnected
                    pipeHandle);
            }
            catch (Exception)
            {
                pipeHandle.Dispose();
                throw;
            }
        }
        #endregion
    }
}
