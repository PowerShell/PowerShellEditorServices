// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PowerShellEditorServices.Test.E2E
{
    /// <summary>
    ///     A <see cref="StdioServerProcess"/> is a <see cref="ServerProcess"/> that launches its server as an external process and communicates with it over STDIN / STDOUT.
    /// </summary>
    public class StdioServerProcess : ServerProcess
    {
        /// <summary>
        ///     A <see cref="ProcessStartInfo"/> that describes how to start the server.
        /// </summary>
        private readonly ProcessStartInfo _serverStartInfo;

        /// <summary>
        ///     The current server process (if any).
        /// </summary>
#pragma warning disable CA2213
        private Process _serverProcess;
#pragma warning restore CA2213

        /// <summary>
        ///     Create a new <see cref="StdioServerProcess"/>.
        /// </summary>
        /// <param name="loggerFactory">
        ///     The factory for loggers used by the process and its components.
        /// </param>
        /// <param name="serverStartInfo">
        ///     A <see cref="ProcessStartInfo"/> that describes how to start the server.
        /// </param>
        public StdioServerProcess(ILoggerFactory loggerFactory, ProcessStartInfo serverStartInfo)
            : base(loggerFactory) => _serverStartInfo = serverStartInfo ?? throw new ArgumentNullException(nameof(serverStartInfo));

        public int ProcessId => _serverProcess.Id;

        /// <summary>
        ///     The process ID of the server process, useful for attaching a debugger.
        /// </summary>
        public int Id => _serverProcess.Id;

        /// <summary>
        ///     Dispose of resources being used by the launcher.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Process serverProcess = Interlocked.Exchange(ref _serverProcess, null);
                if (serverProcess is not null)
                {
                    if (!serverProcess.HasExited)
                    {
                        serverProcess.Kill();
                    }

                    serverProcess.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        ///     Is the server running?
        /// </summary>
        public override bool IsRunning => !ServerExitCompletion.Task.IsCompleted;

        /// <summary>
        ///     The server's input stream.
        /// </summary>
        protected override Stream GetInputStream() => _serverProcess?.StandardInput?.BaseStream;

        /// <summary>
        ///     The server's output stream.
        /// </summary>
        protected override Stream GetOutputStream() => _serverProcess?.StandardOutput?.BaseStream;

        /// <summary>
        ///     Start or connect to the server.
        /// </summary>
        public override Task Start()
        {
            ServerExitCompletion = new TaskCompletionSource<object>();

            _serverStartInfo.CreateNoWindow = true;
            _serverStartInfo.UseShellExecute = false;
            _serverStartInfo.RedirectStandardInput = true;
            _serverStartInfo.RedirectStandardOutput = true;
            _serverStartInfo.RedirectStandardError = true;

            Process serverProcess = _serverProcess = new Process
            {
                StartInfo = _serverStartInfo,
                EnableRaisingEvents = true
            };
            serverProcess.Exited += ServerProcess_Exit;

            if (!serverProcess.Start())
            {
                throw new InvalidOperationException("Failed to launch language server .");
            }

            ServerStartCompletion.TrySetResult(null);

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Stop or disconnect from the server.
        /// </summary>
        public override Task Stop()
        {
            Process serverProcess = Interlocked.Exchange(ref _serverProcess, null);
            ServerExitCompletion.TrySetResult(null);
            if (serverProcess?.HasExited == false)
            {
                serverProcess.Kill();
            }
            return ServerExitCompletion.Task;
        }

        public event EventHandler<ProcessExitedEventArgs> ProcessExited;

        /// <summary>
        ///     Called when the server process has exited.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="args">
        ///     The event arguments.
        /// </param>
        private void ServerProcess_Exit(object sender, EventArgs args)
        {
            Log.LogDebug("Server process has exited.");

            Process serverProcess = (Process)sender;

            int exitCode = serverProcess.ExitCode;
            string errorMsg = serverProcess.StandardError.ReadToEnd();

            OnExited();
            ProcessExited?.Invoke(this, new ProcessExitedEventArgs(exitCode, errorMsg));
            if (exitCode != 0)
            {
                ServerExitCompletion.TrySetException(new ProcessExitedException("Stdio server process exited unexpectedly", exitCode, errorMsg));
            }
            else
            {
                ServerExitCompletion.TrySetResult(null);
            }
            ServerStartCompletion = new TaskCompletionSource<object>();
        }
    }

    public class ProcessExitedException : Exception
    {
        public ProcessExitedException(string message, int exitCode, string errorMessage)
            : base(message)
        {
            ExitCode = exitCode;
            ErrorMessage = errorMessage;
        }

        public int ExitCode { get; init; }

        public string ErrorMessage { get; init; }
    }

    public class ProcessExitedEventArgs : EventArgs
    {
        public ProcessExitedEventArgs(int exitCode, string errorMessage)
        {
            ExitCode = exitCode;
            ErrorMessage = errorMessage;
        }

        public int ExitCode { get; init; }

        public string ErrorMessage { get; init; }
    }
}
