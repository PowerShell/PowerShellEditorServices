using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace PowerShellEditorServices.Test.E2E
{
    public class LSPTestsFixture : TestsFixture
    {
        public override bool IsDebugAdapterTests => false;

        public ILanguageClient PsesLanguageClient { get; private set; }
        public List<Diagnostic> Diagnostics { get; set; }

        public async override Task CustomInitializeAsync(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream)
        {
            Diagnostics = new List<Diagnostic>();
            DirectoryInfo testdir =
                Directory.CreateDirectory(Path.Combine(s_binDir, Path.GetRandomFileName()));

            PsesLanguageClient = LanguageClient.PreInit(options =>
            {
                options
                    .WithInput(inputStream)
                    .WithOutput(outputStream)
                    .WithRootUri(DocumentUri.FromFileSystemPath(testdir.FullName))
                    .OnPublishDiagnostics(diagnosticParams =>
                    {
                        Diagnostics.AddRange(diagnosticParams.Diagnostics.Where(d => d != null));
                    });

                // Enable all capabilities this this is for testing.
                var capabilities = typeof(ICapability).Assembly.GetExportedTypes()
                    .Where(z => typeof(ICapability).IsAssignableFrom(z))
                    .Where(z => z.IsClass && !z.IsAbstract);
                foreach (var item in capabilities)
                {
                    options.WithCapability(Activator.CreateInstance(item, Array.Empty<object>()) as ICapability);
                }
            });

            await Task.Delay(2000);
            while (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Threading.Thread.Sleep(1000);
                // System.Console.WriteLine(System.Diagnostics.Process.GetCurrentProcess().Id);
            }

            await PsesLanguageClient.Initialize(CancellationToken.None);

            // Make sure Script Analysis is enabled because we'll need it in the tests.
            PsesLanguageClient.Workspace.DidChangeConfiguration(
                new DidChangeConfigurationParams
                {
                    Settings = JObject.Parse(@"
{
    ""powershell"": {
        ""scriptAnalysis"": {
            ""enable"": true
        }
    }
}
")
                });
        }

        public override async Task DisposeAsync()
        {
            try
            {
                await PsesLanguageClient.Shutdown();
                await _psesProcess.Stop();
                PsesLanguageClient?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Language client has a disposal bug in it
            }
        }
    }
}
