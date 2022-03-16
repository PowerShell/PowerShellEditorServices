// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PowerShellEditorServices.Test.E2E
{
    internal class LoggingStream : Stream
    {
        private static readonly string s_banner = new('=', 20);

        private readonly Stream _underlyingStream;

        public LoggingStream(Stream underlyingStream) => _underlyingStream = underlyingStream;

        public override bool CanRead => _underlyingStream.CanRead;

        public override bool CanSeek => _underlyingStream.CanSeek;

        public override bool CanWrite => _underlyingStream.CanWrite;

        public override long Length => _underlyingStream.Length;

        public override long Position { get => _underlyingStream.Position; set => _underlyingStream.Position = value; }

        public override void Flush() => _underlyingStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int actualCount = _underlyingStream.Read(buffer, offset, count);
            LogData("READ", buffer, offset, actualCount);
            return actualCount;
        }

        public override long Seek(long offset, SeekOrigin origin) => _underlyingStream.Seek(offset, origin);

        public override void SetLength(long value) => _underlyingStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            LogData("WRITE", buffer, offset, count);
            _underlyingStream.Write(buffer, offset, count);
        }

        private static void LogData(string header, byte[] buffer, int offset, int count)
        {
            Debug.WriteLine($"{header} |{s_banner.Substring(0, Math.Max(s_banner.Length - header.Length - 2, 0))}");
            string data = Encoding.UTF8.GetString(buffer, offset, count);
            Debug.WriteLine(data);
            Debug.WriteLine(s_banner);
            Debug.WriteLine("\n");
        }
    }
}
