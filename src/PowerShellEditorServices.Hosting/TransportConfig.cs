using System;
using System.IO;
using System.IO.Pipes;

#if !CoreCLR
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
#endif

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

        void Validate();
    }

    public class StdioTransportConfig : ITransportConfig
    {
        public TransportType TransportType => TransportType.Stdio;

        public string Endpoint => "<stdio>";

        public Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            return Task.FromResult((Console.OpenStandardInput(), Console.OpenStandardOutput()));
        }

        public void Validate()
        {
        }
    }

    public class DuplexNamedPipeTransportConfig : ITransportConfig
    {
        private readonly string _pipeName;

        public DuplexNamedPipeTransportConfig(string pipeName)
        {
            _pipeName = pipeName;
        }

        public string Endpoint => $"InOut pipe: {_pipeName}";

        public TransportType TransportType => TransportType.NamedPipe;

        public async Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            NamedPipeServerStream namedPipe = NamedPipeUtils.CreateNamedPipe(_pipeName, PipeDirection.InOut);
            await namedPipe.WaitForConnectionAsync().ConfigureAwait(false);
            return (namedPipe, namedPipe);
        }

        public void Validate()
        {
        }
    }

    public class SimplexNamedPipeTransportConfig : ITransportConfig
    {
        private readonly string _inPipeName;
        private readonly string _outPipeName;

        public SimplexNamedPipeTransportConfig(string inPipeName, string outPipeName)
        {
            _inPipeName = inPipeName;
            _outPipeName = outPipeName;
        }

        public string Endpoint => $"In pipe: {_inPipeName} Out pipe: {_outPipeName}";

        public TransportType TransportType => TransportType.NamedPipe;

        public async Task<(Stream inStream, Stream outStream)> ConnectStreamsAsync()
        {
            NamedPipeServerStream inPipe = NamedPipeUtils.CreateNamedPipe(_inPipeName, PipeDirection.InOut);
            Task inPipeConnected = inPipe.WaitForConnectionAsync();

            NamedPipeServerStream outPipe = NamedPipeUtils.CreateNamedPipe(_outPipeName, PipeDirection.Out);
            Task outPipeConnected = outPipe.WaitForConnectionAsync();

            await Task.WhenAll(inPipeConnected, outPipeConnected).ConfigureAwait(false);

            return (inPipe, outPipe);
        }

        public void Validate()
        {
        }
    }
}
