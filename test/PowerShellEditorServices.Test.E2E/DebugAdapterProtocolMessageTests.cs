// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PowerShell.EditorServices.Handlers;
using Nerdbank.Streams;
using OmniSharp.Extensions.DebugAdapter.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Client;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.JsonRpc.Server;
using Xunit;
using Xunit.Abstractions;
using DapStackFrame = OmniSharp.Extensions.DebugAdapter.Protocol.Models.StackFrame;

namespace PowerShellEditorServices.Test.E2E
{
    [Trait("Category", "DAP")]
    // ITestOutputHelper is injected by XUnit
    // https://xunit.net/docs/capturing-output
    public class DebugAdapterProtocolMessageTests(ITestOutputHelper output) : IAsyncLifetime
    {
        // After initialization, use this client to send messages for E2E tests and check results
        private IDebugAdapterClient client;

        private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Test scripts output here, where the output can be read to verify script progress against breakpointing
        /// </summary>
        private static readonly string testScriptLogPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        private readonly PsesStdioLanguageServerProcessHost psesHost = new(isDebugAdapter: true);

        private readonly TaskCompletionSource<IDebugAdapterClient> initializedLanguageClientTcs = new();
        /// <summary>
        /// This task is useful for waiting until the client is initialized (but before Server Initialized is sent)
        /// </summary>
        private Task<IDebugAdapterClient> initializedLanguageClient => initializedLanguageClientTcs.Task;

        /// <summary>
        /// Is used to read the script log file to verify script progress against breakpointing.
        private StreamReader scriptLogReader;

        private TaskCompletionSource<StoppedEvent> nextStoppedTcs = new();
        /// <summary>
        /// This task is useful for waiting until a breakpoint is hit in a test.
        /// </summary>
        private Task<StoppedEvent> nextStopped => nextStoppedTcs.Task;

        /// <summary>
        /// This task is useful for waiting until a StartDebuggingAttachRequest is received.
        /// </summary>
        private readonly TaskCompletionSource<StartDebuggingAttachRequestArguments> startDebuggingAttachRequestTcs = new();

        /// <summary>
        /// This task is useful for waiting until the debug session has terminated.
        /// </summary>
        private readonly TaskCompletionSource<TerminatedEvent> terminatedTcs = new();

        public async Task InitializeAsync()
        {
            // Cleanup testScriptLogPath if it exists due to an interrupted previous run
            if (File.Exists(testScriptLogPath))
            {
                File.Delete(testScriptLogPath);
            }

            (StreamReader stdout, StreamWriter stdin) = await psesHost.Start();

            // Splice the streams together and enable debug logging of all messages sent and received
            DebugOutputStream psesStream = new(
                FullDuplexStream.Splice(stdout.BaseStream, stdin.BaseStream)
            );

            /*
            PSES follows the following DAP flow:
            Receive a Initialize request
            Run Initialize handler and send response back
            Receive a Launch/Attach request
            Run Launch/Attach handler and send response back
            PSES sends the initialized event at the end of the Launch/Attach handler

            This is to spec, but the omnisharp client has a flaw where it does not complete the await until after
            Server Initialized has been received, when it should in fact return once the Client Initialize (aka
            capabilities) response is received. Per the DAP spec, we can send Launch/Attach before Server Initialized
            and PSES relies on this behavior, but if we await the standard client initialization From method, it would
            deadlock the test because it won't return until Server Initialized is received from PSES, which it won't
            send until a launch is sent.

            HACK: To get around this, we abuse the OnInitialized handler to return the client "early" via the
            `InitializedLanguageClient` once the Client Initialize response has been received.
            see https://github.com/OmniSharp/csharp-language-server-protocol/issues/1408
            */
            Task<DebugAdapterClient> dapClientInitializeTask = DebugAdapterClient.From(options =>
            {
                options
                    .WithInput(psesStream)
                    .WithOutput(psesStream)
                    // The "early" return mentioned above
                    .OnInitialized((dapClient, _, _, _) =>
                    {
                        initializedLanguageClientTcs.SetResult(dapClient);
                        return Task.CompletedTask;
                    })
                    // This TCS is useful to wait for a breakpoint to be hit
                    .OnStopped((StoppedEvent e) =>
                    {
                        TaskCompletionSource<StoppedEvent> currentStoppedTcs = nextStoppedTcs;
                        nextStoppedTcs = new();

                        currentStoppedTcs.SetResult(e);
                    })
                    .OnRequest("startDebugging", (StartDebuggingAttachRequestArguments request) =>
                    {
                        startDebuggingAttachRequestTcs.SetResult(request);
                        return Task.CompletedTask;
                    })
                    .OnTerminated((TerminatedEvent e) =>
                    {
                        terminatedTcs.SetResult(e);
                        return Task.CompletedTask;
                    })
                ;
            });

            // This ensures any unhandled exceptions get addressed if it fails to start before our early return completes.
            // Under normal operation the initializedLanguageClient will always return first.
            await Task.WhenAny(
                initializedLanguageClient,
                dapClientInitializeTask
            );

            client = await initializedLanguageClient;
        }

