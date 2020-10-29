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

        public TaskCompletionSource<object> Started { get; private set; }

        public async override Task CustomInitializeAsync(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream)
        {
            var started = new TaskCompletionSource<object>();
            PsesDebugAdapterClient = DebugAdapterClient.Create(options =>
            {
                options.WithSerializer(new DapProtocolSerializer());
                options
                    .WithInput(inputStream)
                    .WithOutput(outputStream)
                    .OnStarted((client, token) => {
                        Started.SetResult(true);
                        return Task.CompletedTask;
                    })
                    .OnInitialized((client, request, response, token) => {
                        started.SetResult(true);
                        return Task.CompletedTask;
                    });
            });

            PsesDebugAdapterClient.Initialize(CancellationToken.None).ConfigureAwait(false);
            await started.Task.ConfigureAwait(false);
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
