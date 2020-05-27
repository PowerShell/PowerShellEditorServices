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
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Xunit.Abstractions;

namespace PowerShellEditorServices.Test.E2E
{
    public class LSPTestsFixture : TestsFixture
    {
        public override bool IsDebugAdapterTests => false;

        public ILanguageClient PsesLanguageClient { get; private set; }
        public List<Diagnostic> Diagnostics { get; set; }

        public ITestOutputHelper Output { get; set; }

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
                    })
                    .OnLogMessage(logMessageParams =>
                    {
                        Output?.WriteLine($"{logMessageParams.Type.ToString()}: {logMessageParams.Message}");
                    });

                // Enable all capabilities this this is for testing.
                // This will be a built in feature of the Omnisharp client at some point.
                var capabilityTypes = typeof(ICapability).Assembly.GetExportedTypes()
                    .Where(z => typeof(ICapability).IsAssignableFrom(z))
                    .Where(z => z.IsClass && !z.IsAbstract);
                foreach (Type capabilityType in capabilityTypes)
                {
                    options.WithCapability(Activator.CreateInstance(capabilityType, Array.Empty<object>()) as ICapability);
                }
            });

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
