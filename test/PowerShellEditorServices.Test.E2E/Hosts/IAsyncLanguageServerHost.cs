// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PowerShellEditorServices.Test.E2E;

/// <summary>
/// Represents a debug adapter server host that can be started and stopped and provides streams for communication.
/// </summary>
public interface IAsyncLanguageServerHost : IAsyncDisposable
{
    // Start the host and return when the host is ready to communicate. It should return a tuple of a stream Reader and stream Writer for communication with the LSP. The underlying streams can be retrieved via baseStream propertyif needed.
    Task<(StreamReader, StreamWriter)> Start(CancellationToken token = default);
    // Stops the host and returns when the host has fully stopped. It should be idempotent, such that if called while the host is already stopping/stopped, it will have the same result
    Task<bool> Stop(CancellationToken token = default);

    // Optional to implement if more is required than a simple stop
    async ValueTask IAsyncDisposable.DisposeAsync() => await Stop();
}
