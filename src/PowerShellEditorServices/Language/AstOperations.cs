//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace Microsoft.PowerShell.EditorServices.Language
{
    /// <summary>
    /// Provides common operations for the syntax tree of a parsed script.
    /// </summary>
    public static class AstOperations
    {
        static public CommandCompletion GetCompletions(
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

            return commandCompletion;
        }
    }
}
