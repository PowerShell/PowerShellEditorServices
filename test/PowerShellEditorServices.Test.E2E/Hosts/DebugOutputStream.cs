// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System.Diagnostics;
using System.IO;
using System.Text;
using Nerdbank.Streams;

namespace PowerShellEditorServices.Test.E2E;

/// <summary>
/// A stream that logs all data read and written to the debug stream which is visible in the debug console when a
/// debugger is attached.
/// </summary>
internal class DebugOutputStream : MonitoringStream
{
    public DebugOutputStream(Stream? underlyingStream)
    : base(underlyingStream ?? new MemoryStream())
    {
        DidRead += (_, segment) =>
        {
            if (segment.Array is null) { return; }
            LogData("⬅️", segment.Array, segment.Offset, segment.Count);
        };

        DidWrite += (_, segment) =>
        {
            if (segment.Array is null) { return; }
            LogData("➡️", segment.Array, segment.Offset, segment.Count);
        };
    }

    private static void LogData(string header, byte[] buffer, int offset, int count)
    {
        // If debugging, the raw traffic will be visible in the debug console
        if (Debugger.IsAttached)
        {
            string data = Encoding.UTF8.GetString(buffer, offset, count);
            Debug.WriteLine($"{header} {data}");
        }
    }
}
