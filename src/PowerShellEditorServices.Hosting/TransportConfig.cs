using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public enum TransportType
    {
        Stdio,
        NamedPipe,
    }

    public interface ITransportConfig
    {
        Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync();

        TransportType TransportType { get; }

        string Endpoint { get; }

        string SessionFileTransportName { get; }

        IReadOnlyDictionary<string, object> SessionFileEntries { get; }
    }

    public class StdioTransportConfig : ITransportConfig
    {
        public TransportType TransportType => TransportType.Stdio;

        public string Endpoint => "<stdio>";

        public string SessionFileTransportName => "Stdio";

        public IReadOnlyDictionary<string, object> SessionFileEntries { get; } = null;

        public Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            return Task.FromResult((Console.OpenStandardInput(), Console.OpenStandardOutput()));
        }
    }

    public class DuplexNamedPipeTransportConfig : ITransportConfig
    {
        public static DuplexNamedPipeTransportConfig Create(string pipeName)
        {
            return new DuplexNamedPipeTransportConfig(pipeName ?? NamedPipeUtils.GenerateValidNamedPipeName());
        }

        private readonly string _pipeName;

        private DuplexNamedPipeTransportConfig(string pipeName)
        {
            _pipeName = pipeName;
            SessionFileEntries = new Dictionary<string, object>{ { "PipeName", NamedPipeUtils.GetNamedPipePath(pipeName) } };
        }

        public string Endpoint => $"InOut pipe: {_pipeName}";

        public TransportType TransportType => TransportType.NamedPipe;

        public string SessionFileTransportName => "NamedPipe";

        public IReadOnlyDictionary<string, object> SessionFileEntries { get; }

        public async Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            NamedPipeServerStream namedPipe = NamedPipeUtils.CreateNamedPipe(_pipeName, PipeDirection.InOut);
            await namedPipe.WaitForConnectionAsync().ConfigureAwait(false);
            return (namedPipe, namedPipe);
        }
    }

    public class SimplexNamedPipeTransportConfig : ITransportConfig
    {
        public static SimplexNamedPipeTransportConfig Create(string pipeNameBase)
        {
            if (pipeNameBase == null)
            {
                pipeNameBase = NamedPipeUtils.GenerateValidNamedPipeName();
            }

            string inPipeName = $"in_{pipeNameBase}";
            string outPipeName = $"out_{pipeNameBase}";

            return SimplexNamedPipeTransportConfig.Create(inPipeName, outPipeName);
        }

        public static SimplexNamedPipeTransportConfig Create(string inPipeName, string outPipeName)
        {
            return new SimplexNamedPipeTransportConfig(inPipeName, outPipeName);
        }

        private readonly string _inPipeName;
        private readonly string _outPipeName;

        private SimplexNamedPipeTransportConfig(string inPipeName, string outPipeName)
        {
            _inPipeName = inPipeName;
            _outPipeName = outPipeName;

            SessionFileEntries = new Dictionary<string, object>
            {
                { "ReadPipeName", NamedPipeUtils.GetNamedPipePath(inPipeName) },
                { "WritePipeName", NamedPipeUtils.GetNamedPipePath(outPipeName) },
            };
        }

        public string Endpoint => $"In pipe: {_inPipeName} Out pipe: {_outPipeName}";

        public TransportType TransportType => TransportType.NamedPipe;

        public string SessionFileTransportName => "NamedPipeSimplex";

        public IReadOnlyDictionary<string, object> SessionFileEntries { get; }

        public async Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            NamedPipeServerStream inPipe = NamedPipeUtils.CreateNamedPipe(_inPipeName, PipeDirection.InOut);
            Task inPipeConnected = inPipe.WaitForConnectionAsync();

            NamedPipeServerStream outPipe = NamedPipeUtils.CreateNamedPipe(_outPipeName, PipeDirection.Out);
            Task outPipeConnected = outPipe.WaitForConnectionAsync();

            await Task.WhenAll(inPipeConnected, outPipeConnected).ConfigureAwait(false);

            return (inPipe, outPipe);
        }
    }
}
