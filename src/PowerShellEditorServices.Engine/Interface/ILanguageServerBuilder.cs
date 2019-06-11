using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Engine
{
    public interface ILanguageServerBuilder
    {
        string NamedPipeName { get; set; }

        string OutNamedPipeName { get; set; }

        ILoggerFactory LoggerFactory { get; set; }

        LogLevel MinimumLogLevel { get; set; }

        ILanguageServer BuildLanguageServer();
    }
}
