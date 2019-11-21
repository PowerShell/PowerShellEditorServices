using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    internal static class NamedPipeUtils
    {
#if !CoreCLR
        private const int PipeBufferSize = 1024;
#endif

        internal static NamedPipeServerStream CreateNamedPipe(
            string pipeName,
            PipeDirection pipeDirection)
        {
#if CoreCLR
            return new NamedPipeServerStream(
                pipeName: pipeName,
                direction: pipeDirection,
                maxNumberOfServerInstances: 1,
                transmissionMode: PipeTransmissionMode.Byte,
                options: PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
#else

            var pipeSecurity = new PipeSecurity();

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Allow the Administrators group full access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null).Translate(typeof(NTAccount)),
                    PipeAccessRights.FullControl, AccessControlType.Allow));
            }
            else
            {
                // Allow the current user read/write access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    PipeAccessRights.ReadWrite, AccessControlType.Allow));
            }

            return new NamedPipeServerStream(
                pipeName,
                pipeDirection,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: PipeBufferSize,
                outBufferSize: PipeBufferSize,
                pipeSecurity);
#endif
        }

        public static string GenerateValidNamedPipeName()
        {
            int tries = 0;
            do
            {
                string pipeName = $"PSES_{Path.GetRandomFileName()}";

                if (IsPipeNameValid(pipeName))
                {
                    return pipeName;
                }

            } while (tries < 10);

            throw new Exception("Unable to create named pipe; no available names");
        }

        public static bool IsPipeNameValid(string pipeName)
        {
            if (string.IsNullOrEmpty(pipeName))
            {
                return false;
            }

            return !File.Exists(GetNamedPipePath(pipeName));
        }

        public static string GetNamedPipePath(string pipeName)
        {
#if CoreCLR
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{pipeName}");
            }
#endif

            return $@"\\.\pipe\{pipeName}";
        }
    }
}
