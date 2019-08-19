using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Client.Processes;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.E2E
{
    public class TestsFixture : IAsyncLifetime
    {
        private readonly static string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly static string s_bundledModulePath = new FileInfo(Path.Combine(
            s_binDir,
            "..", "..", "..", "..", "..",
            "module")).FullName;

        private readonly static string s_sessionDetailsPath = Path.Combine(
            s_binDir,
            $"pses_test_sessiondetails_{Path.GetRandomFileName()}");

        private readonly static string s_logPath = Path.Combine(
            s_binDir,
            $"pses_test_logs_{Path.GetRandomFileName()}");

        const string s_logLevel = "Diagnostic";
        readonly static string[] s_featureFlags = { "PSReadLine" };
        const string s_hostName = "TestHost";
        const string s_hostProfileId = "TestHost";
        const string s_hostVersion = "1.0.0";
        readonly static string[] s_additionalModules = { "PowerShellEditorServices.VSCode" };

        private StdioServerProcess _psesProcess;
        public LanguageClient LanguageClient { get; private set; }
        public List<Diagnostic> Diagnostics { get; set; }

        public async Task InitializeAsync()
        {
            var factory = new LoggerFactory();

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("PWSH_EXE_NAME") ?? "pwsh"
            };
            processStartInfo.ArgumentList.Add("-NoLogo");
            processStartInfo.ArgumentList.Add("-NoProfile");
            processStartInfo.ArgumentList.Add("-EncodedCommand");

            string[] args = {
                Path.Combine(s_bundledModulePath, "PowerShellEditorServices", "Start-EditorServices.ps1"),
                "-LogPath", s_logPath,
                "-LogLevel", s_logLevel,
                "-SessionDetailsPath", s_sessionDetailsPath,
                "-FeatureFlags", string.Join(',', s_featureFlags),
                "-HostName", s_hostName,
                "-HostProfileId", s_hostProfileId,
                "-HostVersion", s_hostVersion,
                "-AdditionalModules", string.Join(',', s_additionalModules),
                "-BundledModulesPath", s_bundledModulePath,
                "-Stdio"
            };

            string base64Str = Convert.ToBase64String(
                System.Text.Encoding.Unicode.GetBytes(string.Join(' ', args)));

            processStartInfo.ArgumentList.Add(base64Str);

            _psesProcess = new StdioServerProcess(factory, processStartInfo);
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
