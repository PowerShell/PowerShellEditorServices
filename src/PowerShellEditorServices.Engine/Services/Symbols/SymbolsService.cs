using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
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

        /// <summary>
        /// Finds the symbol in the script given a file location
        /// </summary>
        /// <param name="scriptFile">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>A SymbolReference of the symbol found at the given location
        /// or null if there is no symbol at that location
        /// </returns>
        public SymbolReference FindSymbolAtLocation(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference symbolReference =
                AstOperations.FindSymbolAtPosition(
                    scriptFile.ScriptAst,
                    lineNumber,
                    columnNumber);

            if (symbolReference != null)
            {
                symbolReference.FilePath = scriptFile.FilePath;
            }

            return symbolReference;
        }

        /// <summary>
        /// Finds all the references of a symbol
        /// </summary>
        /// <param name="foundSymbol">The symbol to find all references for</param>
        /// <param name="referencedFiles">An array of scriptFiles too search for references in</param>
        /// <param name="workspace">The workspace that will be searched for symbols</param>
        /// <returns>FindReferencesResult</returns>
        public List<SymbolReference> FindReferencesOfSymbol(
            SymbolReference foundSymbol,
            ScriptFile[] referencedFiles,
            WorkspaceService workspace)
        {
            if (foundSymbol == null)
            {
                return null;
            }

            int symbolOffset = referencedFiles[0].GetOffsetAtPosition(
                foundSymbol.ScriptRegion.StartLineNumber,
                foundSymbol.ScriptRegion.StartColumnNumber);

            // NOTE: we use to make sure aliases were loaded but took it out because we needed the pipeline thread.

            // We want to look for references first in referenced files, hence we use ordered dictionary
            // TODO: File system case-sensitivity is based on filesystem not OS, but OS is a much cheaper heuristic
            var fileMap = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? new OrderedDictionary()
                : new OrderedDictionary(StringComparer.OrdinalIgnoreCase);

            foreach (ScriptFile scriptFile in referencedFiles)
            {
                fileMap[scriptFile.FilePath] = scriptFile;
            }

            foreach (string filePath in workspace.EnumeratePSFiles())
            {
                if (!fileMap.Contains(filePath))
                {
                    if (!workspace.TryGetFile(filePath, out ScriptFile scriptFile))
                    {
                        // If we can't access the file for some reason, just ignore it
                        continue;
                    }

                    fileMap[filePath] = scriptFile;
                }
            }

            var symbolReferences = new List<SymbolReference>();
            foreach (object fileName in fileMap.Keys)
            {
                var file = (ScriptFile)fileMap[fileName];

                IEnumerable<SymbolReference> references = AstOperations.FindReferencesOfSymbol(
                    file.ScriptAst,
                    foundSymbol,
                    needsAliases: false);

                foreach (SymbolReference reference in references)
                {
                    try
                    {
                        reference.SourceLine = file.GetLine(reference.ScriptRegion.StartLineNumber);
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        reference.SourceLine = string.Empty;
                        _logger.LogException("Found reference is out of range in script file", e);
                    }
                    reference.FilePath = file.FilePath;
                    symbolReferences.Add(reference);
                }
            }

            return symbolReferences;
        }
    }
}
