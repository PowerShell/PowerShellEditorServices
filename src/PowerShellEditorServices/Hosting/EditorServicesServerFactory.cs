using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Server;
using Serilog;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    internal class EditorServicesServerFactory
    {
        public static EditorServicesServerFactory Create(string logPath, int minimumLogLevel)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File(logPath)
                .MinimumLevel.Verbose()
                .CreateLogger();

            ILoggerFactory loggerFactory = new LoggerFactory().AddSerilog(Log.Logger);

            return new EditorServicesServerFactory(loggerFactory, (LogLevel)minimumLogLevel);
        }

        private readonly ILoggerFactory _loggerFactory;

        private readonly Extensions.Logging.ILogger _logger;

        private readonly LogLevel _minimumLogLevel;

        public EditorServicesServerFactory(ILoggerFactory loggerFactory, LogLevel minimumLogLevel)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<EditorServicesServerFactory>();
            _minimumLogLevel = minimumLogLevel;
        }

        public PsesLanguageServer CreateLanguageServer(
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostDetails)
        {
            return new PsesLanguageServer(_loggerFactory, _minimumLogLevel, inputStream, outputStream, hostDetails);
        }

        public PsesDebugServer CreateDebugServerWithLanguageServer(Stream inputStream, Stream outputStream, PsesLanguageServer languageServer)
        {
            return PsesDebugServer.CreateWithLanguageServerServices(_loggerFactory, inputStream, outputStream, languageServer.LanguageServer.Services);
        }

        public PsesDebugServer RecreateDebugServer(Stream inputStream, Stream outputStream, PsesDebugServer debugServer)
        {
            return PsesDebugServer.CreateWithLanguageServerServices(_loggerFactory, inputStream, outputStream, debugServer.ServiceProvider);
        }

        public PsesDebugServer CreateDebugServerForTempSession(Stream inputStream, Stream outputStream, HostStartupInfo hostStartupInfo)
        {
            return PsesDebugServer.CreateForTempSession(_loggerFactory, _minimumLogLevel, inputStream, outputStream, hostStartupInfo);
        }
    }
}
