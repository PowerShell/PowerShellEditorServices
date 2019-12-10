//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

#if !CoreCLR
using System.Security.Principal;
using System.Security.AccessControl;
#endif

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Utility class for handling named pipe creation in .NET Core and .NET Framework.
    /// </summary>
    internal static class NamedPipeUtils
    {
#if !CoreCLR
        // .NET Framework requires the buffer size to be specified
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

            // In .NET Framework, we must manually ACL the named pipes we create

            var pipeSecurity = new PipeSecurity();

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Allow the Administrators group full access to the pipe.
                pipeSecurity.AddAccessRule(
                    new PipeAccessRule(
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

        /// <summary>
        /// Generate a named pipe name known to not already be in use.
        /// </summary>
        /// <param name="prefixes">Prefix variants of the pipename to test, if any.</param>
        /// <returns>A named pipe name or name suffix that is safe to you.</returns>
        public static string GenerateValidNamedPipeName(IReadOnlyCollection<string> prefixes = null)
        {
            int tries = 0;
            do
            {
                string pipeName = $"PSES_{Path.GetRandomFileName()}";

                // In the simple prefix-less case, just test the pipe name
                if (prefixes == null)
                {
                    if (!IsPipeNameValid(pipeName))
                    {
                        continue;
                    }

                    return pipeName;
                }

                // If we have prefixes, test that all prefix/pipename combinations are valid
                bool allPipeNamesValid = true;
                foreach (string prefix in prefixes)
                {
                    string prefixedPipeName = $"{prefix}_{pipeName}";
                    if (!IsPipeNameValid(prefixedPipeName))
                    {
                        allPipeNamesValid = false;
                        break;
                    }
                }

                if (allPipeNamesValid)
                {
                    return pipeName;
                }

            } while (tries < 10);

            throw new IOException("Unable to create named pipe; no available names");
        }

        /// <summary>
        /// Validate that a named pipe file name is a legitimate named pipe file name and is not already in use.
        /// </summary>
        /// <param name="pipeName">The named pipe name to validate. This should be a simple name rather than a path.</param>
        /// <returns>True if the named pipe name is valid, false otherwise.</returns>
        public static bool IsPipeNameValid(string pipeName)
        {
            if (string.IsNullOrEmpty(pipeName))
            {
                return false;
            }

            return !File.Exists(GetNamedPipePath(pipeName));
        }

        /// <summary>
        /// Get the path of a named pipe given its name.
        /// </summary>
        /// <param name="pipeName">The simple name of the named pipe.</param>
        /// <returns>The full path of the named pipe.</returns>
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
