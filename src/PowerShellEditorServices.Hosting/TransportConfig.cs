using System;
using System.IO;
using System.IO.Pipes;

#if !CoreCLR
using System.Security.AccessControl;
using System.Security.Principal;
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
        (Stream inStream, Stream outStream) CreateInOutStreams();

        TransportType TransportType { get; }

        string Endpoint { get; }

        void Validate();
    }

    public class StdioTransportConfig : ITransportConfig
    {
        public TransportType TransportType => TransportType.Stdio;

        public string Endpoint => "<stdio>";

        public (Stream inStream, Stream outStream) CreateInOutStreams()
        {
            return (Console.OpenStandardInput(), Console.OpenStandardOutput());
        }

        public void Validate()
        {
        }
    }

    public abstract class NamedPipeTransportConfig : ITransportConfig
    {
        private const int PipeBufferSize = 1024;

        public TransportType TransportType => TransportType.NamedPipe;

        public abstract string Endpoint { get; }

        public abstract (Stream inStream, Stream outStream) CreateInOutStreams();
        public abstract void Validate();

        protected NamedPipeServerStream CreateNamedPipe(string pipeName, PipeDirection pipeDirection, PipeOptions extraPipeOptions = PipeOptions.None)
        {
#if CoreCLR
            return new NamedPipeServerStream(
                pipeName: pipeName,
                direction: pipeDirection,
                maxNumberOfServerInstances: 1,
                transmissionMode: PipeTransmissionMode.Byte,
                options: PipeOptions.CurrentUserOnly | extraPipeOptions);
#else

            var pipeSecurity = new PipeSecurity();

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Allow the Administrators group full access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null).Translate(typeof(NTAccount)),
                    PipeAccessRights.FullControl, AccessControlType.Allow));
            }
            else
            {
                // Allow the current user read/write access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    PipeAccessRights.ReadWrite, AccessControlType.Allow));
            }

            return new NamedPipeServerStream(
                pipeName,
                pipeDirection,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | extraPipeOptions,
                inBufferSize: PipeBufferSize,
                outBufferSize: PipeBufferSize,
                pipeSecurity);
#endif
        }
    }

    public class DuplexNamedPipeTransportConfig : NamedPipeTransportConfig
    {
        private readonly string _pipeName;

        public DuplexNamedPipeTransportConfig(string pipeName)
        {
            _pipeName = pipeName;
        }
        public override string Endpoint => $"InOut pipe: {_pipeName}";

        public override (Stream inStream, Stream outStream) CreateInOutStreams()
        {
            var extraPipeOptions = PipeOptions.None;
#if CoreCLR
            extraPipeOptions |= PipeOptions.Asynchronous;
#endif

            NamedPipeServerStream namedPipe = CreateNamedPipe(_pipeName, PipeDirection.InOut, extraPipeOptions);
            return (namedPipe, namedPipe);
        }

        public override void Validate()
        {
        }
    }

    public class SimplexNamedPipeTransportConfig : NamedPipeTransportConfig
    {
        private readonly string _inPipeName;
        private readonly string _outPipeName;

        public SimplexNamedPipeTransportConfig(string inPipeName, string outPipeName)
        {
            _inPipeName = inPipeName;
            _outPipeName = outPipeName;
        }

        public override string Endpoint => $"In pipe: {_inPipeName} Out pipe: {_outPipeName}";

        public override (Stream inStream, Stream outStream) CreateInOutStreams()
        {
            var extraInPipeOptions = PipeOptions.None;
#if CoreCLR
            extraInPipeOptions |= PipeOptions.Asynchronous;
#endif
            NamedPipeServerStream inPipe = CreateNamedPipe(_inPipeName, PipeDirection.InOut, extraInPipeOptions);
            NamedPipeServerStream outPipe = CreateNamedPipe(_outPipeName, PipeDirection.Out);

            return (inPipe, outPipe);
        }

        public override void Validate()
        {
        }
    }
}
