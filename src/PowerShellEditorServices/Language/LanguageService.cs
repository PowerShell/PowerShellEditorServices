//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Language
{
    using Microsoft.PowerShell.EditorServices.Utility;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    /// <summary>
    /// Provides a high-level service for performing code completion and
    /// navigation operations on PowerShell scripts.
    /// </summary>
    public class LanguageService
    {
        #region Private Fields

        private Runspace runspace;
        private CompletionResults mostRecentCompletions;
        private int mostRecentRequestLine;
        private int mostRecentRequestOffest;
        private string mostRecentRequestFile;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of the LanguageService class and uses
        /// the given Runspace to execute language service operations.
        /// </summary>
        /// <param name="languageServiceRunspace">
        /// The Runspace in which language service operations will be executed.
        /// </param>
        public LanguageService(Runspace languageServiceRunspace)
        {
            Validate.IsNotNull("languageServiceRunspace", languageServiceRunspace);

            this.runspace = languageServiceRunspace;
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
        public CompletionResults GetCompletionsInFile(
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

            CompletionResults completionResults =
                AstOperations.GetCompletions(
                    scriptFile.ScriptAst,
                    scriptFile.ScriptTokens,
                    fileOffset,
                    this.runspace);
                    
            // save state of most recent completion
            mostRecentCompletions = completionResults;
            mostRecentRequestFile = scriptFile.FilePath;
            mostRecentRequestLine = lineNumber;
            mostRecentRequestOffest = columnNumber;

            return completionResults;
        }

        /// <summary>
        /// Finds command completion details for the script given a file location 
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <param name="entryName">The name of the suggestion that needs details</param>
        /// <returns>CompletionResult object (contains information about the command completion)</returns>
        public CompletionDetails GetCompletionDetailsInFile(
            ScriptFile file,
            int lineNumber,
            int columnNumber,
            string entryName)
        {
            if (file.FilePath.Equals(mostRecentRequestFile) &&
                lineNumber == mostRecentRequestLine &&
                columnNumber == mostRecentRequestOffest)
            {
                CompletionDetails completionResult = 
                    mostRecentCompletions.Completions.First(
                        result => result.CompletionText.Equals(entryName));
                return completionResult;
            }
            else { return null; }
        }

        /// <summary>
        /// Finds the symbol in the script given a file location
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>A SymbolReference of the symbol found at the given location
        /// or null if there is no symbol at that location 
        /// </returns>
        public SymbolReference FindSymbolAtLocation(
            ScriptFile file,
            int lineNumber,
            int columnNumber)
        {
            return
                AstOperations.FindSymbolAtPosition(
                    file.ScriptAst,
                    lineNumber,
                    columnNumber);
        }

        /// <summary>
        /// Finds all the references of a symbol
        /// </summary>
        /// <param name="foundSymbol">The symbol to find all references for</param>
        /// <param name="referencedFiles">An array of scriptFiles too search for references in</param>
        /// <returns>FindReferencesResult</returns>
        public FindReferencesResult FindReferencesOfSymbol(
            SymbolReference foundSymbol,
            ScriptFile[] referencedFiles)
        {                
            if (foundSymbol != null)
            {
                int symbolOffset = referencedFiles[0].GetOffsetAtPosition(
                    foundSymbol.ScriptRegion.StartLineNumber,
                    foundSymbol.ScriptRegion.StartColumnNumber);
                List<SymbolReference> symbolReferences = new List<SymbolReference>();

                foreach (ScriptFile file in referencedFiles)
                {
                    IEnumerable<SymbolReference> symbolReferencesinFile =
                    AstOperations
                        .FindReferencesOfSymbol(
                            file.ScriptAst,
                            foundSymbol)
                        .Select(
                            reference =>
                            {
                                reference.SourceLine =
                                    file.GetLine(reference.ScriptRegion.StartLineNumber);
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
        /// Finds the definition of a symbol in the script 
        /// </summary>
        /// <param name="foundSymbol">The symbol to find a definition for</param>
        /// <param name="referencedFiles">An array of scriptFiles too search for the definition in</param>
        /// <returns>GetDefinitionResult</returns>
        public GetDefinitionResult GetDefinitionOfSymbol(
            SymbolReference foundSymbol,
            ScriptFile[] referencedFiles)
        {
            if (foundSymbol != null)
            {
                SymbolReference foundDefinition = null;
                int index = 0;
                while (foundDefinition == null && index < referencedFiles.Length)
                {
                    foundDefinition =
                        AstOperations.FindDefinitionOfSymbol(
                            referencedFiles[index].ScriptAst,
                            foundSymbol);
                    index++;
                }
                return new GetDefinitionResult(foundDefinition);
            }
            else { return null; }
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
                IEnumerable<SymbolReference> symbolOccurrences =
                    AstOperations
                        .FindReferencesOfSymbol(
                            file.ScriptAst,
                            foundSymbol);

                return
                    new FindOccurrencesResult
                    {
                        FoundOccurrences = symbolOccurrences
                    };
            }
            else { return null; }
        }

        /// <summary>
        /// Finds the parameter set hints of a specific command (determined by a given file location)
        /// </summary>
        /// <param name="file">The details and contents of a open script file</param>
        /// <param name="lineNumber">The line number of the cursor for the given script</param>
        /// <param name="columnNumber">The coulumn number of the cursor for the given script</param>
        /// <returns>ParameterSetSignatures</returns>
        public ParameterSetSignatures FindParameterSetsInFile(
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
                if (GetCommandInfo(foundSymbol.SymbolName) != null)
                {
                    IEnumerable<CommandParameterSetInfo> commandInfo = 
                        GetCommandInfo(foundSymbol.SymbolName).ParameterSets;
                    List<CommandParameterSetInfo> commandInfoSet = 
                        new List<CommandParameterSetInfo>(commandInfo);

                    return new ParameterSetSignatures(commandInfoSet, foundSymbol);
                }
                else { return null; }
            }
            else { return null; }
        }
        
        #endregion

        private CommandInfo GetCommandInfo(string commandName)
        {
            CommandInfo commandInfo = null;

            using (PowerShell powerShell = PowerShell.Create())
            {
                powerShell.Runspace = this.runspace;
                powerShell.AddCommand("Get-Command");
                powerShell.AddArgument(commandName);
                commandInfo = powerShell.Invoke<CommandInfo>().FirstOrDefault();
            }

            return commandInfo;
        }
    }
}
