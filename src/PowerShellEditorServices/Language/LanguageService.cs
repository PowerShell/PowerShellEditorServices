//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Threading;
    /// <summary>
    /// Provides a high-level service for performing code completion and
    /// navigation operations on PowerShell scripts.
    /// </summary>
    public class LanguageService
    {
        #region Private Fields

        private bool areAliasesLoaded;
        private PowerShellContext powerShellContext;
        private CompletionResults mostRecentCompletions;
        private int mostRecentRequestLine;
        private int mostRecentRequestOffest;
        private string mostRecentRequestFile;
        private Dictionary<String, List<String>> CmdletToAliasDictionary;
        private Dictionary<String, String> AliasToCmdletDictionary;
        private string mostRecentActiveCommand;
        private int? mostRecentActiveSignature;
        private int? mostRecentActiveParameter;

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
        public LanguageService(PowerShellContext powerShellContext)
        {
            Validate.IsNotNull("powerShellContext", powerShellContext);

            this.powerShellContext = powerShellContext;

            this.CmdletToAliasDictionary = new Dictionary<String, List<String>>(StringComparer.OrdinalIgnoreCase);
            this.AliasToCmdletDictionary = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
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

            RunspaceHandle runspaceHandle =
                await this.powerShellContext.GetRunspaceHandle(
                    new CancellationTokenSource(DefaultWaitTimeoutMilliseconds).Token);

            CommandCompletion commandCompletion =
                AstOperations.GetCompletions(
                    scriptFile.ScriptAst,
                    scriptFile.ScriptTokens,
                    fileOffset,
                    runspaceHandle.Runspace);

            runspaceHandle.Dispose();

            if (commandCompletion != null)
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
            else
            {
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
                // Request a runspace handle with a short timeout
                RunspaceHandle runspaceHandle =
                    await this.powerShellContext.GetRunspaceHandle(
                        new CancellationTokenSource(DefaultWaitTimeoutMilliseconds).Token);

                symbolReference.FilePath = scriptFile.FilePath;
                symbolDetails = new SymbolDetails(symbolReference, runspaceHandle.Runspace);

                runspaceHandle.Dispose();
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

            IEnumerable<SymbolReference> symbolReferencesinFile =
                AstOperations
                    .FindSymbolsInDocument(scriptFile.ScriptAst, this.powerShellContext.PowerShellVersion)
                    .Select(
                        reference => {
                            reference.SourceLine =
                                scriptFile.GetLine(reference.ScriptRegion.StartLineNumber);
                            reference.FilePath = scriptFile.FilePath;
                            return reference;
                        });

            return
                new FindOccurrencesResult {
                    FoundOccurrences = symbolReferencesinFile
                };
        }

        /// <summary>
        /// Finds all the references of a symbol
        /// </summary>
        /// <param name="foundSymbol">The symbol to find all references for</param>
        /// <param name="referencedFiles">An array of scriptFiles too search for references in</param>
        /// <returns>FindReferencesResult</returns>
        public async Task<FindReferencesResult> FindReferencesOfSymbol(
            SymbolReference foundSymbol,
            ScriptFile[] referencedFiles)
        {                
            if (foundSymbol != null)
            {
                int symbolOffset = referencedFiles[0].GetOffsetAtPosition(
                    foundSymbol.ScriptRegion.StartLineNumber,
                    foundSymbol.ScriptRegion.StartColumnNumber);

                // Make sure aliases have been loaded
                await GetAliases();

                List<SymbolReference> symbolReferences = new List<SymbolReference>();
                foreach (ScriptFile file in referencedFiles)
                {
                    IEnumerable<SymbolReference> symbolReferencesinFile =
                    AstOperations
                        .FindReferencesOfSymbol(
                            file.ScriptAst,
                            foundSymbol,
                            CmdletToAliasDictionary,
                            AliasToCmdletDictionary)
                        .Select(
                            reference =>
                            {
                                reference.SourceLine =
                                    file.GetLine(reference.ScriptRegion.StartLineNumber);
                                reference.FilePath = file.FilePath;
                                return reference;
                            });

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

            // look through the referenced files until definition is found
            // or there are no more file to look through
            SymbolReference foundDefinition = null;
            for (int i = 0; i < referencedFiles.Length; i++)
            {
                foundDefinition =
                    AstOperations.FindDefinitionOfSymbol(
                        referencedFiles[i].ScriptAst,
                        foundSymbol);

                if (foundDefinition != null)
                {
                    foundDefinition.FilePath = referencedFiles[i].FilePath;
                    break;
                }
            }

            // if definition is not found in referenced files 
            // look for it in the builtin commands
            if (foundDefinition == null)
            {
                CommandInfo cmdInfo = await GetCommandInfo(foundSymbol.SymbolName);

                foundDefinition = 
                    await FindDeclarationForBuiltinCommand(
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
                CommandInfo commandInfo = await GetCommandInfo(foundSymbol.SymbolName);
                if (commandInfo != null)
                {
                    try
                    {
                        IEnumerable<CommandParameterSetInfo> commandParamSets = commandInfo.ParameterSets;
                        return new ParameterSetSignatures(commandParamSets, foundSymbol);
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

        private void ResetMostRecentSignatureParam()
        {
            mostRecentActiveSignature = 0;
            mostRecentActiveParameter = 0;
        }

        /// <summary>
        /// Find the active signature and parameter
        /// </summary>
        /// <param name="paramSetSigs">ParameterSetSignatures of the symbol under consideration</param>
        /// <param name="curSymbol">Symbol at the current cursor</param>
        /// <returns>A 2-tuple of integers such that the first item is the active parameter and the second item is the active signature</returns>
        public Tuple<int?,int?> GetActiveParameterAndSignature(
            ParameterSetSignatures paramSetSigs, string curSymbol)
        {
            if (paramSetSigs == null)
            {
                ResetMostRecentSignatureParam();
            }
            else
            {
                if (mostRecentActiveCommand != paramSetSigs.CommandName)
                {
                    ResetMostRecentSignatureParam();
                    mostRecentActiveCommand = paramSetSigs.CommandName;
                }

                int activeSig = 0;
                bool matchFound = false;
                if (curSymbol != null)
                {
                    foreach (var signature in paramSetSigs.Signatures)
                    {
                        int activeParam = 0;
                        foreach (var parameter in signature.Parameters)
                        {
                            if (parameter.Name.StartsWith(curSymbol, StringComparison.OrdinalIgnoreCase))
                            {
                                mostRecentActiveParameter = activeParam;
                                mostRecentActiveSignature = activeSig;
                                matchFound = true;
                            }
                            activeParam++;
                        }
                        if (matchFound)
                        {
                            break;
                        }
                        activeSig++;
                    }
                }

                // no match found
                // or current symbol is null
                // check if the most recent active parameter is the last one in the current signature.
                // if so, we can expand common parameters or for now set it to null
                if (!matchFound
                    && mostRecentActiveSignature.HasValue
                    && mostRecentActiveSignature < paramSetSigs.Signatures.Length
                    && mostRecentActiveParameter.HasValue
                    && mostRecentActiveParameter.Value == paramSetSigs.Signatures[mostRecentActiveSignature.Value].Parameters.Count() - 1)
                {
                    mostRecentActiveParameter = null;
                }
            }

            Logger.Write(
                LogLevel.Verbose,
                string.Format(
                    "current symbol:{0}; active command:{1}; active parameter:{2}; active signature:{3}",
                    curSymbol ?? "null",
                    mostRecentActiveCommand ?? "null",
                    mostRecentActiveParameter ?? -1,
                    mostRecentActiveSignature ?? -1));

            return new Tuple<int?, int?>(mostRecentActiveParameter, mostRecentActiveSignature);
        }

        #endregion

        #region Private Fields

        /// <summary>
        /// Gets all aliases found in the runspace
        /// </summary>
        private async Task GetAliases()
        {
            if (!this.areAliasesLoaded)
            {
                try
                {
                    RunspaceHandle runspaceHandle =
                        await this.powerShellContext.GetRunspaceHandle(
                            new CancellationTokenSource(DefaultWaitTimeoutMilliseconds).Token);

                    CommandInvocationIntrinsics invokeCommand = runspaceHandle.Runspace.SessionStateProxy.InvokeCommand;
                    IEnumerable<CommandInfo> aliases = invokeCommand.GetCommands("*", CommandTypes.Alias, true);

                    runspaceHandle.Dispose();

                    foreach (AliasInfo aliasInfo in aliases)
                    {
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
                catch (TaskCanceledException)
                {
                    // The wait for a RunspaceHandle has timed out, skip aliases for now
                }
            }
        }

        private async Task<CommandInfo> GetCommandInfo(string commandName)
        {
            PSCommand command = new PSCommand();
            command.AddCommand("Get-Command");
            command.AddArgument(commandName);

            var results = await this.powerShellContext.ExecuteCommand<CommandInfo>(command);
            return results.FirstOrDefault();
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

        private async Task<SymbolReference> FindDeclarationForBuiltinCommand(
            CommandInfo cmdInfo, 
            SymbolReference foundSymbol,
            Workspace workspace)
        {
            SymbolReference foundDefinition = null;
            if (cmdInfo != null)
            {
                int index = 0;
                ScriptFile[] nestedModuleFiles;

                CommandInfo commandInfo = await GetCommandInfo(foundSymbol.SymbolName);

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

        #endregion
    }
}
