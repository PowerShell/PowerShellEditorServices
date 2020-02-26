//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Configuration specifying an editor services protocol transport stream configuration.
    /// </summary>
    public interface ITransportConfig
    {
        /// <summary>
        /// Create, connect and return the configured transport streams.
        /// </summary>
        /// <returns>The connected transport streams. inStream and outStream may be the same stream for duplex streams.</returns>
        Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync();

        /// <summary>
        /// The name of the transport endpoint for logging.
        /// </summary>
        string EndpointDetails { get; }

        /// <summary>
        /// The name of the transport to record in the session file.
        /// </summary>
        string SessionFileTransportName { get; }

        /// <summary>
        /// Extra entries to record in the session file.
        /// </summary>
        IReadOnlyDictionary<string, object> SessionFileEntries { get; }
    }

    /// <summary>
    /// Configuration for the standard input/output transport.
    /// </summary>
    public sealed class StdioTransportConfig : ITransportConfig
    {
        private readonly HostLogger _logger;

        public StdioTransportConfig(HostLogger logger)
        {
            _logger = logger;
        }

        public string EndpointDetails => "<stdio>";

        public string SessionFileTransportName => "Stdio";

        public IReadOnlyDictionary<string, object> SessionFileEntries { get; } = null;

        public Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Connecting stdio streams");
            return Task.FromResult((Console.OpenStandardInput(), Console.OpenStandardOutput()));
        }
    }

    /// <summary>
    /// Configuration for a full duplex named pipe.
    /// </summary>
    public sealed class DuplexNamedPipeTransportConfig : ITransportConfig
    {
        /// <summary>
        /// Create a duplex named pipe transport config with an automatically generated pipe name.
        /// </summary>
        /// <returns>A new duplex named pipe transport configuration.</returns>
        public static DuplexNamedPipeTransportConfig Create(HostLogger logger)
        {
            return new DuplexNamedPipeTransportConfig(logger, NamedPipeUtils.GenerateValidNamedPipeName());
        }

        /// <summary>
        /// Create a duplex named pipe transport config with the given pipe name.
        /// </summary>
        /// <returns>A new duplex named pipe transport configuration.</returns>
        public static DuplexNamedPipeTransportConfig Create(HostLogger logger, string pipeName)
        {
            if (pipeName == null)
            {
                return DuplexNamedPipeTransportConfig.Create(logger);
            }

            return new DuplexNamedPipeTransportConfig(logger, pipeName);
        }

        private readonly HostLogger _logger;

        private readonly string _pipeName;

        private DuplexNamedPipeTransportConfig(HostLogger logger, string pipeName)
        {
            _logger = logger;
            _pipeName = pipeName;
            SessionFileEntries = new Dictionary<string, object>{ { "PipeName", NamedPipeUtils.GetNamedPipePath(pipeName) } };
        }

        public string EndpointDetails => $"InOut pipe: {_pipeName}";

        public string SessionFileTransportName => "NamedPipe";

        public IReadOnlyDictionary<string, object> SessionFileEntries { get; }

        public async Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Creating named pipe");
            NamedPipeServerStream namedPipe = NamedPipeUtils.CreateNamedPipe(_pipeName, PipeDirection.InOut);
            _logger.Log(PsesLogLevel.Diagnostic, "Waiting for named pipe connection");
            await namedPipe.WaitForConnectionAsync().ConfigureAwait(false);
            _logger.Log(PsesLogLevel.Diagnostic, "Named pipe connected");
            return (namedPipe, namedPipe);
        }
    }

    /// <summary>
    /// Configuration for two simplex named pipes.
    /// </summary>
    public sealed class SimplexNamedPipeTransportConfig : ITransportConfig
    {
        private const string InPipePrefix = "in";
        private const string OutPipePrefix = "out";

        /// <summary>
        /// Create a pair of simplex named pipes using generated names.
        /// </summary>
        /// <returns>A new simplex named pipe transport config.</returns>
        public static SimplexNamedPipeTransportConfig Create(HostLogger logger)
        {
            return SimplexNamedPipeTransportConfig.Create(logger, NamedPipeUtils.GenerateValidNamedPipeName(new[] { InPipePrefix, OutPipePrefix }));
        }

        /// <summary>
        /// Create a pair of simplex named pipes using the given name as a base.
        /// </summary>
        /// <returns>A new simplex named pipe transport config.</returns>
        public static SimplexNamedPipeTransportConfig Create(HostLogger logger, string pipeNameBase)
        {
            if (pipeNameBase == null)
            {
                return SimplexNamedPipeTransportConfig.Create(logger);
            }

            string inPipeName = $"{InPipePrefix}_{pipeNameBase}";
            string outPipeName = $"{OutPipePrefix}_{pipeNameBase}";

            return SimplexNamedPipeTransportConfig.Create(logger, inPipeName, outPipeName);
        }

        /// <summary>
        /// Create a pair of simplex named pipes using the given names.
        /// </summary>
        /// <returns>A new simplex named pipe transport config.</returns>
        public static SimplexNamedPipeTransportConfig Create(HostLogger logger, string inPipeName, string outPipeName)
        {
            return new SimplexNamedPipeTransportConfig(logger, inPipeName, outPipeName);
        }

        private readonly HostLogger _logger;
        private readonly string _inPipeName;
        private readonly string _outPipeName;

        private SimplexNamedPipeTransportConfig(HostLogger logger, string inPipeName, string outPipeName)
        {
            _logger = logger;
            _inPipeName = inPipeName;
            _outPipeName = outPipeName;

            SessionFileEntries = new Dictionary<string, object>
            {
                { "ReadPipeName", NamedPipeUtils.GetNamedPipePath(inPipeName) },
                { "WritePipeName", NamedPipeUtils.GetNamedPipePath(outPipeName) },
            };
        }

        public string EndpointDetails => $"In pipe: {_inPipeName} Out pipe: {_outPipeName}";

        public string SessionFileTransportName => "NamedPipeSimplex";

        public IReadOnlyDictionary<string, object> SessionFileEntries { get; }

        public async Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Starting in pipe connection");
            NamedPipeServerStream inPipe = NamedPipeUtils.CreateNamedPipe(_inPipeName, PipeDirection.InOut);
            Task inPipeConnected = inPipe.WaitForConnectionAsync();

            _logger.Log(PsesLogLevel.Diagnostic, "Starting out pipe connection");
            NamedPipeServerStream outPipe = NamedPipeUtils.CreateNamedPipe(_outPipeName, PipeDirection.Out);
            Task outPipeConnected = outPipe.WaitForConnectionAsync();

            _logger.Log(PsesLogLevel.Diagnostic, "Wating for pipe connections");
            await Task.WhenAll(inPipeConnected, outPipeConnected).ConfigureAwait(false);

            _logger.Log(PsesLogLevel.Diagnostic, "Simplex named pipe transport connected");
            return (inPipe, outPipe);
        }
    }
}