        public async Task DisposeAsync()
        {
            await client.RequestDisconnect(new DisconnectArguments
            {
                Restart = false,
                TerminateDebuggee = true
            });
            client?.Dispose();
            psesHost.Stop();

            scriptLogReader?.Dispose(); //Also disposes the underlying filestream
            if (File.Exists(testScriptLogPath))
            {
                File.Delete(testScriptLogPath);
            }
        }

        private static string NewTestFile(string script, bool isPester = false)
        {
            string fileExt = isPester ? ".Tests.ps1" : ".ps1";
            string filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + fileExt);
            File.WriteAllText(filePath, script);

            return filePath;
        }

        /// <summary>
        /// Given an array of strings, generate a PowerShell script that writes each string to our test script log path
        /// so it can be read back later to verify script progress against breakpointing.
        /// </summary>
        /// <param name="logStatements">A list of statements that for which a script will be generated to write each statement to a testing log that can be read by <see cref="ReadScriptLogLineAsync" />. The strings are double quoted in Powershell, so variables such as <c>$($PSScriptRoot)</c> etc. can be used</param>
        /// <returns>A script string that should be written to disk and instructed by PSES to execute</returns>
        /// <exception cref="ArgumentNullException"></exception>
        private string GenerateLoggingScript(params string[] logStatements)
        {
            if (logStatements.Length == 0)
            {
                throw new ArgumentNullException(nameof(logStatements), "Expected at least one argument.");
            }

            // Clean up side effects from other test runs.
            if (File.Exists(testScriptLogPath))
            {
                File.Delete(testScriptLogPath);
            }

            // Have script create file first with `>` (but don't rely on overwriting).
            // NOTE: We uses double quotes so that we can use PowerShell variables.
            StringBuilder builder = new StringBuilder()
                .Append("Write-Output \"")
                .Append(logStatements[0])
                .Append("\" > '")
                .Append(testScriptLogPath)
                .AppendLine("'");

            for (int i = 1; i < logStatements.Length; i++)
            {
                // Then append to that script with `>>`.
                builder
                    .Append("Write-Output \"")
                    .Append(logStatements[i])
                    .Append("\" >> '")
                    .Append(testScriptLogPath)
                    .AppendLine("'");
            }

            output.WriteLine("Script is:");
            output.WriteLine(builder.ToString());
            return builder.ToString();
        }

        /// <summary>
        /// Reads the next output line from the test script log file. Useful in assertions to verify script progress against breakpointing.
        /// </summary>
        private async Task<string> ReadScriptLogLineAsync()
        {
            while (scriptLogReader is null)
            {
                try
                {
                    scriptLogReader = new StreamReader(
                        new FileStream(
                            testScriptLogPath,
                            FileMode.OpenOrCreate,
                            FileAccess.Read, // Because we use append, its OK to create the file ahead of the script
                            FileShare.ReadWrite
                        )
                    );
                }
                catch (IOException) //Sadly there does not appear to be a xplat way to wait for file availability, but luckily this does not appear to fire often.
                {
                    await Task.Delay(500);
                }
            }

            // return valid lines only
            string nextLine = string.Empty;
            while (nextLine is null || nextLine.Length == 0)
            {
                nextLine = await scriptLogReader.ReadLineAsync(); //Might return null if at EOF because we created it above but the script hasn't written to it yet
            }
            return nextLine;
        }

