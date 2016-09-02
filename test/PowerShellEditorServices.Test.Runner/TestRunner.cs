//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using Xunit;
using Xunit.Runners;

namespace Microsoft.PowerShell.EditorServices.Test.Runner
{
    public class TestRunner
    {
        private int exitCode = 0;

        private object consoleLock = new object();

        private ManualResetEvent finished;

        public int RunTests(string[] assemblyFileNames)
        {
            XunitProject project = new XunitProject();
            foreach (string assemblyFileName in assemblyFileNames)
            {
                this.finished = new ManualResetEvent(false);

                var path = System.IO.Path.GetFullPath(assemblyFileName);

                using (var runner = AssemblyRunner.WithoutAppDomain(path))
                {
                    runner.OnDiagnosticMessage = OnDiagnosticMessage;
                    runner.OnErrorMessage = OnErrorMessage;
                    runner.OnTestStarting = OnTestStarting;
                    runner.OnDiscoveryComplete = OnDiscoveryComplete;
                    runner.OnExecutionComplete = OnExecutionComplete;
                    runner.OnTestFailed = OnTestFailed;
                    runner.OnTestSkipped = OnTestSkipped;

                    Console.WriteLine($"Discovering tests for path {path}...");

                    runner.Start(
                        //typeName: "LanguageServiceTests",
                        //parallel: false,
                        methodDisplay: TestMethodDisplay.ClassAndMethod);

                    this.finished.WaitOne();
                    this.finished.Dispose();
                }
            };

            return this.exitCode;
        }

        private void OnDiagnosticMessage(DiagnosticMessageInfo obj)
        {
            lock (this.consoleLock)
            {
                Console.WriteLine($"DIAGNOSTIC: {obj.Message}");
            }
        }

        private void OnTestStarting(TestStartingInfo obj)
        {
            lock (this.consoleLock)
            {
                Console.WriteLine($"Starting test {obj.TestDisplayName}...");
            }
        }

        private void OnErrorMessage(ErrorMessageInfo obj)
        {
            lock (this.consoleLock)
            {
                Console.WriteLine($"Error while running tests:\r\n\r\n{obj.ExceptionMessage}");
            }
        }

        private void OnDiscoveryComplete(DiscoveryCompleteInfo info)
        {
            lock (this.consoleLock)
            {
                Console.WriteLine($"Running {info.TestCasesToRun} of {info.TestCasesDiscovered} tests...");
            }
        }

        private void OnExecutionComplete(ExecutionCompleteInfo info)
        {
            lock (this.consoleLock)
            {
                Console.WriteLine($"Finished: {info.TotalTests} tests in {Math.Round(info.ExecutionTime, 3)}s ({info.TestsFailed} failed, {info.TestsSkipped} skipped)");
            }

            this.finished.Set();
        }

        private void OnTestFailed(TestFailedInfo info)
        {
            lock (this.consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;

                Console.WriteLine("[FAIL] {0}: {1}", info.TestDisplayName, info.ExceptionMessage);
                if (info.ExceptionStackTrace != null)
                    Console.WriteLine(info.ExceptionStackTrace);

                Console.ResetColor();
            }

            exitCode = 1;
        }

        private void OnTestSkipped(TestSkippedInfo info)
        {
            lock (this.consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[SKIP] {0}: {1}", info.TestDisplayName, info.SkipReason);
                Console.ResetColor();
            }
        }
    }
}