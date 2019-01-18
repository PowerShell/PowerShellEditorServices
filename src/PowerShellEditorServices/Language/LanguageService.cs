//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Symbols;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides a high-level service for performing code completion and
    /// navigation operations on PowerShell scripts.
    /// </summary>
    public class LanguageService
    {
        #region Private Fields

        const int DefaultWaitTimeoutMilliseconds = 5000;

        private readonly ILogger _logger;

        private readonly PowerShellContext _powerShellContext;

        private readonly Dictionary<String, List<String>> _cmdletToAliasDictionary;

        private readonly Dictionary<String, String> _aliasToCmdletDictionary;

        private readonly IDocumentSymbolProvider[] _documentSymbolProviders;

        private readonly SemaphoreSlim _aliasHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private bool _areAliasesLoaded;

        private CompletionResults _mostRecentCompletions;

        private int _mostRecentRequestLine;

        private int _mostRecentRequestOffest;

        private string _mostRecentRequestFile;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of the LanguageService class and uses
        /// the given Runspace to execute language service operations.
        /// </summary>
        /// <param name="powerShellContext">
        /// The PowerShellContext in which language service operations will be executed.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public LanguageService(
            PowerShellContext powerShellContext,
            ILogger logger)
        {
            Validate.IsNotNull(nameof(powerShellContext), powerShellContext);

            _powerShellContext = powerShellContext;
            _logger = logger;

            _cmdletToAliasDictionary = new Dictionary<String, List<String>>(StringComparer.OrdinalIgnoreCase);
            _aliasToCmdletDictionary = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            _documentSymbolProviders = new IDocumentSymbolProvider[]
            {
                new ScriptDocumentSymbolProvider(powerShellContext.LocalPowerShellVersion.Version),
                new PsdDocumentSymbolProvider(),
                new PesterDocumentSymbolProvider()
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets completions for a statement contained in the given
        /// script file at the specified line and column position.
        /// </summary>
        /// <param name="scriptFile">
        /// The script file in which completions will be gathered.
        /// </param>
        /// <param name="lineNumber">
        /// The 1-based line number at which completions will be gathered.
        /// </param>
        /// <param name="columnNumber">
        /// The 1-based column number at which completions will be gathered.
        /// </param>
        /// <returns>
        /// A CommandCompletion instance completions for the identified statement.
        /// </returns>
        public async Task<CompletionResults> GetCompletionsInFileAsync(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            Validate.IsNotNull(nameof(scriptFile), scriptFile);

            // Get the offset at the specified position.  This method
            // will also validate the given position.
            int fileOffset =
                scriptFile.GetOffsetAtPosition(
                    lineNumber,
                    columnNumber);

            CommandCompletion commandCompletion =
                await AstOperations.GetCompletionsAsync(
                    scriptFile.ScriptAst,
                    scriptFile.ScriptTokens,
                    fileOffset,
                    _powerShellContext,
                    _logger,
                    new CancellationTokenSource(DefaultWaitTimeoutMilliseconds).Token);

            if (commandCompletion == null)
            {
                return new CompletionResults();
            }

            try
            {
                CompletionResults completionResults =
                    CompletionResults.Create(
                        scriptFile,
                        commandCompletion);

                // save state of most recent completion
                _mostRecentCompletions = completionResults;
                _mostRecentRequestFile = scriptFile.Id;
                _mostRecentRequestLine = lineNumber;
                _mostRecentRequestOffest = columnNumber;

                return completionResults;
            }
            catch (ArgumentException e)
            {
                // Bad completion results could return an invalid
                // replacement range, catch that here
                _logger.Write(
                    LogLevel.Error,
                    $"Caught exception while trying to create CompletionResults:\n\n{e.ToString()}");

                return new CompletionResults();
            }
        }

        /// <summary>
        /// Finds command completion details for the script given a file location
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="entryName">The name of the suggestion that needs details</param>
        /// <returns>CompletionResult object (contains information about the command completion)</returns>
        public CompletionDetails GetCompletionDetailsInFile(
            ScriptFile file,
            string entryName)
        {
            if (!file.Id.Equals(_mostRecentRequestFile))
            {
                return null;
            }

            foreach (CompletionDetails completion in _mostRecentCompletions.Completions)
            {
                if (completion.CompletionText.Equals(entryName))
                {
                    return completion;
                }
            }

            // If we found no completions, return null
            return null;
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
            SymbolDetails symbolDetails = null;
            SymbolReference symbolReference =
                AstOperations.FindSymbolAtPosition(
                    scriptFile.ScriptAst,
                    lineNumber,
                    columnNumber);

            if (symbolReference == null)
            {
                // TODO #21: Return Result<T>
                return null;
            }

            symbolReference.FilePath = scriptFile.FilePath;
            symbolDetails =
                await SymbolDetails.CreateAsync(
                    symbolReference,
                    _powerShellContext);

            return symbolDetails;
        }

        /// <summary>
        /// Finds all the symbols in a file.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the symbol can be located.</param>
        /// <returns></returns>
        public FindOccurrencesResult FindSymbolsInFile(ScriptFile scriptFile)
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

            return new FindOccurrencesResult
            {
                FoundOccurrences = foundOccurrences
            };
        }

        /// <summary>
        /// Finds all the references of a symbol
        /// </summary>
        /// <param name="foundSymbol">The symbol to find all references for</param>
        /// <param name="referencedFiles">An array of scriptFiles too search for references in</param>
        /// <param name="workspace">The workspace that will be searched for symbols</param>
        /// <returns>FindReferencesResult</returns>
        public async Task<FindReferencesResult> FindReferencesOfSymbolAsync(
            SymbolReference foundSymbol,
            ScriptFile[] referencedFiles,
            Workspace workspace)
        {
            if (foundSymbol == null)
            {
                return null;
            }

            int symbolOffset = referencedFiles[0].GetOffsetAtPosition(
                foundSymbol.ScriptRegion.StartLineNumber,
                foundSymbol.ScriptRegion.StartColumnNumber);

            // Make sure aliases have been loaded
            await GetAliasesAsync();

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
                await _aliasHandle.WaitAsync();
                try
                {

                    IEnumerable<SymbolReference> references = AstOperations.FindReferencesOfSymbol(
                        file.ScriptAst,
                        foundSymbol,
                        _cmdletToAliasDictionary,
                        _aliasToCmdletDictionary);

                    foreach (SymbolReference reference in references)
                    {
                        try
                        {
                            reference.SourceLine = file.GetLine(reference.ScriptRegion.StartLineNumber);
                        }
                        catch (ArgumentOutOfRangeException e)
                        {
                            reference.SourceLine = string.Empty;
                            _logger.WriteException("Found reference is out of range in script file", e);
                        }
                        reference.FilePath = file.FilePath;
                        symbolReferences.Add(reference);
                    }
                }
                finally
                {
                    _aliasHandle.Release();
                }
            }

            return new FindReferencesResult
            {
                SymbolFileOffset = symbolOffset,
                SymbolName = foundSymbol.SymbolName,
                FoundReferences = symbolReferences
            };
        }

        /// <summary>
        /// Finds the definition of a symbol in the script file or any of the
        /// files that it references.
        /// </summary>
        /// <param name="sourceFile">The initial script file to be searched for the symbol's definition.</param>
        /// <param name="foundSymbol">The symbol for which a definition will be found.</param>
        /// <param name="workspace">The Workspace to which the ScriptFile belongs.</param>
        /// <returns>The resulting GetDefinitionResult for the symbol's definition.</returns>
        public async Task<GetDefinitionResult> GetDefinitionOfSymbolAsync(
            ScriptFile sourceFile,
            SymbolReference foundSymbol,
            Workspace workspace)
        {
            Validate.IsNotNull(nameof(sourceFile), sourceFile);
            Validate.IsNotNull(nameof(foundSymbol), foundSymbol);
            Validate.IsNotNull(nameof(workspace), workspace);

            ScriptFile[] referencedFiles =
                workspace.ExpandScriptReferences(
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
                    string dotSourcedPath = GetDotSourcedPath(foundSymbol, workspace, scriptFile);
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
                IEnumerable<string> allFiles = workspace.EnumeratePSFiles();
                foreach (string file in allFiles)
                {
                    if (filesSearched.Contains(file))
                    {
                        continue;
                    }

                    Token[] tokens = null;
                    ParseError[] parseErrors = null;
                    foundDefinition =
                        AstOperations.FindDefinitionOfSymbol(
                            Parser.ParseFile(file, out tokens, out parseErrors),
                            foundSymbol);

                    filesSearched.Add(file);
                    if (foundDefinition != null)
                    {
                        foundDefinition.FilePath = file;
                        break;
                    }

                }
            }

            // if definition is not found in file in the workspace
            // look for it in the builtin commands
            if (foundDefinition == null)
            {
                CommandInfo cmdInfo =
                    await CommandHelpers.GetCommandInfoAsync(
                        foundSymbol.SymbolName,
                        _powerShellContext);

                foundDefinition =
                    FindDeclarationForBuiltinCommand(
                        cmdInfo,
                        foundSymbol,
                        workspace);
            }

            return foundDefinition != null ?
                new GetDefinitionResult(foundDefinition) :
                null;
        }

        /// <summary>
        /// Gets a path from a dot-source symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the dot-source expression.</param>
        /// <param name="workspace">The current workspace</param>
        /// <param name="scriptFile">The script file containing the symbol</param>
        /// <returns></returns>
        private static string GetDotSourcedPath(SymbolReference symbol, Workspace workspace, ScriptFile scriptFile)
        {
            string cleanedUpSymbol = PathUtils.NormalizePathSeparators(symbol.SymbolName.Trim('\'', '"'));
            string psScriptRoot = Path.GetDirectoryName(scriptFile.FilePath);
            return workspace.ResolveRelativeScriptPath(psScriptRoot,
                Regex.Replace(cleanedUpSymbol, @"\$PSScriptRoot|\${PSScriptRoot}", psScriptRoot, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Finds all the occurences of a symbol in the script given a file location
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>FindOccurrencesResult</returns>
        public FindOccurrencesResult FindOccurrencesInFile(
            ScriptFile file,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference foundSymbol =
                AstOperations.FindSymbolAtPosition(
                    file.ScriptAst,
                    lineNumber,
                    columnNumber);

            if (foundSymbol == null)
            {
                return null;
            }

            // find all references, and indicate that looking for aliases is not needed
            IEnumerable<SymbolReference> symbolOccurrences =
                AstOperations
                    .FindReferencesOfSymbol(
                        file.ScriptAst,
                        foundSymbol,
                        false);

            return new FindOccurrencesResult
            {
                FoundOccurrences = symbolOccurrences
            };
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
            int columnNumber)
        {
            SymbolReference foundSymbol =
                AstOperations.FindCommandAtPosition(
                    file.ScriptAst,
                    lineNumber,
                    columnNumber);

            if (foundSymbol == null)
            {
                return null;
            }

            CommandInfo commandInfo =
                await CommandHelpers.GetCommandInfoAsync(
                    foundSymbol.SymbolName,
                    _powerShellContext);

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
                _logger.WriteException("RuntimeException encountered while accessing command parameter sets", e);

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
        /// Gets the smallest statment ast that contains the given script position as
        /// indicated by lineNumber and columnNumber parameters.
        /// </summary>
        /// <param name="scriptFile">Open script file.</param>
        /// <param name="lineNumber">1-based line number of the position.</param>
        /// <param name="columnNumber">1-based column number of the position.</param>
        /// <returns></returns>
        public ScriptRegion FindSmallestStatementAstRegion(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            Ast ast = FindSmallestStatementAst(scriptFile, lineNumber, columnNumber);
            if (ast == null)
            {
                return null;
            }

            return ScriptRegion.Create(ast.Extent);
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
                    var fdAst = ast as FunctionDefinitionAst;
                    if (fdAst == null)
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

        #endregion

        #region Private Fields

        /// <summary>
        /// Gets all aliases found in the runspace
        /// </summary>
        private async Task GetAliasesAsync()
        {
            if (_areAliasesLoaded)
            {
                return;
            }

            await _aliasHandle.WaitAsync();
            try
            {
                if (_powerShellContext.IsCurrentRunspaceOutOfProcess())
                {
                    _areAliasesLoaded = true;
                    return;
                }

                var aliases = await _powerShellContext.ExecuteCommandAsync<AliasInfo>(
                    new PSCommand()
                        .AddCommand("Microsoft.PowerShell.Core\\Get-Command")
                        .AddParameter("CommandType", CommandTypes.Alias),
                    sendOutputToHost: false,
                    sendErrorToHost: false);

                foreach (AliasInfo aliasInfo in aliases)
                {
                    // Using Get-Command will obtain aliases from modules not yet loaded,
                    // these aliases will not have a definition.
                    if (string.IsNullOrEmpty(aliasInfo.Definition))
                    {
                        continue;
                    }

                    if (!_cmdletToAliasDictionary.ContainsKey(aliasInfo.Definition))
                    {
                        _cmdletToAliasDictionary.Add(aliasInfo.Definition, new List<String> { aliasInfo.Name });
                    }
                    else
                    {
                        _cmdletToAliasDictionary[aliasInfo.Definition].Add(aliasInfo.Name);
                    }

                    _aliasToCmdletDictionary.Add(aliasInfo.Name, aliasInfo.Definition);
                }

                _areAliasesLoaded = true;
            }
            catch (PSNotSupportedException e)
            {
                _logger.Write(
                    LogLevel.Warning,
                    $"Caught PSNotSupportedException while attempting to get aliases from remote session:\n\n{e.ToString()}");

                // Prevent the aliases from being fetched again - no point if the remote doesn't support InvokeCommand.
                _areAliasesLoaded = true;
            }
            catch (TaskCanceledException)
            {
                // The wait for a RunspaceHandle has timed out, skip aliases for now
            }
            finally
            {
                _aliasHandle.Release();
            }
        }

        private ScriptFile[] GetBuiltinCommandScriptFiles(
            PSModuleInfo moduleInfo,
            Workspace workspace)
        {
            if (moduleInfo == null)
            {
                return new ScriptFile[0];
            }

            string modPath = moduleInfo.Path;
            List<ScriptFile> scriptFiles = new List<ScriptFile>();
            ScriptFile newFile;

            // find any files where the moduleInfo's path ends with ps1 or psm1
            // and add it to allowed script files
            if (modPath.EndsWith(@".ps1") || modPath.EndsWith(@".psm1"))
            {
                newFile = workspace.GetFile(modPath);
                newFile.IsAnalysisEnabled = false;
                scriptFiles.Add(newFile);
            }
            if (moduleInfo.NestedModules.Count > 0)
            {
                foreach (PSModuleInfo nestedInfo in moduleInfo.NestedModules)
                {
                    string nestedModPath = nestedInfo.Path;
                    if (nestedModPath.EndsWith(@".ps1") || nestedModPath.EndsWith(@".psm1"))
                    {
                        newFile = workspace.GetFile(nestedModPath);
                        newFile.IsAnalysisEnabled = false;
                        scriptFiles.Add(newFile);
                    }
                }
            }

            return scriptFiles.ToArray();
        }

        private SymbolReference FindDeclarationForBuiltinCommand(
            CommandInfo commandInfo,
            SymbolReference foundSymbol,
            Workspace workspace)
        {
            if (commandInfo == null)
            {
                return null;
            }

            ScriptFile[] nestedModuleFiles =
                GetBuiltinCommandScriptFiles(
                    commandInfo.Module,
                    workspace);

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

        private Ast FindSmallestStatementAst(ScriptFile scriptFile, int lineNumber, int columnNumber)
        {
            IEnumerable<Ast> asts = scriptFile.ScriptAst.FindAll(ast =>
            {
                return ast is StatementAst && ast.Extent.Contains(lineNumber, columnNumber);
            }, true);

            // Find the Ast with the smallest extent
            Ast minAst = scriptFile.ScriptAst;
            foreach (Ast ast in asts)
            {
                if (ast.Extent.ExtentWidthComparer(minAst.Extent) == -1)
                {
                    minAst = ast;
                }
            }

            return minAst;
        }

        #endregion
    }
}
