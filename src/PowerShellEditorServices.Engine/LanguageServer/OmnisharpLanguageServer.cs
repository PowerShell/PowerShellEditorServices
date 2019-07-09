using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OS = OmniSharp.Extensions.LanguageServer.Server;
using System.Security.AccessControl;
using OmniSharp.Extensions.LanguageServer.Server;
using PowerShellEditorServices.Engine.Services.Workspace.Handlers;

namespace Microsoft.PowerShell.EditorServices.Engine
{
    public class OmnisharpLanguageServer : ILanguageServer
    {
        public class Configuration
        {
            public string NamedPipeName { get; set; }

            public string OutNamedPipeName { get; set; }

            public ILoggerFactory LoggerFactory { get; set; }

            public LogLevel MinimumLogLevel { get; set; }

            public IServiceCollection Services { get; set; }
        }

        // This int will be casted to a PipeOptions enum that only exists in .NET Core 2.1 and up which is why it's not available to us in .NET Standard.
        private const int CurrentUserOnly = 0x20000000;

        // In .NET Framework, NamedPipeServerStream has a constructor that takes in a PipeSecurity object. We will use reflection to call the constructor,
        // since .NET Framework doesn't have the `CurrentUserOnly` PipeOption.
        // doc: https://docs.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.-ctor?view=netframework-4.7.2#System_IO_Pipes_NamedPipeServerStream__ctor_System_String_System_IO_Pipes_PipeDirection_System_Int32_System_IO_Pipes_PipeTransmissionMode_System_IO_Pipes_PipeOptions_System_Int32_System_Int32_System_IO_Pipes_PipeSecurity_
        private static readonly ConstructorInfo s_netFrameworkPipeServerConstructor =
            typeof(NamedPipeServerStream).GetConstructor(new [] { typeof(string), typeof(PipeDirection), typeof(int), typeof(PipeTransmissionMode), typeof(PipeOptions), typeof(int), typeof(int), typeof(PipeSecurity) });

        private OS.ILanguageServer _languageServer;

        private TaskCompletionSource<bool> _serverStart;

        private readonly Configuration _configuration;

        public OmnisharpLanguageServer(
            Configuration configuration)
        {
            _configuration = configuration;
            _serverStart = new TaskCompletionSource<bool>();
        }

        public async Task StartAsync()
        {
            _languageServer = await OS.LanguageServer.From(options => {
                NamedPipeServerStream namedPipe = CreateNamedPipe(
                    _configuration.NamedPipeName,
                    _configuration.OutNamedPipeName,
                    out NamedPipeServerStream outNamedPipe);

                namedPipe.WaitForConnection();
                if (outNamedPipe != null)
                {
                    outNamedPipe.WaitForConnection();
                }

                options.Input = namedPipe;
                options.Output = outNamedPipe ?? namedPipe;

                options.LoggerFactory = _configuration.LoggerFactory;
                options.MinimumLogLevel = _configuration.MinimumLogLevel;
                options.Services = _configuration.Services;
                options.WithHandler<WorkspaceSymbolsHandler>();
            });

            _serverStart.SetResult(true);
        }

        public async Task WaitForShutdown()
        {
            await _serverStart.Task;
            await _languageServer.WaitForExit;
        }

        private static NamedPipeServerStream CreateNamedPipe(
            string inOutPipeName,
            string outPipeName,
            out NamedPipeServerStream outPipe)
        {
            // .NET Core implementation is simplest so try that first
            if (VersionUtils.IsNetCore)
            {
                outPipe = outPipeName == null
                    ? null
                    : new NamedPipeServerStream(
                        pipeName: outPipeName,
                        direction: PipeDirection.Out,
                        maxNumberOfServerInstances: 1,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: (PipeOptions)CurrentUserOnly);

                return new NamedPipeServerStream(
                    pipeName: inOutPipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous | (PipeOptions)CurrentUserOnly);
            }

            // Now deal with Windows PowerShell
            // We need to use reflection to get a nice constructor

            var pipeSecurity = new PipeSecurity();

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Allow the Administrators group full access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)),
                    PipeAccessRights.FullControl, AccessControlType.Allow));
            }
            else
            {
                // Allow the current user read/write access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    PipeAccessRights.ReadWrite, AccessControlType.Allow));
            }

            outPipe = outPipeName == null
                ? null
                : (NamedPipeServerStream)s_netFrameworkPipeServerConstructor.Invoke(
                    new object[] {
                        outPipeName,
                        PipeDirection.InOut,
                        1, // maxNumberOfServerInstances
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        1024, // inBufferSize
                        1024, // outBufferSize
                        pipeSecurity
                    });

            return (NamedPipeServerStream)s_netFrameworkPipeServerConstructor.Invoke(
                new object[] {
                    inOutPipeName,
                    PipeDirection.InOut,
                    1, // maxNumberOfServerInstances
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    1024, // inBufferSize
                    1024, // outBufferSize
                    pipeSecurity
                });
        }
    }
}
