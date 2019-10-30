//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Management.Automation.Host;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Server
{
    internal class NamedPipePsesLanguageServer : PsesLanguageServer
    {
        // This int will be casted to a PipeOptions enum that only exists in .NET Core 2.1 and up which is why it's not available to us in .NET Standard.
        private const int CurrentUserOnly = 0x20000000;

        // In .NET Framework, NamedPipeServerStream has a constructor that takes in a PipeSecurity object. We will use reflection to call the constructor,
        // since .NET Framework doesn't have the `CurrentUserOnly` PipeOption.
        // doc: https://docs.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.-ctor?view=netframework-4.7.2#System_IO_Pipes_NamedPipeServerStream__ctor_System_String_System_IO_Pipes_PipeDirection_System_Int32_System_IO_Pipes_PipeTransmissionMode_System_IO_Pipes_PipeOptions_System_Int32_System_Int32_System_IO_Pipes_PipeSecurity_
        private static readonly ConstructorInfo s_netFrameworkPipeServerConstructor =
            typeof(NamedPipeServerStream).GetConstructor(new[] { typeof(string), typeof(PipeDirection), typeof(int), typeof(PipeTransmissionMode), typeof(PipeOptions), typeof(int), typeof(int), typeof(PipeSecurity) });

        private readonly string _namedPipeName;
        private readonly string _outNamedPipeName;

        internal NamedPipePsesLanguageServer(
            ILoggerFactory factory,
            LogLevel minimumLogLevel,
            bool enableConsoleRepl,
            bool useLegacyReadLine,
            HashSet<string> featureFlags,
            HostDetails hostDetails,
            string[] additionalModules,
            PSHost internalHost,
            ProfilePaths profilePaths,
            string namedPipeName,
            string outNamedPipeName) : base(
                factory,
                minimumLogLevel,
                enableConsoleRepl,
                useLegacyReadLine,
                featureFlags,
                hostDetails,
                additionalModules,
                internalHost,
                profilePaths)
        {
            _namedPipeName = namedPipeName;
            _outNamedPipeName = outNamedPipeName;
        }

        protected override (Stream input, Stream output) GetInputOutputStreams()
        {
            NamedPipeServerStream namedPipe = CreateNamedPipe(
                    _namedPipeName,
                    _outNamedPipeName,
                    out NamedPipeServerStream outNamedPipe);

            var logger = LoggerFactory.CreateLogger("NamedPipeConnection");

            logger.LogInformation("Waiting for connection");
            namedPipe.WaitForConnection();
            if (outNamedPipe != null)
            {
                outNamedPipe.WaitForConnection();
            }

            logger.LogInformation("Connected");

            return (namedPipe, outNamedPipe ?? namedPipe);
        }

        private static NamedPipeServerStream CreateNamedPipe(
            string inOutPipeName,
            string outPipeName,
            out NamedPipeServerStream outPipe)
        {
            // .NET Core implementation is simplest so try that first
            if (VersionUtils.IsNetCore)
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

            var pipeSecurity = new PipeSecurity();

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
    }
}
