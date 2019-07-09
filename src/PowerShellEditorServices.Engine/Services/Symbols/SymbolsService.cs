using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Symbols;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a high-level service for performing code completion and
    /// navigation operations on PowerShell scripts.
    /// </summary>
    public class SymbolsService
    {
        #region Private Fields

        const int DefaultWaitTimeoutMilliseconds = 5000;

        private readonly ILogger _logger;

        private readonly IDocumentSymbolProvider[] _documentSymbolProviders;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of the SymbolsService class and uses
        /// the given Runspace to execute language service operations.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext in which language service operations will be executed.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public SymbolsService(
            ILoggerFactory factory)
        {
            _logger = factory.CreateLogger<SymbolsService>();
            _documentSymbolProviders = new IDocumentSymbolProvider[]
            {
                new ScriptDocumentSymbolProvider(VersionUtils.PSVersion),
                new PsdDocumentSymbolProvider(),
                new PesterDocumentSymbolProvider()
            };
        }

        #endregion



        /// <summary>
        /// Finds all the symbols in a file.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the symbol can be located.</param>
        /// <returns></returns>
        public List<SymbolReference> FindSymbolsInFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            var foundOccurrences = new List<SymbolReference>();
            foreach (IDocumentSymbolProvider symbolProvider in _documentSymbolProviders)
            {
                foreach (SymbolReference reference in symbolProvider.ProvideDocumentSymbols(scriptFile))
                {
                    reference.SourceLine = scriptFile.GetLine(reference.ScriptRegion.StartLineNumber);
                    reference.FilePath = scriptFile.FilePath;
                    foundOccurrences.Add(reference);
                }
            }

            return foundOccurrences;
        }
    }
}
