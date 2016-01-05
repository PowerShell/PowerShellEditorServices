//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.PowerShell.EditorServices.Protocol.Client;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Owin;
using Owin.WebSocket.Extensions;
using Xunit;
using Microsoft.PowerShell.EditorServices.Channel.WebSocket;

namespace Microsoft.PowerShell.EditorServices.Test.Channel.WebSocket
{
    public class WebSocketChannelTest : IAsyncLifetime
    {
        private IDisposable webapp;
        private LanguageServiceClient languageServiceClient;

        public async Task InitializeAsync()
        {
            webapp = WebApp.Start<Startup>("http://localhost:9999");
            this.languageServiceClient =
                 new LanguageServiceClient(
                     new WebsocketClientChannel("ws://localhost:9999/language"));

            await this.languageServiceClient.Start();
        }

        public Task DisposeAsync()
        {
            webapp.Dispose();
            return Task.Delay(0);
        }

        [Fact]
        public async Task ServiceCommunicatesOverWebsockets()
        {
            string expandedText =
                await this.SendRequest(
                    ExpandAliasRequest.Type,
                    "gci\r\npwd");

            Assert.Equal("Get-ChildItem\r\nGet-Location", expandedText);
        }

        private Task<TResult> SendRequest<TParams, TResult>(
                RequestType<TParams, TResult> requestType,
                TParams requestParams)
                    {
                        return
                            this.languageServiceClient.SendRequest(
                                requestType,
                                requestParams);
        }
    }

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapWebSocketRoute<LanguageServerWebSocketConnection>("/language");
            app.MapWebSocketRoute<DebugAdapterWebSocketConnection>("/debug");
        }
    }
}

