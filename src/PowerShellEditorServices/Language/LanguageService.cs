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

    public class LanguageService
    {
        private Runspace runspace;

        public LanguageService(Runspace languageServiceRunspace)
        {
            Validate.IsNotNull("languageServiceRunspace", languageServiceRunspace);

            this.runspace = languageServiceRunspace;
        }

        public CommandCompletion GetCompletionsInFile(
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

            CommandCompletion completionSuggestions = 
                AstOperations.GetCompletions(
                    scriptFile.ScriptAst,
                    scriptFile.ScriptTokens,
                    fileOffset,
                    this.runspace);

            return completionSuggestions;
        }
    }
}
