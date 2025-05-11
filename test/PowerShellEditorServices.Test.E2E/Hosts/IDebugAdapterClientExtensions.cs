// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using OmniSharp.Extensions.DebugAdapter.Protocol.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace PowerShellEditorServices.Test.E2E
{
    public static class IDebugAdapterClientExtensions
    {
        public static async Task LaunchScript(this IDebugAdapterClient debugAdapterClient, string script, string executeMode = "DotSource")
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
        }
    }
}
