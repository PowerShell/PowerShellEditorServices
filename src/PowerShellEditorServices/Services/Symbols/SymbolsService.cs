// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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
        #endregion

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
                _codeLensProviders.TryAdd(referencesProvider.ProviderId, referencesProvider);
            }

            PesterCodeLensProvider pesterProvider = new(configurationService);
            _codeLensProviders.TryAdd(pesterProvider.ProviderId, pesterProvider);

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
                _documentSymbolProviders.TryAdd(documentSymbolProvider.ProviderId, documentSymbolProvider);
            }
        }

        #endregion

        public bool TryRegisterCodeLensProvider(ICodeLensProvider codeLensProvider) => _codeLensProviders.TryAdd(codeLensProvider.ProviderId, codeLensProvider);

        public bool DeregisterCodeLensProvider(string providerId) => _codeLensProviders.TryRemove(providerId, out _);

        public IEnumerable<ICodeLensProvider> GetCodeLensProviders() => _codeLensProviders.Values;

        public bool TryRegisterDocumentSymbolProvider(IDocumentSymbolProvider documentSymbolProvider) => _documentSymbolProviders.TryAdd(documentSymbolProvider.ProviderId, documentSymbolProvider);

        public bool DeregisterDocumentSymbolProvider(string providerId) => _documentSymbolProviders.TryRemove(providerId, out _);

        public IEnumerable<IDocumentSymbolProvider> GetDocumentSymbolProviders() => _documentSymbolProviders.Values;

        /// <summary>
        /// Finds all the symbols in a file.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the symbol can be located.</param>
        public List<SymbolReference> FindSymbolsInFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            List<SymbolReference> symbols = new();
            foreach (IDocumentSymbolProvider symbolProvider in GetDocumentSymbolProviders())
            {
                // TODO: Each provider needs to set the source line and filepath.
                symbols.AddRange(symbolProvider.ProvideDocumentSymbols(scriptFile));
            }

            return symbols;
        }

        /// <summary>
        /// Finds the symbol in the script given a file location
        /// </summary>
        /// <param name="scriptFile">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The column number of the cursor for the given script</param>
        /// <returns>A SymbolReference of the symbol found at the given location
        /// or null if there is no symbol at that location
        /// </returns>
        public static SymbolReference FindSymbolAtLocation(
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
        /// <param name="referencedFiles">An array of scriptFiles to search for references in</param>
        /// <param name="cancellationToken"></param>
        /// <returns>FindReferencesResult</returns>
        public async Task<IEnumerable<SymbolReference>> ScanForReferencesOfSymbol(
            SymbolReference foundSymbol,
            ScriptFile[] referencedFiles,
            CancellationToken cancellationToken = default)
        {
            if (foundSymbol == null)
            {
                return null;
            }

            // TODO: Should we handle aliases at a lower level?
            CommandHelpers.AliasMap aliases = await CommandHelpers.GetAliasesAsync(
                _executionService,
                cancellationToken).ConfigureAwait(false);

            string targetName = foundSymbol.SymbolName;
            if (foundSymbol.SymbolType is SymbolType.Function)
            {
                targetName = CommandHelpers.StripModuleQualification(targetName, out _);
                if (aliases.AliasToCmdlets.TryGetValue(foundSymbol.SymbolName, out string aliasDefinition))
                {
                    targetName = aliasDefinition;
                }
            }

            // We want to look for references first in referenced files, hence we use ordered dictionary
            // TODO: File system case-sensitivity is based on filesystem not OS, but OS is a much cheaper heuristic
            OrderedDictionary fileMap = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? new OrderedDictionary()
                : new OrderedDictionary(StringComparer.OrdinalIgnoreCase);

            foreach (ScriptFile scriptFile in referencedFiles)
            {
                fileMap[scriptFile.FilePath] = scriptFile;
            }

            await ScanWorkspacePSFiles(cancellationToken).ConfigureAwait(false);

            List<SymbolReference> symbolReferences = new();

            // Using a nested method here to get a bit more readability and to avoid roslynator
            // asserting we should use a giant nested ternary here.
            static string[] GetIdentifiers(string symbolName, SymbolType symbolType, CommandHelpers.AliasMap aliases)
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

            string[] allIdentifiers = GetIdentifiers(targetName, foundSymbol.SymbolType, aliases);

            foreach (ScriptFile file in _workspaceService.GetOpenedFiles())
            {
                foreach (string targetIdentifier in allIdentifiers)
                {
                    if (!file.References.TryGetReferences(targetIdentifier, out ConcurrentBag<SymbolReference> references))
                    {
                        continue;
                    }

                    symbolReferences.AddRange(references);

                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return symbolReferences;
        }

        /// <summary>
        /// Finds all the occurrences of a symbol in the script given a file location
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="symbolLineNumber">The line number of the cursor for the given script</param>
        /// <param name="symbolColumnNumber">The column number of the cursor for the given script</param>
        /// <returns>FindOccurrencesResult</returns>
        public static IEnumerable<SymbolReference> FindOccurrencesInFile(
            ScriptFile file,
            int symbolLineNumber,
            int symbolColumnNumber)
        {
            SymbolReference foundSymbol = AstOperations.FindSymbolAtPosition(
                file.ScriptAst,
                symbolLineNumber,
                symbolColumnNumber);

            if (foundSymbol == null)
            {
                return null;
            }

            if (file.References.TryGetReferences(foundSymbol.SymbolName, out ConcurrentBag<SymbolReference> references))
            {
                return references;
            }

            return null;
        }

        /// <summary>
        /// Finds a function, class or enum definition in the script given a file location
        /// </summary>
        /// <param name="scriptFile">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The column number of the cursor for the given script</param>
        /// <returns>A SymbolReference of the symbol found at the given location
        /// or null if there is no symbol at that location
        /// </returns>
        public static SymbolReference FindSymbolDefinitionAtLocation(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference symbolReference =
                AstOperations.FindSymbolAtPosition(
                    scriptFile.ScriptAst,
                    lineNumber,
                    columnNumber,
                    includeDefinitions: true);

            if (symbolReference != null)
            {
                symbolReference.FilePath = scriptFile.FilePath;
            }

            return symbolReference;
        }

        /// <summary>
        /// Finds the details of the symbol at the given script file location.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the symbol can be located.</param>
        /// <param name="lineNumber">The line number at which the symbol can be located.</param>
        /// <param name="columnNumber">The column number at which the symbol can be located.</param>
        /// <returns></returns>
        public Task<SymbolDetails> FindSymbolDetailsAtLocationAsync(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference symbolReference =
                AstOperations.FindSymbolAtPosition(
                    scriptFile.ScriptAst,
                    lineNumber,
                    columnNumber,
                    returnFullSignature: true);

            if (symbolReference == null)
            {
                return Task.FromResult<SymbolDetails>(null);
            }

            symbolReference.FilePath = scriptFile.FilePath;
            return SymbolDetails.CreateAsync(
                symbolReference,
                _runspaceContext.CurrentRunspace,
                _executionService);
        }

        /// <summary>
        /// Finds the parameter set hints of a specific command (determined by a given file location)
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The column number of the cursor for the given script</param>
        /// <returns>ParameterSetSignatures</returns>
        public async Task<ParameterSetSignatures> FindParameterSetsInFileAsync(
            ScriptFile file,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference foundSymbol =
                AstOperations.FindCommandAtPosition(
                    file.ScriptAst,
                    lineNumber,
                    columnNumber);

            // If we are not possibly looking at a Function, we don't
            // need to continue because we won't be able to get the
            // CommandInfo object.
            if (foundSymbol?.SymbolType is not SymbolType.Function
                and not SymbolType.Unknown)
            {
                return null;
            }

            CommandInfo commandInfo =
                await CommandHelpers.GetCommandInfoAsync(
                    foundSymbol.SymbolName,
                    _runspaceContext.CurrentRunspace,
                    _executionService).ConfigureAwait(false);

            if (commandInfo == null)
            {
                return null;
            }

            try
            {
                IEnumerable<CommandParameterSetInfo> commandParamSets = commandInfo.ParameterSets;
                return new ParameterSetSignatures(commandParamSets, foundSymbol);
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
        /// Finds the definition of a symbol in the script file or any of the
        /// files that it references.
        /// </summary>
        /// <param name="sourceFile">The initial script file to be searched for the symbol's definition.</param>
        /// <param name="foundSymbol">The symbol for which a definition will be found.</param>
        /// <returns>The resulting GetDefinitionResult for the symbol's definition.</returns>
        public async Task<SymbolReference> GetDefinitionOfSymbolAsync(
            ScriptFile sourceFile,
            SymbolReference foundSymbol)
        {
            Validate.IsNotNull(nameof(sourceFile), sourceFile);
            Validate.IsNotNull(nameof(foundSymbol), foundSymbol);

            // If symbol is an alias, resolve it.
            (Dictionary<string, List<string>> _, Dictionary<string, string> aliasToCmdlets) =
                await CommandHelpers.GetAliasesAsync(_executionService).ConfigureAwait(false);

            if (aliasToCmdlets.TryGetValue(foundSymbol.SymbolName, out string value))
            {
                foundSymbol = new SymbolReference(
                    foundSymbol.SymbolType,
value,
                    foundSymbol.ScriptRegion,
                    foundSymbol.FilePath,
                    foundSymbol.SourceLine);
            }

            ScriptFile[] referencedFiles = _workspaceService.ExpandScriptReferences(sourceFile);

            HashSet<string> filesSearched = new(StringComparer.OrdinalIgnoreCase);

            // look through the referenced files until definition is found
            // or there are no more file to look through
            SymbolReference foundDefinition = null;
            foreach (ScriptFile scriptFile in referencedFiles)
            {
                foundDefinition = AstOperations.FindDefinitionOfSymbol(scriptFile.ScriptAst, foundSymbol);

                filesSearched.Add(scriptFile.FilePath);
                if (foundDefinition is not null)
                {
                    foundDefinition.FilePath = scriptFile.FilePath;
                    break;
                }

                if (foundSymbol.SymbolType == SymbolType.Function)
                {
                    // Dot-sourcing is parsed as a "Function" Symbol.
                    string dotSourcedPath = GetDotSourcedPath(foundSymbol, scriptFile);
                    if (scriptFile.FilePath == dotSourcedPath)
                    {
                        foundDefinition = new SymbolReference(
                            SymbolType.Function,
                            foundSymbol.SymbolName,
                            scriptFile.ScriptAst.Extent,
                            scriptFile.FilePath);
                        break;
                    }
                }
            }

            // if the definition the not found in referenced files
            // look for it in all the files in the workspace
            if (foundDefinition is null)
            {
                // Get a list of all powershell files in the workspace path
                foreach (string file in _workspaceService.EnumeratePSFiles())
                {
                    if (filesSearched.Contains(file))
                    {
                        continue;
                    }

                    foundDefinition =
                        AstOperations.FindDefinitionOfSymbol(
                            Parser.ParseFile(file, out Token[] tokens, out ParseError[] parseErrors),
                            foundSymbol);

                    filesSearched.Add(file);
                    if (foundDefinition is not null)
                    {
                        foundDefinition.FilePath = file;
                        break;
                    }
                }
            }

            // if the definition is not found in a file in the workspace
            // look for it in the builtin commands but only if the symbol
            // we are looking at is possibly a Function.
            if (foundDefinition is null
                && (foundSymbol.SymbolType == SymbolType.Function
                    || foundSymbol.SymbolType == SymbolType.Unknown))
            {
                CommandInfo cmdInfo =
                    await CommandHelpers.GetCommandInfoAsync(
                        foundSymbol.SymbolName,
                        _runspaceContext.CurrentRunspace,
                        _executionService).ConfigureAwait(false);

                foundDefinition =
                    FindDeclarationForBuiltinCommand(
                        cmdInfo,
                        foundSymbol);
            }

            return foundDefinition;
        }

        private Task _workspaceScanCompleted;

        private async Task ScanWorkspacePSFiles(CancellationToken cancellationToken = default)
        {
            if (_configurationService.CurrentSettings.AnalyzeOpenDocumentsOnly)
            {
                return;
            }

            Task scanTask = _workspaceScanCompleted;
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
            cancellationToken.Register(() => cancelled.TrySetCanceled());
            await Task.WhenAny(scanTask, cancelled.Task).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a path from a dot-source symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the dot-source expression.</param>
        /// <param name="scriptFile">The script file containing the symbol</param>
        /// <returns></returns>
        private string GetDotSourcedPath(SymbolReference symbol, ScriptFile scriptFile)
        {
            string cleanedUpSymbol = PathUtils.NormalizePathSeparators(symbol.SymbolName.Trim('\'', '"'));
            string psScriptRoot = Path.GetDirectoryName(scriptFile.FilePath);
            return _workspaceService.ResolveRelativeScriptPath(psScriptRoot,
                Regex.Replace(cleanedUpSymbol, @"\$PSScriptRoot|\${PSScriptRoot}", psScriptRoot, RegexOptions.IgnoreCase));
        }

        private SymbolReference FindDeclarationForBuiltinCommand(
            CommandInfo commandInfo,
            SymbolReference foundSymbol)
        {
            if (commandInfo == null)
            {
                return null;
            }

            ScriptFile[] nestedModuleFiles =
                GetBuiltinCommandScriptFiles(
                    commandInfo.Module);

            SymbolReference foundDefinition = null;
            foreach (ScriptFile nestedModuleFile in nestedModuleFiles)
            {
                foundDefinition = AstOperations.FindDefinitionOfSymbol(
                    nestedModuleFile.ScriptAst,
                    foundSymbol);

                if (foundDefinition != null)
                {
                    foundDefinition.FilePath = nestedModuleFile.FilePath;
                    break;
                }
            }

            return foundDefinition;
        }

        private ScriptFile[] GetBuiltinCommandScriptFiles(
            PSModuleInfo moduleInfo)
        {
            if (moduleInfo == null)
            {
                return Array.Empty<ScriptFile>();
            }

            string modPath = moduleInfo.Path;
            List<ScriptFile> scriptFiles = new();
            ScriptFile newFile;

            // find any files where the moduleInfo's path ends with ps1 or psm1
            // and add it to allowed script files
            if (modPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                modPath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase))
            {
                newFile = _workspaceService.GetFile(modPath);
                newFile.IsAnalysisEnabled = false;
                scriptFiles.Add(newFile);
            }

            if (moduleInfo.NestedModules.Count > 0)
            {
                foreach (PSModuleInfo nestedInfo in moduleInfo.NestedModules)
                {
                    string nestedModPath = nestedInfo.Path;
                    if (nestedModPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                        nestedModPath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase))
                    {
                        newFile = _workspaceService.GetFile(nestedModPath);
                        newFile.IsAnalysisEnabled = false;
                        scriptFiles.Add(newFile);
                    }
                }
            }

            return scriptFiles.ToArray();
        }

        /// <summary>
        /// Finds a function definition that follows or contains the given line number.
        /// </summary>
        /// <param name="scriptFile">Open script file.</param>
        /// <param name="lineNumber">The 1 based line on which to look for function definition.</param>
        /// <param name="helpLocation"></param>
        /// <returns>If found, returns the function definition, otherwise, returns null.</returns>
        public static FunctionDefinitionAst GetFunctionDefinitionForHelpComment(
            ScriptFile scriptFile,
            int lineNumber,
            out string helpLocation)
        {
            // check if the next line contains a function definition
            FunctionDefinitionAst funcDefnAst = GetFunctionDefinitionAtLine(scriptFile, lineNumber + 1);
            if (funcDefnAst != null)
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

                    return fdAst.Body.Extent.StartLineNumber < lineNumber &&
                        fdAst.Body.Extent.EndLineNumber > lineNumber;
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
                if (funcDefnAst == null)
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

            // TODO use tokens to check for non empty character instead of just checking for line offset
            if (funcDefnAst.Body.Extent.StartLineNumber == lineNumber - 1)
            {
                helpLocation = "begin";
                return funcDefnAst;
            }

            if (funcDefnAst.Body.Extent.EndLineNumber == lineNumber + 1)
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
        /// </summary>
        /// <param name="scriptFile">Open script file.</param>
        /// <param name="lineNumber">The 1 based line on which to look for function definition.</param>
        /// <returns>If found, returns the function definition on the given line. Otherwise, returns null.</returns>
        public static FunctionDefinitionAst GetFunctionDefinitionAtLine(
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
                Task scanInProgress = _workspaceScanCompleted;
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
