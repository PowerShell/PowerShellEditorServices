//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Commands
{
    /// <summary>
    /// The Start-EditorServices command, the conventional entrypoint for PowerShell Editor Services.
    /// </summary>
    public sealed class InvokeReadLineForEditorServicesCommand : PSCmdlet
    {
        private delegate string ReadLineInvoker(
            Runspace runspace,
            EngineIntrinsics engineIntrinsics,
            CancellationToken cancellationToken);

        private static Lazy<ReadLineInvoker> s_readLine = new Lazy<ReadLineInvoker>(() =>
        {
            Type type = Type.GetType("Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine2");
            MethodInfo method = type?.GetMethod(
                "ReadLine",
                new[] { typeof(Runspace), typeof(EngineIntrinsics), typeof(CancellationToken) });

            // TODO: Handle method being null here. This shouldn't ever happen.

            return (ReadLineInvoker)method.CreateDelegate(typeof(ReadLineInvoker));
        });

        /// <summary>
        /// The ID to give to the host's profile.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public CancellationToken CancellationToken { get; set; }

        protected override void EndProcessing()
        {
            // This returns a string.
            object result = s_readLine.Value(
                Runspace.DefaultRunspace,
                SessionState.PSVariable.Get("ExecutionContext").Value as EngineIntrinsics,
                CancellationToken
            );

            WriteObject(result);
        }
    }
}
