using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace PowerShellEditorServices.Test.E2E
{
    public abstract class TestsFixture : IAsyncLifetime
    {
        protected readonly static string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly static string s_bundledModulePath = new FileInfo(Path.Combine(
            s_binDir,
            "..", "..", "..", "..", "..",
            "module")).FullName;

        private readonly static string s_sessionDetailsPath = Path.Combine(
            s_binDir,
            $"pses_test_sessiondetails_{Path.GetRandomFileName()}");

        private readonly static string s_logPath = Path.Combine(
            Environment.GetEnvironmentVariable("BUILD_ARTIFACTSTAGINGDIRECTORY") ?? s_binDir,
            $"pses_test_logs_{Path.GetRandomFileName()}");

        const string s_logLevel = "Diagnostic";
        readonly static string[] s_featureFlags = { "PSReadLine" };
        const string s_hostName = "TestHost";
        const string s_hostProfileId = "TestHost";
        const string s_hostVersion = "1.0.0";
        readonly static string[] s_additionalModules = { "PowerShellEditorServices.VSCode" };

        protected StdioServerProcess _psesProcess;

        public static string PwshExe { get; } = Environment.GetEnvironmentVariable("PWSH_EXE_NAME") ?? "pwsh";

        public static bool IsWindowsPowerShell { get; } = PwshExe.Contains("powershell");
        public static bool RunningInConstainedLanguageMode { get; } =
            Environment.GetEnvironmentVariable("__PSLockdownPolicy", EnvironmentVariableTarget.Machine) != null;

        public virtual bool IsDebugAdapterTests { get; set; }

        public async Task InitializeAsync()
        {
            var factory = new LoggerFactory();

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = PwshExe
            };
            processStartInfo.ArgumentList.Add("-NoLogo");
            processStartInfo.ArgumentList.Add("-NoProfile");
            processStartInfo.ArgumentList.Add("-EncodedCommand");

            List<string> args = new List<string>
            {
                "&",
                SingleQuoteEscape(Path.Combine(s_bundledModulePath, "PowerShellEditorServices", "Start-EditorServices.ps1")),
                "-LogPath", SingleQuoteEscape(s_logPath),
                "-LogLevel", s_logLevel,
                "-SessionDetailsPath", SingleQuoteEscape(s_sessionDetailsPath),
                "-FeatureFlags", string.Join(',', s_featureFlags),
                "-HostName", s_hostName,
                "-HostProfileId", s_hostProfileId,
                "-HostVersion", s_hostVersion,
                "-AdditionalModules", string.Join(',', s_additionalModules),
                "-BundledModulesPath", SingleQuoteEscape(s_bundledModulePath),
                "-Stdio"
            };

            if (IsDebugAdapterTests)
            {
                args.Add("-DebugServiceOnly");
            }

            string base64Str = Convert.ToBase64String(
                System.Text.Encoding.Unicode.GetBytes(string.Join(' ', args)));

            processStartInfo.ArgumentList.Add(base64Str);

            _psesProcess = new StdioServerProcess(factory, processStartInfo);
            await _psesProcess.Start();

            await CustomInitializeAsync(factory, _psesProcess.OutputStream, _psesProcess.InputStream).ConfigureAwait(false);
        }

        public virtual async Task DisposeAsync()
        {
            await _psesProcess.Stop();
        }

        public abstract Task CustomInitializeAsync(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream);

        private static string SingleQuoteEscape(string str)
        {
            return $"'{str.Replace("'", "''")}'";
        }
    }
}