        [Fact]
        public void CanInitializeWithCorrectServerSettings()
        {
            Assert.True(client.ServerSettings.SupportsConditionalBreakpoints);
            Assert.True(client.ServerSettings.SupportsConfigurationDoneRequest);
            Assert.True(client.ServerSettings.SupportsFunctionBreakpoints);
            Assert.True(client.ServerSettings.SupportsHitConditionalBreakpoints);
            Assert.True(client.ServerSettings.SupportsLogPoints);
            Assert.True(client.ServerSettings.SupportsSetVariable);
            Assert.True(client.ServerSettings.SupportsDelayedStackTraceLoading);
        }

        [Fact]
        public async Task UsesDotSourceOperatorAndQuotesAsync()
        {
            string filePath = NewTestFile(GenerateLoggingScript("$($MyInvocation.Line)"));
            await client.LaunchScript(filePath);
            ConfigurationDoneResponse configDoneResponse = await client.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);

            string actual = await ReadScriptLogLineAsync();
            Assert.StartsWith(". '", actual);
        }

        [Fact]
        public async Task UsesCallOperatorWithSettingAsync()
        {
            string filePath = NewTestFile(GenerateLoggingScript("$($MyInvocation.Line)"));
            await client.LaunchScript(filePath, executeMode: "Call");
            ConfigurationDoneResponse configDoneResponse = await client.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);

            string actual = await ReadScriptLogLineAsync();
            Assert.StartsWith("& '", actual);
        }

        [Fact]
        public async Task CanLaunchScriptWithNoBreakpointsAsync()
        {
            string filePath = NewTestFile(GenerateLoggingScript("works"));

            await client.LaunchScript(filePath);

            ConfigurationDoneResponse configDoneResponse = await client.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);

            string actual = await ReadScriptLogLineAsync();
            Assert.Equal("works", actual);
        }

        [SkippableFact]
        public async Task CanSetBreakpointsAsync()
        {
            Skip.If(PsesStdioLanguageServerProcessHost.RunningInConstrainedLanguageMode,
                "Breakpoints can't be set in Constrained Language Mode.");

            string filePath = NewTestFile(GenerateLoggingScript(
                "before breakpoint",
                "at breakpoint",
                "after breakpoint"
            ));

            await client.LaunchScript(filePath);

            // {"command":"setBreakpoints","arguments":{"source":{"name":"dfsdfg.ps1","path":"/Users/tyleonha/Code/PowerShell/Misc/foo/dfsdfg.ps1"},"lines":[2],"breakpoints":[{"line":2}],"sourceModified":false},"type":"request","seq":3}
            SetBreakpointsResponse setBreakpointsResponse = await client.SetBreakpoints(new SetBreakpointsArguments
            {
                Source = new Source { Name = Path.GetFileName(filePath), Path = filePath },
                Breakpoints = new SourceBreakpoint[] { new SourceBreakpoint { Line = 2 } },
                SourceModified = false,
            });

            Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
            Assert.True(breakpoint.Verified);
            Assert.Equal(filePath, breakpoint.Source.Path, ignoreCase: s_isWindows);
            Assert.Equal(2, breakpoint.Line);

            ConfigurationDoneResponse configDoneResponse = await client.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);

            // Wait until we hit the breakpoint
            StoppedEvent stoppedEvent = await nextStopped;
            Assert.Equal("breakpoint", stoppedEvent.Reason);

            // The code before the breakpoint should have already run
            Assert.Equal("before breakpoint", await ReadScriptLogLineAsync());

            // Assert that the stopped breakpoint is the one we set
            StackTraceResponse stackTraceResponse = await client.RequestStackTrace(new StackTraceArguments { ThreadId = 1 });
            DapStackFrame stoppedTopFrame = stackTraceResponse.StackFrames.First();
            Assert.Equal(2, stoppedTopFrame.Line);

            _ = await client.RequestContinue(new ContinueArguments { ThreadId = 1 });

            string atBreakpointActual = await ReadScriptLogLineAsync();
            Assert.Equal("at breakpoint", atBreakpointActual);

            string afterBreakpointActual = await ReadScriptLogLineAsync();
            Assert.Equal("after breakpoint", afterBreakpointActual);
        }

        [SkippableFact]
        public async Task FailsIfStacktraceRequestedWhenNotPaused()
        {
            Skip.If(PsesStdioLanguageServerProcessHost.RunningInConstrainedLanguageMode,
                "Breakpoints can't be set in Constrained Language Mode.");

            // We want a long running script that never hits the next breakpoint
            string filePath = NewTestFile(GenerateLoggingScript(
                "$(sleep 10)",
                "Should fail before we get here"
            ));

            await client.SetBreakpoints(
                new SetBreakpointsArguments
                {
                    Source = new Source { Name = Path.GetFileName(filePath), Path = filePath },
                    Breakpoints = new SourceBreakpoint[] { new SourceBreakpoint { Line = 1 } },
                    SourceModified = false,
                }
            );

            // Signal to start the script
            await client.RequestConfigurationDone(new ConfigurationDoneArguments());
            await client.LaunchScript(filePath);

            // Try to get the stacktrace, which should throw as we are not currently at a breakpoint.
            await Assert.ThrowsAsync<JsonRpcException>(() => client.RequestStackTrace(
                new StackTraceArguments { }
            ));
        }

        [SkippableFact]
        public async Task SendsInitialLabelBreakpointForPerformanceReasons()
        {
            Skip.If(PsesStdioLanguageServerProcessHost.RunningInConstrainedLanguageMode,
                "Breakpoints can't be set in Constrained Language Mode.");
            string filePath = NewTestFile(GenerateLoggingScript(
                "before breakpoint",
                "label breakpoint"
            ));

            // Request a launch. Note that per DAP spec, launch doesn't actually begin until ConfigDone finishes.
            await client.LaunchScript(filePath);

            SetBreakpointsResponse setBreakpointsResponse = await client.SetBreakpoints(new SetBreakpointsArguments
            {
                Source = new Source { Name = Path.GetFileName(filePath), Path = filePath },
                Breakpoints = new SourceBreakpoint[] { new SourceBreakpoint { Line = 2 } },
                SourceModified = false,
            });

            Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
            Assert.True(breakpoint.Verified);
            Assert.Equal(filePath, breakpoint.Source.Path, ignoreCase: s_isWindows);
            Assert.Equal(2, breakpoint.Line);

            _ = client.RequestConfigurationDone(new ConfigurationDoneArguments());

            // Wait for the breakpoint to be hit
            StoppedEvent stoppedEvent = await nextStopped;
            Assert.Equal("breakpoint", stoppedEvent.Reason);

            // The code before the breakpoint should have already run
            Assert.Equal("before breakpoint", await ReadScriptLogLineAsync());

            // Get the stacktrace for the breakpoint
            StackTraceResponse stackTraceResponse = await client.RequestStackTrace(
                new StackTraceArguments { ThreadId = 1 }
            );
            DapStackFrame firstFrame = stackTraceResponse.StackFrames.First();

            // Our synthetic label breakpoint should be present
            Assert.Equal(
                StackFramePresentationHint.Label,
                firstFrame.PresentationHint
            );
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
            Skip.IfNot(PsesStdioLanguageServerProcessHost.IsWindowsPowerShell,
                "Windows Forms requires Windows PowerShell.");
            Skip.If(PsesStdioLanguageServerProcessHost.RunningInConstrainedLanguageMode,
                "Breakpoints can't be set in Constrained Language Mode.");

            string filePath = NewTestFile(string.Join(Environment.NewLine, new[]
                {
                "Add-Type -AssemblyName System.Windows.Forms",
                "$global:form = New-Object System.Windows.Forms.Form",
                "Write-Host $form"
            }));

            await client.LaunchScript(filePath);

            SetFunctionBreakpointsResponse setBreakpointsResponse = await client.SetFunctionBreakpoints(
                new SetFunctionBreakpointsArguments
                {
                    Breakpoints = new FunctionBreakpoint[]
                        { new FunctionBreakpoint { Name = "Write-Host", } }
                });

            Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
            Assert.True(breakpoint.Verified);

            ConfigurationDoneResponse configDoneResponse = await client.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.NotNull(configDoneResponse);
            await Task.Delay(5000);

            VariablesResponse variablesResponse = await client.RequestVariables(
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
            string script = GenerateLoggingScript("$($MyInvocation.Line)", "$(1+1)") + "# a comment at the end";
            Assert.EndsWith(Environment.NewLine + "# a comment at the end", script);

            // NOTE: This is horribly complicated, but the "script" parameter here is assigned to
            // PsesLaunchRequestArguments.Script, which is then assigned to
            // DebugStateService.ScriptToLaunch in that handler, and finally used by the
            // ConfigurationDoneHandler in LaunchScriptAsync.
            await client.LaunchScript(script);

            _ = await client.RequestConfigurationDone(new ConfigurationDoneArguments());

            // We can check that the script was invoked as expected, which is to dot-source a script
            // block with the contents surrounded by newlines. While we can't check that the last
            // line was a curly brace by itself, we did check that the contents ended with a
            // comment, so if this output exists then the bug did not recur.
            Assert.Equal(". {", await ReadScriptLogLineAsync());

            // Verifies that the script did run and the body was evaluated
            Assert.Equal("2", await ReadScriptLogLineAsync());
        }

        [SkippableFact]
        public async Task CanRunPesterTestFile()
        {
            Skip.If(true, "Pester test is broken.");
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
            output.WriteLine(File.ReadAllText(pesterLog));
            */

            string pesterTest = NewTestFile(@"
            Describe 'A' {
                Context 'B' {
                    It 'C' {
                        { throw 'error' } | Should -Throw
                    }
                    It 'D' {
                        " + GenerateLoggingScript("pester") + @"
                    }
                }
            }", isPester: true);

            await client.LaunchScript($"Invoke-Pester -Script '{pesterTest}'");
            await client.RequestConfigurationDone(new ConfigurationDoneArguments());
            Assert.Equal("pester", await ReadScriptLogLineAsync());
        }

#nullable enable
        [InlineData("", null, null, 0, 0, null)]
        [InlineData("-ProcessId 1234 -RunspaceId 5678", null, null, 1234, 5678, null)]
        [InlineData("-ProcessId 1234 -RunspaceId 5678 -ComputerName comp", "comp", null, 1234, 5678, null)]
        [InlineData("-CustomPipeName testpipe -RunspaceName rs-name", null, "testpipe", 0, 0, "rs-name")]
        [SkippableTheory]
        public async Task CanLaunchScriptWithNewChildAttachSession(
            string paramString,
            string? expectedComputerName,
            string? expectedPipeName,
            int expectedProcessId,
            int expectedRunspaceId,
            string? expectedRunspaceName)
        {
            Skip.If(PsesStdioLanguageServerProcessHost.RunningInConstrainedLanguageMode,
                "PowerShellEditorServices.Command is not signed to run FLM in Constrained Language Mode.");

            string script = NewTestFile($"Start-DebugAttachSession {paramString}");

            using CancellationTokenSource timeoutCts = new(30000);
            using CancellationTokenRegistration _ = timeoutCts.Token.Register(() =>
            {
                startDebuggingAttachRequestTcs.TrySetCanceled();
            });
            using CancellationTokenRegistration _2 = timeoutCts.Token.Register(() =>
            {
                terminatedTcs.TrySetCanceled();
            });

            await client.LaunchScript(script);
            await client.RequestConfigurationDone(new ConfigurationDoneArguments());

            StartDebuggingAttachRequestArguments attachRequest = await startDebuggingAttachRequestTcs.Task;
            Assert.Equal("attach", attachRequest.Request);
            Assert.Equal(expectedComputerName, attachRequest.Configuration.ComputerName);
            Assert.Equal(expectedPipeName, attachRequest.Configuration.CustomPipeName);
            Assert.Equal(expectedProcessId, attachRequest.Configuration.ProcessId);
            Assert.Equal(expectedRunspaceId, attachRequest.Configuration.RunspaceId);
            Assert.Equal(expectedRunspaceName, attachRequest.Configuration.RunspaceName);

            await terminatedTcs.Task;
        }

        [SkippableFact]
        public async Task CanLaunchScriptWithNewChildAttachSessionAsJob()
        {
            Skip.If(PsesStdioLanguageServerProcessHost.RunningInConstrainedLanguageMode,
                "PowerShellEditorServices.Command is not signed to run FLM in Constrained Language Mode.");
            Skip.If(PsesStdioLanguageServerProcessHost.IsWindowsPowerShell,
                "WinPS does not have ThreadJob, needed by -AsJob, present by default.");

            string script = NewTestFile("Start-DebugAttachSession -AsJob | Receive-Job -Wait -AutoRemoveJob");

            using CancellationTokenSource timeoutCts = new(30000);
            using CancellationTokenRegistration _1 = timeoutCts.Token.Register(() =>
            {
                startDebuggingAttachRequestTcs.TrySetCanceled();
            });
            using CancellationTokenRegistration _2 = timeoutCts.Token.Register(() =>
            {
                terminatedTcs.TrySetCanceled();
            });

            await client.LaunchScript(script);
            await client.RequestConfigurationDone(new ConfigurationDoneArguments());

            StartDebuggingAttachRequestArguments attachRequest = await startDebuggingAttachRequestTcs.Task;
            Assert.Equal("attach", attachRequest.Request);
            Assert.Null(attachRequest.Configuration.ComputerName);
            Assert.Null(attachRequest.Configuration.CustomPipeName);
            Assert.Equal(0, attachRequest.Configuration.ProcessId);
            Assert.Equal(0, attachRequest.Configuration.RunspaceId);
            Assert.Null(attachRequest.Configuration.RunspaceName);

            await terminatedTcs.Task;
        }

        [SkippableFact]
        public async Task CanAttachScriptWithPathMappings()
        {
            Skip.If(PsesStdioLanguageServerProcessHost.RunningInConstrainedLanguageMode,
                "Breakpoints can't be set in Constrained Language Mode.");

            string[] logStatements = ["$PSCommandPath", "after breakpoint"];

            await RunWithAttachableProcess(logStatements, async (filePath, processId, runspaceId) =>
            {
                string localParent = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string localScriptPath = Path.Combine(localParent, Path.GetFileName(filePath));
                Directory.CreateDirectory(localParent);
                File.Copy(filePath, localScriptPath);

                Task<StoppedEvent> nextStoppedTask = nextStopped;

                AttachResponse attachResponse = await client.Attach(
                    new PsesAttachRequestArguments
                    {
                        ProcessId = processId,
                        RunspaceId = runspaceId,
                        PathMappings = [
                            new()
                            {
                                LocalRoot = localParent + Path.DirectorySeparatorChar,
                                RemoteRoot = Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar
                            }
                        ]
                    }) ?? throw new Exception("Attach response was null.");
                Assert.NotNull(attachResponse);

                SetBreakpointsResponse setBreakpointsResponse = await client.SetBreakpoints(new SetBreakpointsArguments
                {
                    Source = new Source { Name = Path.GetFileName(localScriptPath), Path = localScriptPath },
                    Breakpoints = new SourceBreakpoint[] { new SourceBreakpoint { Line = 2 } },
                    SourceModified = false,
                });

                Breakpoint breakpoint = setBreakpointsResponse.Breakpoints.First();
                Assert.True(breakpoint.Verified);
                Assert.NotNull(breakpoint.Source);
                Assert.Equal(localScriptPath, breakpoint.Source.Path, ignoreCase: s_isWindows);
                Assert.Equal(2, breakpoint.Line);

                ConfigurationDoneResponse configDoneResponse = await client.RequestConfigurationDone(new ConfigurationDoneArguments());
                Assert.NotNull(configDoneResponse);

                // Wait-Debugger stop
                StoppedEvent stoppedEvent = await nextStoppedTask;
                Assert.Equal("step", stoppedEvent.Reason);
                Assert.NotNull(stoppedEvent.ThreadId);

                nextStoppedTask = nextStopped;

                // It is important we wait for the stack trace before continue.
                // The stopped event starts to get the stack trace info in the
                // background and requesting the stack trace is the only way to
                // ensure it is done and won't conflict with the continue request.
                await client.RequestStackTrace(new StackTraceArguments { ThreadId = (int)stoppedEvent.ThreadId });
                await client.RequestContinue(new ContinueArguments { ThreadId = (int)stoppedEvent.ThreadId });

                // Wait until we hit the breakpoint
                stoppedEvent = await nextStoppedTask;
                Assert.Equal("breakpoint", stoppedEvent.Reason);
                Assert.NotNull(stoppedEvent.ThreadId);

                // The code before the breakpoint should have already run
                // It will contain the actual script being run
                string beforeBreakpointActual = await ReadScriptLogLineAsync();
                Assert.Equal(filePath, beforeBreakpointActual);

                // Assert that the stopped breakpoint is the one we set
                StackTraceResponse stackTraceResponse = await client.RequestStackTrace(new StackTraceArguments { ThreadId = (int)stoppedEvent.ThreadId });
                DapStackFrame? stoppedTopFrame = stackTraceResponse.StackFrames?.First();

                // The top frame should have a source path of our local script.
                Assert.NotNull(stoppedTopFrame);
                Assert.Equal(2, stoppedTopFrame.Line);
                Assert.NotNull(stoppedTopFrame.Source);
                Assert.Equal(localScriptPath, stoppedTopFrame.Source.Path, ignoreCase: s_isWindows);

                await client.RequestContinue(new ContinueArguments { ThreadId = 1 });

                string afterBreakpointActual = await ReadScriptLogLineAsync();
                Assert.Equal("after breakpoint", afterBreakpointActual);
            });
        }

        private async Task RunWithAttachableProcess(string[] logStatements, Func<string, int, int, Task> action)
        {
            /*
                There is no public API in pwsh to wait for an attach event. We
                use reflection to wait until the AvailabilityChanged event is
                subscribed to by Debug-Runspace as a marker that it is ready to
                continue.

                We also run the test script in another runspace as WinPS'
                Debug-Runspace will break on the first statement after the
                attach and we want that to be the Wait-Debugger call.

                We can use https://github.com/PowerShell/PowerShell/pull/25788
                once that is merged and we are running against that version but
                WinPS will always need this.
            */
            string scriptEntrypoint = @"
                param([string]$TestScript)

                $debugRunspaceCmd = Get-Command Debug-Runspace -Module Microsoft.PowerShell.Utility
                $runspaceBase = [PSObject].Assembly.GetType(
                    'System.Management.Automation.Runspaces.RunspaceBase')
                $availabilityChangedField = $runspaceBase.GetField(
                    'AvailabilityChanged',
                    [System.Reflection.BindingFlags]'NonPublic, Instance')
                if (-not $availabilityChangedField) {
                    throw 'Failed to get AvailabilityChanged event field'
                }

                $ps = [PowerShell]::Create()
                $runspace = $ps.Runspace

                # Wait-Debugger is needed in WinPS to sync breakpoints before
                # running the script.
                $null = $ps.AddCommand('Wait-Debugger').AddStatement()
                $null = $ps.AddCommand($TestScript)

                # Let the runner know what Runspace to attach to and that it
                # is ready to run.
                'RID: {0}' -f $runspace.Id

                $start = Get-Date
                while ($true) {
                    $subscribed = $availabilityChangedField.GetValue($runspace) |
                        Where-Object Target -is $debugRunspaceCmd.ImplementingType
                    if ($subscribed) {
                        break
                    }

                    if (((Get-Date) - $start).TotalSeconds -gt 10) {
                        throw 'Timeout waiting for Debug-Runspace to be subscribed.'
                    }
                }

                $ps.Invoke()
                foreach ($e in $ps.Streams.Error) {
                    Write-Error -ErrorRecord $e
                }

                # Keep running until the runner has deleted the test script to
                # ensure the process doesn't finish before the test does in
                # normal circumstances.
                while (Test-Path -LiteralPath $TestScript) {
                    Start-Sleep -Seconds 1
                }
            ";

            string filePath = NewTestFile(GenerateLoggingScript(logStatements));
            string encArgs = CreatePwshEncodedArgs(filePath);
            string encCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(scriptEntrypoint));

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = PsesStdioLanguageServerProcessHost.PwshExe,
                Arguments = $"-NoLogo -NoProfile -EncodedCommand {encCommand} -EncodedArguments {encArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.EnvironmentVariables["TERM"] = "dumb";  // Avoids color/VT sequences in test output.

            TaskCompletionSource<int> ridOutput = new();

            // Task shouldn't take longer than 30 seconds to complete.
            using CancellationTokenSource debugTaskCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using CancellationTokenRegistration _ = debugTaskCts.Token.Register(ridOutput.SetCanceled);
            using Process? psProc = Process.Start(psi);
            try
            {
                Assert.NotNull(psProc);
                psProc.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        if (args.Data.StartsWith("RID: "))
                        {
                            int rid = int.Parse(args.Data.Substring(5));
                            ridOutput.SetResult(rid);
                        }

                        output.WriteLine("STDOUT: {0}", args.Data);
                    }
                };
                psProc.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        output.WriteLine("STDERR: {0}", args.Data);
                    }
                };
                psProc.EnableRaisingEvents = true;
                psProc.BeginOutputReadLine();
                psProc.BeginErrorReadLine();

                Task procExited = psProc.WaitForExitAsync(debugTaskCts.Token);
                Task<int> waitRid = ridOutput.Task;

                // Wait for the process to fail or the script to start.
                Task finishedTask = await Task.WhenAny(waitRid, procExited);
                if (finishedTask == procExited)
                {
                    await procExited;
                    Assert.Fail("The attached process exited before the PowerShell entrypoint could start.");
                }
                int rid = await waitRid;

                Task debugTask = action(filePath, psProc.Id, rid);
                finishedTask = await Task.WhenAny(procExited, debugTask);
                if (finishedTask == procExited)
                {
                    await procExited;
                    Assert.Fail("Attached process exited before the script could start.");
                }

                await debugTask;

                File.Delete(filePath);
                psProc.Kill();
                await procExited;
            }
            catch
            {
                if (psProc is not null && !psProc.HasExited)
                {
                    psProc.Kill();
                }

                throw;
            }
        }

        private static string CreatePwshEncodedArgs(params string[] args)
        {
            // Only way to pass args to -EncodedCommand is to use CLIXML with
            // -EncodedArguments. Not pretty but the structure isn't too
            // complex and saves us trying to embed/escape strings in a script.
            string clixmlNamespace = "http://schemas.microsoft.com/powershell/2004/04";
            string clixml = new XDocument(
                new XDeclaration("1.0", "utf-16", "yes"),
                new XElement(XName.Get("Objs", clixmlNamespace),
                    new XAttribute("Version", "1.1.0.1"),
                    new XElement(XName.Get("Obj", clixmlNamespace),
                        new XAttribute("RefId", "0"),
                        new XElement(XName.Get("TN", clixmlNamespace),
                            new XAttribute("RefId", "0"),
                            new XElement(XName.Get("T", clixmlNamespace), "System.Collections.ArrayList"),
                            new XElement(XName.Get("T", clixmlNamespace), "System.Object")
                        ),
                        new XElement(XName.Get("LST", clixmlNamespace),
                            args.Select(s => new XElement(XName.Get("S", clixmlNamespace), s))
                        )
                ))).ToString(SaveOptions.DisableFormatting);

            return Convert.ToBase64String(Encoding.Unicode.GetBytes(clixml));
        }

        private record StartDebuggingAttachRequestArguments(PsesAttachRequestArguments Configuration, string Request);
    }
}
