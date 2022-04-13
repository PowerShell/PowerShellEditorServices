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
        private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string s_testOutputPath = Path.Combine(s_binDir, TestOutputFileName);

        private readonly ITestOutputHelper _output;
        private DebugAdapterClient PsesDebugAdapterClient;
        private PsesStdioProcess _psesProcess;

        public TaskCompletionSource<object> Started { get; } = new TaskCompletionSource<object>();

        public DebugAdapterProtocolMessageTests(ITestOutputHelper output) => _output = output;

        public async Task InitializeAsync()
        {
            LoggerFactory factory = new();
            _psesProcess = new PsesStdioProcess(factory, true);
            await _psesProcess.Start().ConfigureAwait(false);

            TaskCompletionSource<bool> initialized = new();

            _psesProcess.ProcessExited += (sender, args) =>
            {
                initialized.TrySetException(new ProcessExitedException("Initialization failed due to process failure", args.ExitCode, args.ErrorMessage));
                Started.TrySetException(new ProcessExitedException("Startup failed due to process failure", args.ExitCode, args.ErrorMessage));
            };

            PsesDebugAdapterClient = DebugAdapterClient.Create(options =>
            {
                options
                    .WithInput(_psesProcess.OutputStream)
                    .WithOutput(_psesProcess.InputStream)
                    // The OnStarted delegate gets run when we receive the _Initialized_ event from the server:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Events_Initialized
                    .OnStarted((_, _) =>
                    {
                        Started.SetResult(true);
                        return Task.CompletedTask;
                    })
                    // The OnInitialized delegate gets run when we first receive the _Initialize_ response:
                    // https://microsoft.github.io/debug-adapter-protocol/specification#Requests_Initialize
                    .OnInitialized((_, _, _, _) =>
                    {
                        initialized.SetResult(true);
                        return Task.CompletedTask;
                    });

                options.OnUnhandledException = (exception) =>
                {
                    initialized.SetException(exception);
                    Started.SetException(exception);
                };
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

        private static string NewTestFile(string script, bool isPester = false)
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
                throw new ArgumentNullException(nameof(logStatements), "Expected at least one argument.");
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

        private static string[] GetLog() => File.ReadLines(s_testOutputPath).ToArray();

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

            Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
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

        // This is a regression test for a bug where user code causes a new synchronization context
        // to be created, breaking the extension. It's most evident when debugging PowerShell
        // scripts that use System.Windows.Forms. It required fixing both Editor Services and
        // OmniSharp.
        //
        // This test depends on PowerShell being able to load System.Windows.Forms, which only works
        // reliably with Windows PowerShell. It works with PowerShell Core in the real-world;
        // however, our host executable is xUnit, not PowerShell. So by restricting to Windows
        // PowerShell, we avoid all issues with our test project (and the xUnit executable) not
        // having System.Windows.Forms deployed, and can instead rely on the Windows Global Assembly
        // Cache (GAC) to find it.
        [Trait("Category", "DAP")]
        [SkippableFact]
        public async Task CanStepPastSystemWindowsForms()
        {
            Skip.IfNot(PsesStdioProcess.IsWindowsPowerShell);
            Skip.If(PsesStdioProcess.RunningInConstainedLanguageMode);

            string filePath = NewTestFile(string.Join(Environment.NewLine, new[]
                {
                    "Add-Type -AssemblyName System.Windows.Forms",
                    "$global:form = New-Object System.Windows.Forms.Form",
                    "Write-Host $form"
                }));

            await PsesDebugAdapterClient.LaunchScript(filePath, Started).ConfigureAwait(false);

            SetFunctionBreakpointsResponse setBreakpointsResponse = await PsesDebugAdapterClient.SetFunctionBreakpoints(
                new SetFunctionBreakpointsArguments
                {
                    Breakpoints = new FunctionBreakpoint[]
                        { new FunctionBreakpoint { Name = "Write-Host", } }
                }).ConfigureAwait(false);

            Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
            Assert.True(breakpoint.Verified);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments()).ConfigureAwait(false);
            Assert.NotNull(configDoneResponse);

            // At this point the script should be running so lets give it time
            await Task.Delay(2000).ConfigureAwait(false);

            VariablesResponse variablesResponse = await PsesDebugAdapterClient.RequestVariables(
                new VariablesArguments
                {
                    VariablesReference = 1
                }).ConfigureAwait(false);

            Variable form = variablesResponse.Variables.FirstOrDefault(v => v.Name == "$form");
            Assert.NotNull(form);
            Assert.Equal("System.Windows.Forms.Form, Text: ", form.Value);
        }
    }
}
