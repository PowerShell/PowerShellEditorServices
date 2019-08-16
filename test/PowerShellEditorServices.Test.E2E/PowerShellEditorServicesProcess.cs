using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Client.Processes;

namespace PowerShellEditorServices.Test.E2E
{
    public class PowerShellEditorServicesProcess : NamedPipeServerProcess
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

        private readonly Process _psesProcess;

        public PowerShellEditorServicesProcess(ILoggerFactory loggerFactory)
            : base(null, loggerFactory)
        {
            _psesProcess = new Process();

            _psesProcess.StartInfo.FileName = Environment.GetEnvironmentVariable("PWSH_EXE_NAME") ?? "pwsh";
            //_psesProcess.StartInfo.CreateNoWindow = true;
            //_psesProcess.StartInfo.UseShellExecute = false;
            //_psesProcess.StartInfo.RedirectStandardInput = true;
            //_psesProcess.StartInfo.RedirectStandardOutput = true;
            //_psesProcess.StartInfo.RedirectStandardError = true;
            _psesProcess.StartInfo.ArgumentList.Add("-NoLogo");
            _psesProcess.StartInfo.ArgumentList.Add("-NoProfile");
            _psesProcess.StartInfo.ArgumentList.Add("-EncodedCommand");

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
                "-BundledModulesPath", s_bundledModulePath
            };

            var base64Str = System.Convert.ToBase64String(
                System.Text.Encoding.Unicode.GetBytes(string.Join(' ', args)));

            _psesProcess.StartInfo.ArgumentList.Add(base64Str);
        }

        public async override Task Start()
        {
            _psesProcess.Start();

            var i = 0;
            while (!File.Exists(s_sessionDetailsPath))
            {
                if (i >= 10)
                {
                    throw new Exception("No session file found - server failed to start");
                }

                Thread.Sleep(2000);
                i++;
            }

            var sessionDetails = JObject
                .Parse(File.ReadAllText(s_sessionDetailsPath))
                .ToObject<SessionDetails>();

            ServerInputStream = new NamedPipeServerStream(Path.GetRandomFileName(), PipeDirection.Out, 2, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, inBufferSize: 1024, outBufferSize: 1024);
            ServerOutputStream = new NamedPipeServerStream(Path.GetRandomFileName(), PipeDirection.In, 2, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, inBufferSize: 1024, outBufferSize: 1024);

            if (sessionDetails.LanguageServicePipeName != null)
            {
                ClientInputStream = new NamedPipeClientStream(".",
                    sessionDetails.LanguageServicePipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                ClientOutputStream = ClientInputStream;
                await ClientInputStream.ConnectAsync();
            }
            else
            {
                ClientInputStream = new NamedPipeClientStream(".",
                    sessionDetails.LanguageServiceReadPipeName,
                    PipeDirection.In,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                ClientOutputStream = new NamedPipeClientStream(".",
                    sessionDetails.LanguageServiceWritePipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await ClientInputStream.ConnectAsync();
                await ClientOutputStream.ConnectAsync();
            }

            ServerStartCompletion.TrySetResult(null);
        }

        public override Task Stop()
        {
            if(!_psesProcess.HasExited) _psesProcess.Kill();
            return base.Stop();
        }

        public override Stream InputStream => ClientInputStream;
        public override Stream OutputStream => ClientOutputStream;

        private class SessionDetails
        {
            public string Status { get; set; }
            public string DebugServiceTransport { get; set; }
            public string DebugServicePipeName { get; set; }
            public string DebugServiceWritePipeName { get; set; }
            public string DebugServiceReadPipeName { get; set; }
            public string LanguageServicePipeName { get; set; }
            public string LanguageServiceReadPipeName { get; set; }
            public string LanguageServiceWritePipeName { get; set; }
            public string LanguageServiceTransport { get; set; }
        }
    }
}
