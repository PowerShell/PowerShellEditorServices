// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
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
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblies = allAssemblies.FirstOrDefault(a => a.FullName.Contains("Microsoft.PowerShell.PSReadLine2"));
            var type = assemblies?.ExportedTypes?.FirstOrDefault(a => a.FullName == "Microsoft.PowerShell.PSConsoleReadLine");
            MethodInfo method = type?.GetMethod(
                "ReadLine",
                new [] { typeof(Runspace), typeof(EngineIntrinsics), typeof(CancellationToken) });

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
