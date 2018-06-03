//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Symbols;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
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

        private ILogger logger;
        private bool areAliasesLoaded;
        private PowerShellContext powerShellContext;
        private CompletionResults mostRecentCompletions;
        private int mostRecentRequestLine;
        private int mostRecentRequestOffest;
        private string mostRecentRequestFile;
        private Dictionary<String, List<String>> CmdletToAliasDictionary;
        private Dictionary<String, String> AliasToCmdletDictionary;
        private IDocumentSymbolProvider[] documentSymbolProviders;
        private SemaphoreSlim aliasHandle = new SemaphoreSlim(1, 1);

        const int DefaultWaitTimeoutMilliseconds = 5000;

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
            Validate.IsNotNull("powerShellContext", powerShellContext);

            this.powerShellContext = powerShellContext;
            this.logger = logger;

            this.CmdletToAliasDictionary = new Dictionary<String, List<String>>(StringComparer.OrdinalIgnoreCase);
            this.AliasToCmdletDictionary = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            this.documentSymbolProviders = new IDocumentSymbolProvider[]
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
        public async Task<CompletionResults> GetCompletionsInFile(
            ScriptFile scriptFile,
            int lineNumber,
            int columnNumber)
        {
            Validate.IsNotNull("scriptFile", scriptFile);

            // Get the offset at the specified position.  This method
            // will also validate the given position.
            int fileOffset =
                scriptFile.GetOffsetAtPosition(
                    lineNumber,
                    columnNumber);

            CommandCompletion commandCompletion =
                await AstOperations.GetCompletions(
                    scriptFile.ScriptAst,
                    scriptFile.ScriptTokens,
                    fileOffset,
                    this.powerShellContext,
                    this.logger,
                    new CancellationTokenSource(DefaultWaitTimeoutMilliseconds).Token);

            if (commandCompletion != null)
            {
                try
                {
                    CompletionResults completionResults =
                        CompletionResults.Create(
                            scriptFile,
                            commandCompletion);

                    // save state of most recent completion
                    mostRecentCompletions = completionResults;
                    mostRecentRequestFile = scriptFile.Id;
                    mostRecentRequestLine = lineNumber;
                    mostRecentRequestOffest = columnNumber;

                    return completionResults;
                }
                catch (ArgumentException e)
                {
                    // Bad completion results could return an invalid
                    // replacement range, catch that here
                    this.logger.Write(
                        LogLevel.Error,
                        $"Caught exception while trying to create CompletionResults:\n\n{e.ToString()}");
                }
            }

            // If all else fails, return empty results
            return new CompletionResults();
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
            // Makes sure the most recent completions request was the same line and column as this request
            if (file.Id.Equals(mostRecentRequestFile))
            {
                CompletionDetails completionResult =
                    mostRecentCompletions.Completions.FirstOrDefault(
                        result => result.CompletionText.Equals(entryName));

                return completionResult;
            }
            else
            {
                return null;
            }
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
        public async Task<SymbolDetails> FindSymbolDetailsAtLocation(
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

            if (symbolReference != null)
            {
                symbolReference.FilePath = scriptFile.FilePath;
                symbolDetails =
                    await SymbolDetails.Create(
                        symbolReference,
                        this.powerShellContext);
            }
            else
            {
                // TODO #21: Return Result<T>
                return null;
            }

            return symbolDetails;
        }

        /// <summary>
        /// Finds all the symbols in a file.
        /// </summary>
        /// <param name="scriptFile">The ScriptFile in which the symbol can be located.</param>
        /// <returns></returns>
        public FindOccurrencesResult FindSymbolsInFile(ScriptFile scriptFile)
        {
            Validate.IsNotNull("scriptFile", scriptFile);
            return new FindOccurrencesResult
            {
                FoundOccurrences = documentSymbolProviders
                    .SelectMany(p => p.ProvideDocumentSymbols(scriptFile))
                    .Select(reference =>
                        {
                            reference.SourceLine =
                                scriptFile.GetLine(reference.ScriptRegion.StartLineNumber);
                            reference.FilePath = scriptFile.FilePath;
                            return reference;
                        })
            };
        }

        /// <summary>
        /// Finds all the references of a symbol
        /// </summary>
        /// <param name="foundSymbol">The symbol to find all references for</param>
        /// <param name="referencedFiles">An array of scriptFiles too search for references in</param>
        /// <param name="workspace">The workspace that will be searched for symbols</param>
        /// <returns>FindReferencesResult</returns>
        public async Task<FindReferencesResult> FindReferencesOfSymbol(
            SymbolReference foundSymbol,
            ScriptFile[] referencedFiles,
            Workspace workspace)
        {
            if (foundSymbol != null)
            {
                int symbolOffset = referencedFiles[0].GetOffsetAtPosition(
                    foundSymbol.ScriptRegion.StartLineNumber,
                    foundSymbol.ScriptRegion.StartColumnNumber);

                // Make sure aliases have been loaded
                await GetAliases();

                // We want to look for references first in referenced files, hence we use ordered dictionary
                var fileMap = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
                foreach (ScriptFile file in referencedFiles)
                {
                    fileMap.Add(file.FilePath, file);
                }

                var allFiles = workspace.EnumeratePSFiles();
                foreach (var file in allFiles)
                {
                    if (!fileMap.Contains(file))
                    {
                        fileMap.Add(file, new ScriptFile(file, null, this.powerShellContext.LocalPowerShellVersion.Version));
                    }
                }

                List<SymbolReference> symbolReferences = new List<SymbolReference>();
                foreach (var fileName in fileMap.Keys)
                {
                    var file = (ScriptFile)fileMap[fileName];
                    IEnumerable<SymbolReference> symbolReferencesinFile;
                    await this.aliasHandle.WaitAsync();
                    try
                    {
                        symbolReferencesinFile =
                            AstOperations
                                .FindReferencesOfSymbol(
                                    file.ScriptAst,
                                    foundSymbol,
                                    CmdletToAliasDictionary,
                                    AliasToCmdletDictionary)
                                .Select(
                                    reference =>
                                    {
                                        try
                                        {
                                            reference.SourceLine =
                                                file.GetLine(reference.ScriptRegion.StartLineNumber);
                                        }
                                        catch (ArgumentOutOfRangeException e)
                                        {
                                            reference.SourceLine = string.Empty;
                                            this.logger.WriteException("Found reference is out of range in script file", e);
                                        }

                                        reference.FilePath = file.FilePath;
                                        return reference;
                                    });
                    }
                    finally
                    {
                        this.aliasHandle.Release();
                    }

                    symbolReferences.AddRange(symbolReferencesinFile);
                }

                return
                    new FindReferencesResult
                    {
                        SymbolFileOffset = symbolOffset,
                        SymbolName = foundSymbol.SymbolName,
                        FoundReferences = symbolReferences
                    };
            }
            else { return null; }
        }

        /// <summary>
        /// Finds the definition of a symbol in the script file or any of the
        /// files that it references.
        /// </summary>
        /// <param name="sourceFile">The initial script file to be searched for the symbol's definition.</param>
        /// <param name="foundSymbol">The symbol for which a definition will be found.</param>
        /// <param name="workspace">The Workspace to which the ScriptFile belongs.</param>
        /// <returns>The resulting GetDefinitionResult for the symbol's definition.</returns>
        public async Task<GetDefinitionResult> GetDefinitionOfSymbol(
            ScriptFile sourceFile,
            SymbolReference foundSymbol,
            Workspace workspace)
        {
            Validate.IsNotNull("sourceFile", sourceFile);
            Validate.IsNotNull("foundSymbol", foundSymbol);
            Validate.IsNotNull("workspace", workspace);

            ScriptFile[] referencedFiles =
                workspace.ExpandScriptReferences(
                    sourceFile);

            var filesSearched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // look through the referenced files until definition is found
            // or there are no more file to look through
            SymbolReference foundDefinition = null;
            for (int i = 0; i < referencedFiles.Length; i++)
            {
                foundDefinition =
                    AstOperations.FindDefinitionOfSymbol(
                        referencedFiles[i].ScriptAst,
                        foundSymbol);

                filesSearched.Add(referencedFiles[i].FilePath);
                if (foundDefinition != null)
                {
                    foundDefinition.FilePath = referencedFiles[i].FilePath;
                    break;
                }
            }

            // if the definition the not found in referenced files
            // look for it in all the files in the workspace
            if (foundDefinition == null)
            {
                // Get a list of all powershell files in the workspace path
                var allFiles = workspace.EnumeratePSFiles();
                foreach (var file in allFiles)
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
                    await CommandHelpers.GetCommandInfo(
                        foundSymbol.SymbolName,
                        this.powerShellContext);

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

            if (foundSymbol != null)
            {
                // find all references, and indicate that looking for aliases is not needed
                IEnumerable<SymbolReference> symbolOccurrences =
                    AstOperations
                        .FindReferencesOfSymbol(
                            file.ScriptAst,
                            foundSymbol,
                            false);

                return
                    new FindOccurrencesResult
                    {
                        FoundOccurrences = symbolOccurrences
                    };
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the parameter set hints of a specific command (determined by a given file location)
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>ParameterSetSignatures</returns>
        public async Task<ParameterSetSignatures> FindParameterSetsInFile(
            ScriptFile file,
            int lineNumber,
            int columnNumber)
        {
            SymbolReference foundSymbol =
                AstOperations.FindCommandAtPosition(
                    file.ScriptAst,
                    lineNumber,
                    columnNumber);

            if (foundSymbol != null)
            {
                CommandInfo commandInfo =
                    await CommandHelpers.GetCommandInfo(
                        foundSymbol.SymbolName,
                        this.powerShellContext);

                if (commandInfo != null)
                {
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
                        this.logger.WriteException("RuntimeException encountered while accessing command parameter sets", e);

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
                else
                {
                    return null;
                }
            }
            else
            {
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
            var ast = FindSmallestStatementAst(scriptFile, lineNumber, columnNumber);
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
            var functionDefinitionAst = scriptFile.ScriptAst.Find(
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
            var funcDefnAst = GetFunctionDefinitionAtLine(scriptFile, lineNumber + 1);
            if (funcDefnAst != null)
            {
                helpLocation = "before";
                return funcDefnAst;
            }

            // find all the script definitions that contain the line `lineNumber`
            var foundAsts = scriptFile.ScriptAst.FindAll(
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

            if (foundAsts != null && foundAsts.Any())
            {
                // of all the function definitions found, return the innermost function
                // definition that contains `lineNumber`
                funcDefnAst = foundAsts.Cast<FunctionDefinitionAst>().Aggregate((x, y) =>
                {
                    if (x.Extent.StartOffset >= y.Extent.StartOffset && x.Extent.EndOffset <= x.Extent.EndOffset)
                    {
                        return x;
                    }

                    return y;
                });

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
            }

            helpLocation = null;
            return null;
        }

        #endregion

        #region Private Fields

        /// <summary>
        /// Gets all aliases found in the runspace
        /// </summary>
        private async Task GetAliases()
        {
            await this.aliasHandle.WaitAsync();
            try
            {
                if (!this.areAliasesLoaded)
                {
                    if (this.powerShellContext.IsCurrentRunspaceOutOfProcess())
                    {
                        this.areAliasesLoaded = true;
                        return;
                    }

                    var aliases = await this.powerShellContext.ExecuteCommand<AliasInfo>(
                        new PSCommand()
                            .AddCommand("Microsoft.PowerShell.Core\\Get-Command")
                            .AddParameter("CommandType", CommandTypes.Alias),
                        false,
                        false);

                    foreach (AliasInfo aliasInfo in aliases)
                    {
                        // Using Get-Command will obtain aliases from modules not yet loaded,
                        // these aliases will not have a definition.
                        if (string.IsNullOrEmpty(aliasInfo.Definition))
                        {
                            continue;
                        }

                        if (!CmdletToAliasDictionary.ContainsKey(aliasInfo.Definition))
                        {
                            CmdletToAliasDictionary.Add(aliasInfo.Definition, new List<String>() { aliasInfo.Name });
                        }
                        else
                        {
                            CmdletToAliasDictionary[aliasInfo.Definition].Add(aliasInfo.Name);
                        }

                        AliasToCmdletDictionary.Add(aliasInfo.Name, aliasInfo.Definition);
                    }

                    this.areAliasesLoaded = true;
                }
            }
            finally
            {
                this.aliasHandle.Release();
            }
        }

        private ScriptFile[] GetBuiltinCommandScriptFiles(
            PSModuleInfo moduleInfo,
            Workspace workspace)
        {
            // if there is module info for this command
            if (moduleInfo != null)
            {
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

            return new List<ScriptFile>().ToArray();
        }

        private SymbolReference FindDeclarationForBuiltinCommand(
            CommandInfo commandInfo,
            SymbolReference foundSymbol,
            Workspace workspace)
        {
            SymbolReference foundDefinition = null;
            if (commandInfo != null)
            {
                int index = 0;
                ScriptFile[] nestedModuleFiles;

                nestedModuleFiles =
                    GetBuiltinCommandScriptFiles(
                        commandInfo.Module,
                        workspace);

                while (foundDefinition == null && index < nestedModuleFiles.Length)
                {
                    foundDefinition =
                        AstOperations.FindDefinitionOfSymbol(
                            nestedModuleFiles[index].ScriptAst,
                            foundSymbol);

                    if (foundDefinition != null)
                    {
                        foundDefinition.FilePath = nestedModuleFiles[index].FilePath;
                    }

                    index++;
                }
            }

            return foundDefinition;
        }

        private Ast FindSmallestStatementAst(ScriptFile scriptFile, int lineNumber, int columnNumber)
        {
            var asts = scriptFile.ScriptAst.FindAll(ast =>
            {
                return ast is StatementAst && ast.Extent.Contains(lineNumber, columnNumber);
            }, true);

            // Find ast with the smallest extent
            return asts.MinElement((astX, astY) => astX.Extent.ExtentWidthComparer(astY.Extent));
        }

        #endregion
    }
}
