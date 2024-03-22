﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.Configuration;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Xunit;
using Xunit.Abstractions;

namespace PowerShellEditorServices.Test.E2E
{
    public class LSPTestsFixture : IAsyncLifetime
    {
        protected static readonly string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private const bool IsDebugAdapterTests = false;

        public ILanguageClient PsesLanguageClient { get; private set; }
        public List<LogMessageParams> Messages = new();
        public List<Diagnostic> Diagnostics = new();
        internal List<PsesTelemetryEvent> TelemetryEvents = new();
        public ITestOutputHelper Output { get; set; }

        protected PsesStdioProcess _psesProcess;
        public int ProcessId => _psesProcess.Id;

        public async Task InitializeAsync()
        {
            LoggerFactory factory = new();
            _psesProcess = new PsesStdioProcess(factory, IsDebugAdapterTests);
            await _psesProcess.Start();

            DirectoryInfo testDir =
                Directory.CreateDirectory(Path.Combine(s_binDir, Path.GetRandomFileName()));

            PsesLanguageClient = LanguageClient.PreInit(options =>
            {
                options
                    .WithInput(_psesProcess.OutputStream)
                    .WithOutput(_psesProcess.InputStream)
                    .WithWorkspaceFolder(DocumentUri.FromFileSystemPath(testDir.FullName), "testdir")
                    .WithInitializationOptions(new { EnableProfileLoading = false })
                    .OnPublishDiagnostics(diagnosticParams => Diagnostics.AddRange(diagnosticParams.Diagnostics.Where(d => d != null)))
                    .OnLogMessage(logMessageParams => {
                        Output?.WriteLine($"{logMessageParams.Type}: {logMessageParams.Message}");
                        Messages.Add(logMessageParams);
                    })
                    .OnTelemetryEvent(telemetryEventParams => TelemetryEvents.Add(
                        new PsesTelemetryEvent
                        {
                            EventName = (string)telemetryEventParams.ExtensionData["eventName"],
                            Data = telemetryEventParams.ExtensionData["data"] as JObject
                        }));

                // Enable all capabilities this this is for testing.
                // This will be a built in feature of the Omnisharp client at some point.
                IEnumerable<Type> capabilityTypes = typeof(ICapability).Assembly.GetExportedTypes()
                    .Where(z => typeof(ICapability).IsAssignableFrom(z) && z.IsClass && !z.IsAbstract);
                foreach (Type capabilityType in capabilityTypes)
                {
                    options.WithCapability(Activator.CreateInstance(capabilityType, Array.Empty<object>()) as ICapability);
                }
            });

            await PsesLanguageClient.Initialize(CancellationToken.None);

            // Make sure Script Analysis is enabled because we'll need it in the tests.
            // This also makes sure the configuration is set to default values.
            PsesLanguageClient.Workspace.DidChangeConfiguration(
                new DidChangeConfigurationParams
                {
                    Settings = JToken.FromObject(new LanguageServerSettingsWrapper
                    {
                        Files = new EditorFileSettings(),
                        Search = new EditorSearchSettings(),
                        Powershell = new LanguageServerSettings()
                    })
                });
        }

        public async Task DisposeAsync()
        {
            await PsesLanguageClient.Shutdown();
            await _psesProcess.Stop();
            PsesLanguageClient?.Dispose();
        }
    }
}
