//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Language
{
    /// <summary>
    /// Provides common operations for the syntax tree of a parsed script.
    /// </summary>
    internal static class AstOperations
    {
        /// <summary>
        /// Gets completions for the symbol found in the Ast at 
        /// the given file offset.
        /// </summary>
        /// <param name="scriptAst">
        /// The Ast which will be traversed to find a completable symbol.
        /// </param>
        /// <param name="currentTokens">
        /// The array of tokens corresponding to the scriptAst parameter.
        /// </param>
        /// <param name="fileOffset">
        /// The 1-based file offset at which a symbol will be located.
        /// </param>
        /// <param name="runspace">
        /// The Runspace to use for gathering completions.
        /// </param>
        /// <returns>
        /// A CommandCompletion instance that contains completions for the
        /// symbol at the given offset.
        /// </returns>
        static public CompletionResults GetCompletions(
            Ast scriptAst, 
            Token[] currentTokens, 
            int fileOffset, 
            Runspace runspace)
        {
            var type = scriptAst.Extent.StartScriptPosition.GetType();
            var method = 
                type.GetMethod(
                    "CloneWithNewOffset",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int) }, null);

            IScriptPosition cursorPosition = 
                (IScriptPosition)method.Invoke(
                    scriptAst.Extent.StartScriptPosition, 
                    new object[] { fileOffset });

            CommandCompletion commandCompletion = null;
            if (runspace.RunspaceAvailability == RunspaceAvailability.Available)
            {
                using (System.Management.Automation.PowerShell powerShell = 
                        System.Management.Automation.PowerShell.Create())
                {
                    powerShell.Runspace = runspace;

                    commandCompletion = 
                        CommandCompletion.CompleteInput(
                            scriptAst, 
                            currentTokens, 
                            cursorPosition, 
                            null, 
                            powerShell); 
                }
            }

            return CompletionResults.Create(commandCompletion);
        }
    }
}
