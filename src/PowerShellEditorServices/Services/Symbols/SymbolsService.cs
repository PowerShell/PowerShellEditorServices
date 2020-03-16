//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.CodeLenses;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
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
        private readonly PowerShellContextService _powerShellContextService;
        private readonly WorkspaceService _workspaceService;

        private readonly ConcurrentDictionary<string, ICodeLensProvider> _codeLensProviders;
        private readonly ConcurrentDictionary<string, IDocumentSymbolProvider> _documentSymbolProviders;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of the SymbolsService class and uses
        /// the given Runspace to execute language service operations.
        /// </summary>
        /// <param name="factory">An ILoggerFactory implementation used for writing log messages.</param>
        public SymbolsService(
            ILoggerFactory factory,
            PowerShellContextService powerShellContextService,
            WorkspaceService workspaceService,
            ConfigurationService configurationService)
        {
            _logger = factory.CreateLogger<SymbolsService>();
            _powerShellContextService = powerShellContextService;
            _workspaceService = workspaceService;

            _codeLensProviders = new ConcurrentDictionary<string, ICodeLensProvider>();
            var codeLensProviders = new ICodeLensProvider[]
            {
                new ReferencesCodeLensProvider(_workspaceService, this),
                new PesterCodeLensProvider(configurationService),
            };
            foreach (ICodeLensProvider codeLensProvider in codeLensProviders)
            {
                _codeLensProviders.TryAdd(codeLensProvider.ProviderId, codeLensProvider);
            }

            _documentSymbolProviders = new ConcurrentDictionary<string, IDocumentSymbolProvider>();
            var documentSymbolProviders = new IDocumentSymbolProvider[]
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

        public bool TryResgisterCodeLensProvider(ICodeLensProvider codeLensProvider)
        {
            return _codeLensProviders.TryAdd(codeLensProvider.ProviderId, codeLensProvider);
        }

        public bool DeregisterCodeLensProvider(string providerId)
        {
            return _codeLensProviders.TryRemove(providerId, out _);
        }

        public IEnumerable<ICodeLensProvider> GetCodeLensProviders()
        {
            return _codeLensProviders.Values;
        }

        public bool TryRegisterDocumentSymbolProvider(IDocumentSymbolProvider documentSymbolProvider)
        {
            return _documentSymbolProviders.TryAdd(documentSymbolProvider.ProviderId, documentSymbolProvider);
        }

        public bool DeregisterDocumentSymbolProvider(string providerId)
        {
            return _documentSymbolProviders.TryRemove(providerId, out _);
        }

        public IEnumerable<IDocumentSymbolProvider> GetDocumentSymbolProviders()
        {
            return _documentSymbolProviders.Values;
        }

        /// <summary>
        /// Finds all the symbols in a file.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the symbol can be located.</param>
        /// <returns></returns>
        public List<SymbolReference> FindSymbolsInFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            var foundOccurrences = new List<SymbolReference>();
            foreach (IDocumentSymbolProvider symbolProvider in GetDocumentSymbolProviders())
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

        /// <summary>
        /// Finds all the occurences of a symbol in the script given a file location
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="symbolLineNumber">The line number of the cursor for the given script</param>
        /// <param name="symbolColumnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>FindOccurrencesResult</returns>
        public IReadOnlyList<SymbolReference> FindOccurrencesInFile(
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

            return AstOperations.FindReferencesOfSymbol(
                file.ScriptAst,
                foundSymbol,
                needsAliases: false).ToArray();
        }

        /// <summary>
        /// Finds a function definition in the script given a file location
        /// </summary>
        /// <param name="scriptFile">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>A SymbolReference of the symbol found at the given location
        /// or null if there is no symbol at that location
        /// </returns>
        public SymbolReference FindFunctionDefinitionAtLocation(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference symbolReference =
                AstOperations.FindSymbolAtPosition(
                    scriptFile.ScriptAst,
                    lineNumber,
                    columnNumber,
                    includeFunctionDefinitions: true);

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
        public async Task<SymbolDetails> FindSymbolDetailsAtLocationAsync(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference symbolReference =
                AstOperations.FindSymbolAtPosition(
                    scriptFile.ScriptAst,
                    lineNumber,
                    columnNumber);

            if (symbolReference == null)
            {
                return null;
            }

            symbolReference.FilePath = scriptFile.FilePath;
            SymbolDetails symbolDetails = await SymbolDetails.CreateAsync(
                symbolReference,
                _powerShellContextService).ConfigureAwait(false);

            return symbolDetails;
        }

        /// <summary>
        /// Finds the parameter set hints of a specific command (determined by a given file location)
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>ParameterSetSignatures</returns>
        public async Task<ParameterSetSignatures> FindParameterSetsInFileAsync(
            ScriptFile file,
            int lineNumber,
            int columnNumber,
            PowerShellContextService powerShellContext)
        {
            SymbolReference foundSymbol =
                AstOperations.FindCommandAtPosition(
                    file.ScriptAst,
                    lineNumber,
                    columnNumber);

            // If we are not possibly looking at a Function, we don't
            // need to continue because we won't be able to get the
            // CommandInfo object.
            if (foundSymbol?.SymbolType != SymbolType.Function
                && foundSymbol?.SymbolType != SymbolType.Unknown)
            {
                return null;
            }

            CommandInfo commandInfo =
                await CommandHelpers.GetCommandInfoAsync(
                    foundSymbol.SymbolName,
                    powerShellContext).ConfigureAwait(false);

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

            ScriptFile[] referencedFiles =
                _workspaceService.ExpandScriptReferences(
                    sourceFile);

            var filesSearched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // look through the referenced files until definition is found
            // or there are no more file to look through
            SymbolReference foundDefinition = null;
            foreach (ScriptFile scriptFile in referencedFiles)
            {
                foundDefinition =
                    AstOperations.FindDefinitionOfSymbol(
                        scriptFile.ScriptAst,
                        foundSymbol);

                filesSearched.Add(scriptFile.FilePath);
                if (foundDefinition != null)
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
                        foundDefinition = new SymbolReference(SymbolType.Function, foundSymbol.SymbolName, scriptFile.ScriptAst.Extent, scriptFile.FilePath);
                        break;
                    }
                }
            }

            // if the definition the not found in referenced files
            // look for it in all the files in the workspace
            if (foundDefinition == null)
            {
                // Get a list of all powershell files in the workspace path
                IEnumerable<string> allFiles = _workspaceService.EnumeratePSFiles();
                foreach (string file in allFiles)
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
                    if (foundDefinition != null)
                    {
                        foundDefinition.FilePath = file;
                        break;
                    }
                }
            }

            // if the definition is not found in a file in the workspace
            // look for it in the builtin commands but only if the symbol
            // we are looking at is possibly a Function.
            if (foundDefinition == null
                && (foundSymbol.SymbolType == SymbolType.Function
                    || foundSymbol.SymbolType == SymbolType.Unknown))
            {
                CommandInfo cmdInfo =
                    await CommandHelpers.GetCommandInfoAsync(
                        foundSymbol.SymbolName,
                        _powerShellContextService).ConfigureAwait(false);

                foundDefinition =
                    FindDeclarationForBuiltinCommand(
                        cmdInfo,
                        foundSymbol);
            }

            return foundDefinition;
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
            List<ScriptFile> scriptFiles = new List<ScriptFile>();
            ScriptFile newFile;

            // find any files where the moduleInfo's path ends with ps1 or psm1
            // and add it to allowed script files
            if (modPath.EndsWith(@".ps1", StringComparison.OrdinalIgnoreCase) ||
                modPath.EndsWith(@".psm1", StringComparison.OrdinalIgnoreCase))
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
                    if (nestedModPath.EndsWith(@".ps1", StringComparison.OrdinalIgnoreCase) ||
                        nestedModPath.EndsWith(@".psm1", StringComparison.OrdinalIgnoreCase))
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
        public FunctionDefinitionAst GetFunctionDefinitionForHelpComment(
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
                    if (!(ast is FunctionDefinitionAst fdAst))
                    {
                        return false;
                    }

                    return fdAst.Body.Extent.StartLineNumber < lineNumber &&
                        fdAst.Body.Extent.EndLineNumber > lineNumber;
                },
                true);

            if (foundAsts == null || !foundAsts.Any())
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
        public FunctionDefinitionAst GetFunctionDefinitionAtLine(
            ScriptFile scriptFile,
            int lineNumber)
        {
            Ast functionDefinitionAst = scriptFile.ScriptAst.Find(
                ast => ast is FunctionDefinitionAst && ast.Extent.StartLineNumber == lineNumber,
                true);

            return functionDefinitionAst as FunctionDefinitionAst;
        }
    }
}
