//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.DebugAdapter.Protocol.Serialization;

namespace PowerShellEditorServices.Test.E2E
{
    public class DAPTestsFixture : TestsFixture
    {
        public override bool IsDebugAdapterTests => true;

        public DebugAdapterClient PsesDebugAdapterClient { get; private set; }

        public TaskCompletionSource<object> Started { get; } = new TaskCompletionSource<object>();

        public async override Task CustomInitializeAsync(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream)
        {
            var initialized = new TaskCompletionSource<bool>();
            PsesDebugAdapterClient = DebugAdapterClient.Create(options =>
            {
                options
                    .WithInput(inputStream)
                    .WithOutput(outputStream)
                    // The OnStarted delegate gets run when we receive the _Initialized_ event from the server:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Events_Initialized
                    .OnStarted((client, token) => {
                        Started.SetResult(true);
                        return Task.CompletedTask;
                    })
                    // The OnInitialized delegate gets run when we first receive the _Initialize_ response:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                    .OnInitialized((client, request, response, token) => {
                        initialized.SetResult(true);
                        return Task.CompletedTask;
                    });
            });

            // PSES follows the following flow:
            // Receive a Initialize request
            // Run Initialize handler and send response back
            // Receive a Launch/Attach request
            // Run Launch/Attach handler and send response back
            // PSES sends the initialized event at the end of the Launch/Attach handler

            // The way that the Omnisharp client works is that this Initialize method doesn't return until
            // after OnStarted is run... which only happens when Initialized is received from the server.
            // so if we would await this task, it would deadlock.
            // To get around this, we run the Initialize() without await but use a `TaskCompletionSource<bool>`
            // that gets completed when we receive the response to Initialize
            // This tells us that we are ready to send messages to PSES... but are not stuck waiting for
            // Initialized.
            PsesDebugAdapterClient.Initialize(CancellationToken.None).ConfigureAwait(false);
            await initialized.Task.ConfigureAwait(false);
        }

        public override async Task DisposeAsync()
        {
            try
            {
                await PsesDebugAdapterClient.RequestDisconnect(new DisconnectArguments
                    {
                        Restart = false,
                        TerminateDebuggee = true
                    }).ConfigureAwait(false);
                await _psesProcess.Stop().ConfigureAwait(false);
                PsesDebugAdapterClient?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Language client has a disposal bug in it
            }
        }
    }
}
