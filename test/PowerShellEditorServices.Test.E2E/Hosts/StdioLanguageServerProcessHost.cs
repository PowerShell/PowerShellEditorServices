// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PowerShellEditorServices.Test.E2E;

/// <summary>
/// Hosts a language server process that communicates over stdio
/// </summary>
internal class StdioLanguageServerProcessHost(string fileName, IEnumerable<string> argumentList) : IAsyncLanguageServerHost
{
    // The PSES process that will be started and managed
    private readonly Process process = new()
    {
        EnableRaisingEvents = true,
        StartInfo = new ProcessStartInfo(fileName, argumentList)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };

    // Track the state of the startup
    private TaskCompletionSource<(StreamReader, StreamWriter)>? startTcs;
    private TaskCompletionSource<bool>? stopTcs;

    // Starts the process. Returns when the process has started and streams are available.
    public async Task<(StreamReader, StreamWriter)> Start(CancellationToken token = default)
    {
        // Runs this once upon process exit to clean up the state.
        EventHandler? exitHandler = null;
        exitHandler = (sender, e) =>
        {
            // Complete the stopTcs task when the process finally exits, allowing stop to complete
            stopTcs?.TrySetResult(true);
            stopTcs = null;
            startTcs = null;
            process.Exited -= exitHandler;
        };
        process.Exited += exitHandler;

        if (stopTcs is not null)
        {
            throw new InvalidOperationException("The process is currently stopping and cannot be started.");
        }

        // Await the existing task if we have already started, making this operation idempotent
        if (startTcs is not null)
        {
            return await startTcs.Task;
        }

        // Initiate a new startTcs to track the startup
        startTcs = new();

        token.ThrowIfCancellationRequested();

        // Should throw if there are any startup problems such as invalid path, etc.
        process.Start();

        // According to the source the streams should be allocated synchronously after the process has started, however it's not super clear so we will put this here in case there is an explicit race condition.
        if (process.StandardInput.BaseStream is null || process.StandardOutput.BaseStream is null)
        {
            throw new InvalidOperationException("The process has started but the StandardInput or StandardOutput streams are not available. This should never happen and is probably a race condition, please report it to PowerShellEditorServices.");
        }

        startTcs.SetResult((
            process.StandardOutput,
            process.StandardInput
        ));

        // Return the result of the completion task
        return await startTcs.Task;
    }

    public async Task WaitForExit(CancellationToken token = default)
    {
        AssertStarting();
        await process.WaitForExitAsync(token);
    }

    /// <summary>
    /// Determines if the process is in the starting state and throws if not.
    /// </summary>
    private void AssertStarting()
    {
        if (startTcs is null)
        {
            throw new InvalidOperationException("The process is not starting/started, use Start() first.");
        }
    }

    public async Task<bool> Stop(CancellationToken token = default)
    {
        AssertStarting();
        if (stopTcs is not null)
        {
            return await stopTcs.Task;
        }
        stopTcs = new();
        token.ThrowIfCancellationRequested();
        process.Kill();
        await process.WaitForExitAsync(token);
        return true;
    }
}
