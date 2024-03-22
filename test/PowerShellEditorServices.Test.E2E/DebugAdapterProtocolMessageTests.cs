﻿// Copyright (c) Microsoft Corporation.
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
    [Trait("Category", "DAP")]
    public class DebugAdapterProtocolMessageTests : IAsyncLifetime, IDisposable
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
            await _psesProcess.Start();

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
#pragma warning disable CS4014
            PsesDebugAdapterClient.Initialize(CancellationToken.None);
#pragma warning restore CS4014
            await initialized.Task;
        }

        public async Task DisposeAsync()
        {
            await PsesDebugAdapterClient.RequestDisconnect(new DisconnectArguments
            {
                Restart = false,
                TerminateDebuggee = true
            });
            await _psesProcess.Stop();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            PsesDebugAdapterClient?.Dispose();
            _psesProcess?.Dispose();
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

            // Clean up side effects from other test runs.
            if (File.Exists(s_testOutputPath))
            {
                File.Delete(s_testOutputPath);
            }

            // Have script create file first with `>` (but don't rely on overwriting).
            // NOTE: We uses double quotes so that we can use PowerShell variables.
            StringBuilder builder = new StringBuilder()
                .Append("Write-Output \"")
                .Append(logStatements[0])
                .Append("\" > '")
                .Append(s_testOutputPath)
                .AppendLine("'");

            for (int i = 1; i < logStatements.Length; i++)
            {
                // Then append to that script with `>>`.
                builder
                    .Append("Write-Output \"")
                    .Append(logStatements[i])
                    .Append("\" >> '")
                    .Append(s_testOutputPath)
                    .AppendLine("'");
            }

            _output.WriteLine("Script is:");
            _output.WriteLine(builder.ToString());
            return builder.ToString();
        }

        private static async Task<string[]> GetLog()
        {
            for (int i = 0; !File.Exists(s_testOutputPath) && i < 60; i++)
            {
                await Task.Delay(1000);
            }
            // Sleep one more time after the file exists so whatever is writing can finish.
            await Task.Delay(1000);
            return File.ReadLines(s_testOutputPath).ToArray();
        }

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

        [Fact]
        public async Task UsesDotSourceOperatorAndQuotesAsync()
        {
            string filePath = NewTestFile(GenerateScriptFromLoggingStatements("$($MyInvocation.Line)"));
            await PsesDebugAdapterClient.LaunchScript(filePath, Started);
            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);
            Assert.Collection(await GetLog(),
                (i) => Assert.StartsWith(". '", i));
        }

        [Fact]
        public async Task CanLaunchScriptWithNoBreakpointsAsync()
        {
            string filePath = NewTestFile(GenerateScriptFromLoggingStatements("works"));

            await PsesDebugAdapterClient.LaunchScript(filePath, Started);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);
            Assert.Collection(await GetLog(),
                (i) => Assert.Equal("works", i));
        }

        [SkippableFact]
        public async Task CanSetBreakpointsAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode,
                "Breakpoints can't be set in Constrained Language Mode.");

            string filePath = NewTestFile(GenerateScriptFromLoggingStatements(
                "before breakpoint",
                "at breakpoint",
                "after breakpoint"
            ));

            await PsesDebugAdapterClient.LaunchScript(filePath, Started);

            // {"command":"setBreakpoints","arguments":{"source":{"name":"dfsdfg.ps1","path":"/Users/tyleonha/Code/PowerShell/Misc/foo/dfsdfg.ps1"},"lines":[2],"breakpoints":[{"line":2}],"sourceModified":false},"type":"request","seq":3}
            SetBreakpointsResponse setBreakpointsResponse = await PsesDebugAdapterClient.SetBreakpoints(new SetBreakpointsArguments
            {
                Source = new Source { Name = Path.GetFileName(filePath), Path = filePath },
                Breakpoints = new SourceBreakpoint[] { new SourceBreakpoint { Line = 2 } },
                SourceModified = false,
            });

            Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
            Assert.True(breakpoint.Verified);
            Assert.Equal(filePath, breakpoint.Source.Path, ignoreCase: s_isWindows);
            Assert.Equal(2, breakpoint.Line);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);
            Assert.Collection(await GetLog(),
                (i) => Assert.Equal("before breakpoint", i));
            File.Delete(s_testOutputPath);

            ContinueResponse continueResponse = await PsesDebugAdapterClient.RequestContinue(
                new ContinueArguments { ThreadId = 1 });

            Assert.NotNull(continueResponse);
            Assert.Collection(await GetLog(),
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
        [SkippableFact]
        public async Task CanStepPastSystemWindowsForms()
        {
            Skip.IfNot(PsesStdioProcess.IsWindowsPowerShell,
                "Windows Forms requires Windows PowerShell.");
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode,
                "Breakpoints can't be set in Constrained Language Mode.");

            string filePath = NewTestFile(string.Join(Environment.NewLine, new[]
                {
                    "Add-Type -AssemblyName System.Windows.Forms",
                    "$global:form = New-Object System.Windows.Forms.Form",
                    "Write-Host $form"
                }));

            await PsesDebugAdapterClient.LaunchScript(filePath, Started);

            SetFunctionBreakpointsResponse setBreakpointsResponse = await PsesDebugAdapterClient.SetFunctionBreakpoints(
                new SetFunctionBreakpointsArguments
                {
                    Breakpoints = new FunctionBreakpoint[]
                        { new FunctionBreakpoint { Name = "Write-Host", } }
                });

            Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
            Assert.True(breakpoint.Verified);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);
            await Task.Delay(5000);

            VariablesResponse variablesResponse = await PsesDebugAdapterClient.RequestVariables(
                new VariablesArguments { VariablesReference = 1 });

            Variable form = variablesResponse.Variables.FirstOrDefault(v => v.Name == "$form");
            Assert.NotNull(form);
            Assert.Equal("System.Windows.Forms.Form, Text: ", form.Value);
        }

        // This tests the edge-case where a raw script (or an untitled script) has the last line
        // commented. Since in some cases (such as Windows PowerShell, or the script not having a
        // backing ScriptFile) we just wrap the script with braces, we had a bug where the last
        // brace would be after the comment. We had to ensure we wrapped with newlines instead.
        [Fact]
        public async Task CanLaunchScriptWithCommentedLastLineAsync()
        {
            string script = GenerateScriptFromLoggingStatements("$($MyInvocation.Line)") + "# a comment at the end";
            Assert.EndsWith(Environment.NewLine + "# a comment at the end", script);

            // NOTE: This is horribly complicated, but the "script" parameter here is assigned to
            // PsesLaunchRequestArguments.Script, which is then assigned to
            // DebugStateService.ScriptToLaunch in that handler, and finally used by the
            // ConfigurationDoneHandler in LaunchScriptAsync.
            await PsesDebugAdapterClient.LaunchScript(script, Started);

            ConfigurationDoneResponse configDoneResponse = await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);
            // We can check that the script was invoked as expected, which is to dot-source a script
            // block with the contents surrounded by newlines. While we can't check that the last
            // line was a curly brace by itself, we did check that the contents ended with a
            // comment, so if this output exists then the bug did not recur.
            Assert.Collection(await GetLog(),
                (i) => Assert.Equal(". {", i),
                (i) => Assert.Equal("", i));
        }

        [SkippableFact]
        public async Task CanRunPesterTestFile()
        {
            Skip.If(s_isWindows, "Windows CI Pester is broken.");
            /* TODO: Get this to work on Windows.
            string pesterLog = Path.Combine(s_binDir, Path.GetRandomFileName() + ".log");

            string testCommand = @"
                Start-Transcript -Path '" + pesterLog + @"'
                Install-Module -Name Pester -RequiredVersion 5.3.3 -Force -PassThru | Write-Host
                Import-Module -Name Pester -RequiredVersion 5.3.3 -PassThru | Write-Host
                Get-Content '" + pesterTest + @"'
                Stop-Transcript";

            using CancellationTokenSource cts = new(5000);
            while (!File.Exists(pesterLog) && !cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }
            await Task.Delay(15000);
            _output.WriteLine(File.ReadAllText(pesterLog));
            */

            string pesterTest = NewTestFile(@"
                Describe 'A' {
                    Context 'B' {
                        It 'C' {
                            { throw 'error' } | Should -Throw
                        }
                        It 'D' {
                            " + GenerateScriptFromLoggingStatements("pester") + @"
                        }
                    }
                }", isPester: true);

            await PsesDebugAdapterClient.LaunchScript($"Invoke-Pester -Script '{pesterTest}'", Started);
            await PsesDebugAdapterClient.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.Collection(await GetLog(), (i) => Assert.Equal("pester", i));
        }
    }
}
