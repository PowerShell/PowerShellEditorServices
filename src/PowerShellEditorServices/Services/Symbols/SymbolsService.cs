// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.CodeLenses;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.Configuration;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services
{
    /// <summary>
    /// Provides a high-level service for performing code completion and
    /// navigation operations on PowerShell scripts.
    /// </summary>
    internal class SymbolsService
    {
        #region Private Fields

        private readonly ILogger _logger;
        private readonly IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly WorkspaceService _workspaceService;

        private readonly ConcurrentDictionary<string, ICodeLensProvider> _codeLensProviders;
        private readonly ConcurrentDictionary<string, IDocumentSymbolProvider> _documentSymbolProviders;
        private readonly ConfigurationService _configurationService;
        private Task? _workspaceScanCompleted;

        #endregion Private Fields
        #region Constructors

        /// <summary>
        /// Constructs an instance of the SymbolsService class and uses
        /// the given Runspace to execute language service operations.
        /// </summary>
        /// <param name="factory">An ILoggerFactory implementation used for writing log messages.</param>
        /// <param name="runspaceContext"></param>
        /// <param name="executionService"></param>
        /// <param name="workspaceService"></param>
        /// <param name="configurationService"></param>
        public SymbolsService(
            ILoggerFactory factory,
            IRunspaceContext runspaceContext,
            IInternalPowerShellExecutionService executionService,
            WorkspaceService workspaceService,
            ConfigurationService configurationService)
        {
            _logger = factory.CreateLogger<SymbolsService>();
            _runspaceContext = runspaceContext;
            _executionService = executionService;
            _workspaceService = workspaceService;
            _configurationService = configurationService;

            _codeLensProviders = new ConcurrentDictionary<string, ICodeLensProvider>();
            if (configurationService.CurrentSettings.EnableReferencesCodeLens)
            {
                ReferencesCodeLensProvider referencesProvider = new(_workspaceService, this);
                _ = _codeLensProviders.TryAdd(referencesProvider.ProviderId, referencesProvider);
            }

            PesterCodeLensProvider pesterProvider = new(configurationService);
            _ = _codeLensProviders.TryAdd(pesterProvider.ProviderId, pesterProvider);

            // TODO: Is this complication so necessary?
            _documentSymbolProviders = new ConcurrentDictionary<string, IDocumentSymbolProvider>();
            IDocumentSymbolProvider[] documentSymbolProviders = new IDocumentSymbolProvider[]
            {
                new ScriptDocumentSymbolProvider(),
                new PsdDocumentSymbolProvider(),
                new PesterDocumentSymbolProvider(),
            };

            foreach (IDocumentSymbolProvider documentSymbolProvider in documentSymbolProviders)
            {
                _ = _documentSymbolProviders.TryAdd(documentSymbolProvider.ProviderId, documentSymbolProvider);
            }
        }

        #endregion Constructors

        public bool TryRegisterCodeLensProvider(ICodeLensProvider codeLensProvider) => _codeLensProviders.TryAdd(codeLensProvider.ProviderId, codeLensProvider);

        public bool DeregisterCodeLensProvider(string providerId) => _codeLensProviders.TryRemove(providerId, out _);

        public IEnumerable<ICodeLensProvider> GetCodeLensProviders() => _codeLensProviders.Values;

        public bool TryRegisterDocumentSymbolProvider(IDocumentSymbolProvider documentSymbolProvider) => _documentSymbolProviders.TryAdd(documentSymbolProvider.ProviderId, documentSymbolProvider);

        public bool DeregisterDocumentSymbolProvider(string providerId) => _documentSymbolProviders.TryRemove(providerId, out _);

        public IEnumerable<IDocumentSymbolProvider> GetDocumentSymbolProviders() => _documentSymbolProviders.Values;

        /// <summary>
        /// Finds all the symbols in a file, through all document symbol providers.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the symbol can be located.</param>
        public IEnumerable<SymbolReference> FindSymbolsInFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            foreach (IDocumentSymbolProvider symbolProvider in GetDocumentSymbolProviders())
            {
                foreach (SymbolReference symbol in symbolProvider.ProvideDocumentSymbols(scriptFile))
                {
                    yield return symbol;
                }
            }
        }

        /// <summary>
        /// Finds the symbol in the script given a file location.
        /// </summary>
        public static SymbolReference? FindSymbolAtLocation(
            ScriptFile scriptFile, int line, int column)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);
            return scriptFile.References.TryGetSymbolAtPosition(line, column);
        }

        // Using a private method here to get a bit more readability and to avoid roslynator
        // asserting we should use a giant nested ternary.
        private static string[] GetIdentifiers(string symbolName, SymbolType symbolType, CommandHelpers.AliasMap aliases)
        {
            if (symbolType is not SymbolType.Function)
            {
                return new[] { symbolName };
            }

            if (!aliases.CmdletToAliases.TryGetValue(symbolName, out List<string> foundAliasList))
            {
                return new[] { symbolName };
            }

            return foundAliasList.Prepend(symbolName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Finds all the references of a symbol in the workspace, resolving aliases.
        /// TODO: One day use IAsyncEnumerable.
        /// </summary>
        public async Task<IEnumerable<SymbolReference>> ScanForReferencesOfSymbolAsync(
            SymbolReference symbol,
            CancellationToken cancellationToken = default)
        {
            if (symbol is null)
            {
                return Enumerable.Empty<SymbolReference>();
            }

            // TODO: Should we handle aliases at a lower level?
            CommandHelpers.AliasMap aliases = await CommandHelpers.GetAliasesAsync(
                _executionService,
                cancellationToken).ConfigureAwait(false);

            string targetName = symbol.SymbolName;
            if (symbol.SymbolType is SymbolType.Function
                && aliases.AliasToCmdlets.TryGetValue(symbol.SymbolName, out string aliasDefinition))
            {
                targetName = aliasDefinition;
            }

            await ScanWorkspacePSFiles(cancellationToken).ConfigureAwait(false);

            List<SymbolReference> symbols = new();
            string[] allIdentifiers = GetIdentifiers(targetName, symbol.SymbolType, aliases);

            foreach (ScriptFile file in _workspaceService.GetOpenedFiles())
            {
                foreach (string targetIdentifier in allIdentifiers)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                    symbols.AddRange(file.References.TryGetReferences(symbol with { SymbolName = targetIdentifier }));
                }
            }

            return symbols;
        }

        /// <summary>
        /// Finds all the occurrences of a symbol in the script given a file location.
        /// </summary>
        public static IEnumerable<SymbolReference> FindOccurrencesInFile(
            ScriptFile scriptFile, int line, int column) => scriptFile
                .References
                .TryGetReferences(FindSymbolAtLocation(scriptFile, line, column));

        /// <summary>
        /// Finds the symbol at the location and returns it if it's a declaration.
        /// </summary>
        public static SymbolReference? FindSymbolDefinitionAtLocation(
            ScriptFile scriptFile, int line, int column)
        {
            SymbolReference? symbol = FindSymbolAtLocation(scriptFile, line, column);
            return symbol?.IsDeclaration == true ? symbol : null;
        }

        /// <summary>
        /// Finds the details of the symbol at the given script file location.
        /// </summary>
        public Task<SymbolDetails?> FindSymbolDetailsAtLocationAsync(
            ScriptFile scriptFile, int line, int column)
        {
            SymbolReference? symbol = FindSymbolAtLocation(scriptFile, line, column);
            return symbol is null
                ? Task.FromResult<SymbolDetails?>(null)
                : SymbolDetails.CreateAsync(symbol, _runspaceContext.CurrentRunspace, _executionService);
        }

        /// <summary>
        /// Finds the parameter set hints of a specific command (determined by a given file location)
        /// </summary>
        public async Task<ParameterSetSignatures?> FindParameterSetsInFileAsync(
            ScriptFile scriptFile, int line, int column)
        {
            SymbolReference? symbol = FindSymbolAtLocation(scriptFile, line, column);

            // If we are not possibly looking at a Function, we don't
            // need to continue because we won't be able to get the
            // CommandInfo object.
            if (symbol?.SymbolType is not SymbolType.Function
                and not SymbolType.Unknown)
            {
                return null;
            }

            CommandInfo commandInfo =
                await CommandHelpers.GetCommandInfoAsync(
                    symbol.SymbolName,
                    _runspaceContext.CurrentRunspace,
                    _executionService).ConfigureAwait(false);

            if (commandInfo is null)
            {
                return null;
            }

            try
            {
                IEnumerable<CommandParameterSetInfo> commandParamSets = commandInfo.ParameterSets;
                return new ParameterSetSignatures(commandParamSets, symbol);
            }
            catch (RuntimeException e)
            {
                // A RuntimeException will be thrown when an invalid attribute is
                // on a parameter binding block and then that command/script has
                // its signatures resolved by typing it into a script.
                _logger.LogException("RuntimeException encountered while accessing command parameter sets", e);

                return null;
            }
            catch (InvalidOperationException)
            {
                // For some commands there are no paramsets (like applications).  Until
                // the valid command types are better understood, catch this exception
                // which gets raised when there are no ParameterSets for the command type.
                return null;
            }
        }

        /// <summary>
        /// Finds the possible definitions of the symbol in the file or workspace.
        /// TODO: One day use IAsyncEnumerable.
        /// TODO: Fix searching for definition of built-in commands.
        /// TODO: Fix "definition" of dot-source (maybe?)
        /// </summary>
        public async Task<IEnumerable<SymbolReference>> GetDefinitionOfSymbolAsync(
            ScriptFile scriptFile,
            SymbolReference symbol,
            CancellationToken cancellationToken = default)
        {
            List<SymbolReference> declarations = new();
            declarations.AddRange(scriptFile.References.TryGetReferences(symbol).Where(i => i.IsDeclaration));
            if (declarations.Any())
            {
                _logger.LogDebug($"Found possible declaration in same file ${declarations}");
                return declarations;
            }

            IEnumerable<SymbolReference> references =
                await ScanForReferencesOfSymbolAsync(symbol, cancellationToken).ConfigureAwait(false);
            declarations.AddRange(references.Where(i => i.IsDeclaration));

            _logger.LogDebug($"Found possible declaration in workspace ${declarations}");
            return declarations;
        }

        internal async Task ScanWorkspacePSFiles(CancellationToken cancellationToken = default)
        {
            if (_configurationService.CurrentSettings.AnalyzeOpenDocumentsOnly)
            {
                return;
            }

            Task? scanTask = _workspaceScanCompleted;
            // It's not impossible for two scans to start at once but it should be exceedingly
            // unlikely, and shouldn't break anything if it happens to. So we can save some
            // lock time by accepting that possibility.
            if (scanTask is null)
            {
                scanTask = Task.Run(
                    () =>
                    {
                        foreach (string file in _workspaceService.EnumeratePSFiles())
                        {
                            if (_workspaceService.TryGetFile(file, out ScriptFile scriptFile))
                            {
                                scriptFile.References.EnsureInitialized();
                            }
                        }
                    },
                    CancellationToken.None);

                // Ignore the analyzer yelling that we're not awaiting this task, we'll get there.
#pragma warning disable CS4014
                Interlocked.CompareExchange(ref _workspaceScanCompleted, scanTask, null);
#pragma warning restore CS4014
            }

            // In the simple case where the task is already completed or the token we're given cannot
            // be cancelled, do a simple await.
            if (scanTask.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                await scanTask.ConfigureAwait(false);
                return;
            }

            // If it's not yet done and we can be cancelled, create a new task to represent the
            // cancellation. That way we can exit a request that relies on the scan without
            // having to actually stop the work (and then request it again in a few seconds).
            //
            // TODO: There's a new API in net6 that lets you await a task with a cancellation token.
            //       we should #if that in if feasible.
            TaskCompletionSource<bool> cancelled = new();
            _ = cancellationToken.Register(() => cancelled.TrySetCanceled());
            _ = await Task.WhenAny(scanTask, cancelled.Task).ConfigureAwait(false);
        }

        /// <summary>
        /// Finds a function definition that follows or contains the given line number.
        /// </summary>
        public static FunctionDefinitionAst? GetFunctionDefinitionForHelpComment(
            ScriptFile scriptFile,
            int line,
            out string? helpLocation)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);
            // check if the next line contains a function definition
            FunctionDefinitionAst? funcDefnAst = GetFunctionDefinitionAtLine(scriptFile, line + 1);
            if (funcDefnAst is not null)
            {
                helpLocation = "before";
                return funcDefnAst;
            }

            // find all the script definitions that contain the line `lineNumber`
            IEnumerable<Ast> foundAsts = scriptFile.ScriptAst.FindAll(
                ast =>
                {
                    if (ast is not FunctionDefinitionAst fdAst)
                    {
                        return false;
                    }

                    return fdAst.Body.Extent.StartLineNumber < line &&
                        fdAst.Body.Extent.EndLineNumber > line;
                },
                true);

            if (foundAsts?.Any() != true)
            {
                helpLocation = null;
                return null;
            }

            // of all the function definitions found, return the innermost function
            // definition that contains `lineNumber`
            foreach (FunctionDefinitionAst foundAst in foundAsts.Cast<FunctionDefinitionAst>())
            {
                if (funcDefnAst is null)
                {
                    funcDefnAst = foundAst;
                    continue;
                }

                if (funcDefnAst.Extent.StartOffset >= foundAst.Extent.StartOffset
                    && funcDefnAst.Extent.EndOffset <= foundAst.Extent.EndOffset)
                {
                    funcDefnAst = foundAst;
                }
            }

            // TODO: use tokens to check for non empty character instead of just checking for line offset
            if (funcDefnAst?.Body.Extent.StartLineNumber == line - 1)
            {
                helpLocation = "begin";
                return funcDefnAst;
            }

            if (funcDefnAst?.Body.Extent.EndLineNumber == line + 1)
            {
                helpLocation = "end";
                return funcDefnAst;
            }

            // If we didn't find a function definition, then return null
            helpLocation = null;
            return null;
        }

        /// <summary>
        /// Gets the function defined on a given line.
        /// TODO: Remove this.
        /// </summary>
        public static FunctionDefinitionAst? GetFunctionDefinitionAtLine(
            ScriptFile scriptFile,
            int lineNumber)
        {
            Ast functionDefinitionAst = scriptFile.ScriptAst.Find(
                ast => ast is FunctionDefinitionAst && ast.Extent.StartLineNumber == lineNumber,
                true);

            return functionDefinitionAst as FunctionDefinitionAst;
        }

        internal void OnConfigurationUpdated(object _, LanguageServerSettings e)
        {
            if (e.AnalyzeOpenDocumentsOnly)
            {
                Task? scanInProgress = _workspaceScanCompleted;
                if (scanInProgress is not null)
                {
                    // Wait until after the scan completes to close unopened files.
                    _ = scanInProgress.ContinueWith(_ => CloseUnopenedFiles(), TaskScheduler.Default);
                }
                else
                {
                    CloseUnopenedFiles();
                }

                _workspaceScanCompleted = null;

                void CloseUnopenedFiles()
                {
                    foreach (ScriptFile scriptFile in _workspaceService.GetOpenedFiles())
                    {
                        if (scriptFile.IsOpen)
                        {
                            continue;
                        }

                        _workspaceService.CloseFile(scriptFile);
                    }
                }
            }

            if (e.EnableReferencesCodeLens)
            {
                if (_codeLensProviders.ContainsKey(ReferencesCodeLensProvider.Id))
                {
                    return;
                }

                TryRegisterCodeLensProvider(new ReferencesCodeLensProvider(_workspaceService, this));
                return;
            }

            DeregisterCodeLensProvider(ReferencesCodeLensProvider.Id);
        }
    }
}
