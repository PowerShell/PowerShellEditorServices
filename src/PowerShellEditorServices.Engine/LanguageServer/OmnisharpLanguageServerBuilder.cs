using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Engine
{
    public class OmnisharpLanguageServerBuilder : ILanguageServerBuilder
    {
        public OmnisharpLanguageServerBuilder(IServiceCollection serviceCollection)
        {
            Services = serviceCollection;
        }

        public string NamedPipeName { get; set; }

        public string OutNamedPipeName { get; set; }

        public ILoggerFactory LoggerFactory { get; set; } = new LoggerFactory();

        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Trace;

        public IServiceCollection Services { get; }

        public ILanguageServerBuilder AddHandler<THandler>(THandler handler)
        {
            throw new System.NotImplementedException();
        }

        public ILanguageServerBuilder AddService<TService>(TService service)
        {
            throw new System.NotImplementedException();
        }

        public ILanguageServer BuildLanguageServer()
        {
            var config = new OmnisharpLanguageServer.Configuration()
            {
                LoggerFactory = LoggerFactory,
                MinimumLogLevel = MinimumLogLevel,
                NamedPipeName = NamedPipeName,
                OutNamedPipeName = OutNamedPipeName,
                Services = Services
            };

            return new OmnisharpLanguageServer(config);
}
    }
