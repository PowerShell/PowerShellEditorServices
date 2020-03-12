using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Client.Processes;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace PowerShellEditorServices.Test.E2E
{
    public class LSPTestsFixture : TestsFixture
    {
        public override bool IsDebugAdapterTests => false;

        public LanguageClient LanguageClient { get; private set; }
        public List<Diagnostic> Diagnostics { get; set; }

        public async override Task CustomInitializeAsync(
            ILoggerFactory factory,
            StdioServerProcess process)
        {
            LanguageClient = new LanguageClient(factory, process);

            DirectoryInfo testdir =
                Directory.CreateDirectory(Path.Combine(s_binDir, Path.GetRandomFileName()));

            await LanguageClient.Initialize(testdir.FullName);

            // Make sure Script Analysis is enabled because we'll need it in the tests.
            LanguageClient.Workspace.DidChangeConfiguration(JObject.Parse(@"
{
    ""powershell"": {
        ""scriptAnalysis"": {
            ""enable"": true
        }
    }
}
"));

            Diagnostics = new List<Diagnostic>();
            LanguageClient.TextDocument.OnPublishDiagnostics((uri, diagnostics) =>
            {
                Diagnostics.AddRange(diagnostics.Where(d => d != null));
            });
        }

        public override async Task DisposeAsync()
        {
            try
            {
                await LanguageClient.Shutdown();
                await _psesProcess.Stop();
                LanguageClient?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Language client has a disposal bug in it
            }
        }
    }
}
