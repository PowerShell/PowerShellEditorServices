//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using System.Management.Automation.Language;
using Xunit;

namespace PSLanguageService.Test
{
    public class AstOperationTests
    {
        [Fact]
        public void FindsCompletionForScriptFunction()
        {
            //ScriptFile scriptFile = new ScriptFile("");

            //AstOperations.GetCompletions(
            //    scriptFile.ScriptAst,
            //    scriptFile.ScriptTokens,
            //    235246246,
            //    );

            Assert.False(true);
        }

        [Fact]
        public void FindsCompletionForScriptVariable()
        {
            //AstOperations.GetCompletions()
            Assert.False(true);
        }

        private Ast GetAstForFile(string filePath)
        {
            ScriptFile file = new ScriptFile(filePath);
            return file.ScriptAst;
        }
    }
}
