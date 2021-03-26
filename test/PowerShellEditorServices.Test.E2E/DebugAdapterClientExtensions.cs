//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using System.Threading;

namespace PowerShellEditorServices.Test.E2E
{
    public static class DebugAdapterClientExtensions
    {
        public static async Task LaunchScript(this DebugAdapterClient debugAdapterClient, string filePath, TaskCompletionSource<object> started)
        {
            LaunchResponse launchResponse = await debugAdapterClient.Launch(new PsesLaunchRequestArguments
            {
                NoDebug = false,
                Script = filePath,
                Cwd = "",
                CreateTemporaryIntegratedConsole = false,
            }).ConfigureAwait(false);

            if (launchResponse == null)
            {
                throw new Exception("Launch response was null.");
            }

            // This will check to see if we received the Initialized event from the server.
            await Task.Run(
                async () => await started.Task.ConfigureAwait(false),
                new CancellationTokenSource(2000).Token).ConfigureAwait(false);
        }
    }
}
