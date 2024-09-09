// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace PowerShellEditorServices.Test.E2E
{
    public static class DebugAdapterClientExtensions
    {
        public static async Task LaunchScript(this DebugAdapterClient debugAdapterClient, string script, TaskCompletionSource<object> started, string executeMode = "DotSource")
        {
            _ = await debugAdapterClient.Launch(
                new PsesLaunchRequestArguments
                {
                    NoDebug = false,
                    Script = script,
                    Cwd = "",
                    CreateTemporaryIntegratedConsole = false,
                    ExecuteMode = executeMode,
                }) ?? throw new Exception("Launch response was null.");

            // This will check to see if we received the Initialized event from the server.
            await started.Task;
        }
    }
}
