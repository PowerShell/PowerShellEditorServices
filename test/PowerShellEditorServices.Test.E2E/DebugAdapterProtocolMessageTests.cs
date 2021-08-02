// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using Xunit;
using Xunit.Abstractions;

namespace PowerShellEditorServices.Test.E2E
{
    public class DebugAdapterProtocolMessageTests : IAsyncLifetime
    {
        private const string TestOutputFileName = "__dapTestOutputFile.txt";
        private readonly static bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private readonly static string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private readonly static string s_testOutputPath = Path.Combine(s_binDir, TestOutputFileName);

        private readonly ITestOutputHelper _output;
        private DebugAdapterClient PsesDebugAdapterClient;
        private PsesStdioProcess _psesProcess;

        public TaskCompletionSource<object> Started { get; } = new TaskCompletionSource<object>();

        public DebugAdapterProtocolMessageTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task InitializeAsync()
        {
            var factory = new LoggerFactory();
            _psesProcess = new PsesStdioProcess(factory, true);
            await _psesProcess.Start().ConfigureAwait(false);

            var initialized = new TaskCompletionSource<bool>();
            PsesDebugAdapterClient = DebugAdapterClient.Create(options =>
            {
                options
                    .WithInput(_psesProcess.OutputStream)
                    .WithOutput(_psesProcess.InputStream)
                    // The OnStarted delegate gets run when we receive the _Initialized_ event from the server:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Events_Initialized
                    .OnStarted((client, token) => {
                        Started.SetResult(true);
                        return Task.CompletedTask;
                    })
                    // The OnInitialized delegate gets run when we first receive the _Initialize_ response:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                    .OnInitialized((client, request, response, token) => {
                        initialized.SetResult(true);
                        return Task.CompletedTask;
                    });
            });

            // PSES follows the following flow:
            // Receive a Initialize request
            // Run Initialize handler and send response back
            // Receive a Launch/Attach request
            // Run Launch/Attach handler and send response back
            // PSES sends the initialized event at the end of the Launch/Attach handler

            // The way that the Omnisharp client works is that this Initialize method doesn't return until
            // after OnStarted is run... which only happens when Initialized is received from the server.
            // so if we would await this task, it would deadlock.
            // To get around this, we run the Initialize() without await but use a `TaskCompletionSource<bool>`
            // that gets completed when we receive the response to Initialize
            // This tells us that we are ready to send messages to PSES... but are not stuck waiting for
            // Initialized.
            PsesDebugAdapterClient.Initialize(CancellationToken.None).ConfigureAwait(false);
            await initialized.Task.ConfigureAwait(false);
        }

        public async Task DisposeAsync()
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

        private string NewTestFile(string script, bool isPester = false)
        {
            string fileExt = isPester ? ".Tests.ps1" : ".ps1";
            string filePath = Path.Combine(s_binDir, Path.GetRandomFileName() + fileExt);
            File.WriteAllText(filePath, script);

            return filePath;
        }

        private string GenerateScriptFromLoggingStatements(params string[] logStatements)
        {
            if (logStatements.Length == 0)
            {
                throw new ArgumentNullException("Expected at least one argument.");
            }

            // Have script create/overwrite file first with `>`.
            StringBuilder builder = new StringBuilder().Append('\'').Append(logStatements[0]).Append("' > '").Append(s_testOutputPath).AppendLine("'");
            for (int i = 1; i < logStatements.Length; i++)
            {
                // Then append to that script with `>>`.
                builder.Append('\'').Append(logStatements[i]).Append("' >> '").Append(s_testOutputPath).AppendLine("'");
            }

            _output.WriteLine("Script is:");
            _output.WriteLine(builder.ToString());
            return builder.ToString();
        }

        private string[] GetLog()
        {
            return File.ReadLines(s_testOutputPath).ToArray();
        }

        [Trait("Category", "DAP")]
        [Fact]
        public void CanInitializeWithCorrectServerSettings()
        {
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsConditionalBreakpoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsConfigurationDoneRequest);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsFunctionBreakpoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsHitConditionalBreakpoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsLogPoints);
            Assert.True(PsesDebugAdapterClient.ServerSettings.SupportsSetVariable);
        }

        [Trait("Category", "DAP")]
        [Fact]
        public async Task CanLaunchScriptWithNoBreakpointsAsync()
        {
            string filePath = NewTestFile(GenerateScriptFromLoggingStatements("works"));

            await PsesDebugAdapterClient.LaunchScript(filePath, Started).ConfigureAwait(false);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments()).ConfigureAwait(false);
            Assert.NotNull(configDoneResponse);

            // At this point the script should be running so lets give it time
            await Task.Delay(2000).ConfigureAwait(false);

            string[] log = GetLog();
            Assert.Equal("works", log[0]);
        }

        [Trait("Category", "DAP")]
        [SkippableFact]
        public async Task CanSetBreakpointsAsync()
        {
            Skip.If(
                PsesStdioProcess.RunningInConstainedLanguageMode,
                "You can't set breakpoints in ConstrainedLanguage mode.");

            string filePath = NewTestFile(GenerateScriptFromLoggingStatements(
                "before breakpoint",
                "at breakpoint",
                "after breakpoint"
            ));

            await PsesDebugAdapterClient.LaunchScript(filePath, Started).ConfigureAwait(false);

            // {"command":"setBreakpoints","arguments":{"source":{"name":"dfsdfg.ps1","path":"/Users/tyleonha/Code/PowerShell/Misc/foo/dfsdfg.ps1"},"lines":[2],"breakpoints":[{"line":2}],"sourceModified":false},"type":"request","seq":3}
            SetBreakpointsResponse setBreakpointsResponse = await PsesDebugAdapterClient.SetBreakpoints(new SetBreakpointsArguments
            {
                Source = new Source
                {
                    Name = Path.GetFileName(filePath),
                    Path = filePath
                },
                Lines = new long[] { 2 },
                Breakpoints = new SourceBreakpoint[]
                {
                    new SourceBreakpoint
                    {
                        Line = 2,
                    }
                },
                SourceModified = false,
            }).ConfigureAwait(false);

            var breakpoint = setBreakpointsResponse.Breakpoints.First();
            Assert.True(breakpoint.Verified);
            Assert.Equal(filePath, breakpoint.Source.Path, ignoreCase: s_isWindows);
            Assert.Equal(2, breakpoint.Line);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments()).ConfigureAwait(false);
            Assert.NotNull(configDoneResponse);

            // At this point the script should be running so lets give it time
            await Task.Delay(2000).ConfigureAwait(false);

            string[] log = GetLog();
            Assert.Single(log, (i) => i == "before breakpoint");

            ContinueResponse continueResponse = await PsesDebugAdapterClient.RequestContinue(new ContinueArguments
            {
                ThreadId = 1,
            }).ConfigureAwait(true);

            Assert.NotNull(continueResponse);

            // At this point the script should be running so lets give it time
            await Task.Delay(2000).ConfigureAwait(false);

            log = GetLog();
            Assert.Collection(log,
                (i) => Assert.Equal("before breakpoint", i),
                (i) => Assert.Equal("at breakpoint", i),
                (i) => Assert.Equal("after breakpoint", i));
        }
    }
}
