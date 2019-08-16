using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.E2E
{
    public class TestsFixture : IAsyncLifetime
    {
        private readonly static string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public PowerShellEditorServicesProcess _psesProcess;
        public LanguageClient LanguageClient { get; private set; }
        public List<Diagnostic> Diagnostics { get; set; }

        public async Task InitializeAsync()
        {
            var factory = new LoggerFactory();

            _psesProcess = new PowerShellEditorServicesProcess(factory);
            await _psesProcess.Start();

            LanguageClient = new LanguageClient(factory, _psesProcess);

            DirectoryInfo testdir =
                Directory.CreateDirectory(Path.Combine(s_binDir, Path.GetRandomFileName()));
            await LanguageClient.Initialize(testdir.FullName);

            Diagnostics = new List<Diagnostic>();
            LanguageClient.TextDocument.OnPublishDiagnostics((uri, diagnostics) =>
            {
                Diagnostics.AddRange(diagnostics);
            });
        }

        public async Task DisposeAsync()
        {
            await LanguageClient.Shutdown();
            await _psesProcess.Stop();
            LanguageClient?.Dispose();
        }
    }
}
