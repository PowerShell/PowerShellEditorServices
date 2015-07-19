//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Test.Utility;
using System;
using System.IO;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using Xunit;

namespace PSLanguageService.Test
{
    public class LanguageServiceTests : IDisposable
    {
        private Runspace languageServiceRunspace;
        private LanguageService languageService;

        public LanguageServiceTests()
        {
            this.languageServiceRunspace = RunspaceFactory.CreateRunspace();
            this.languageServiceRunspace.ApartmentState = ApartmentState.STA;
            this.languageServiceRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
            this.languageServiceRunspace.Open();

            this.languageService = new LanguageService(this.languageServiceRunspace);
        }

        public void Dispose()
        {
            this.languageServiceRunspace.Dispose();
        }

        [Fact]
        public void LanguageServiceCompletesFunctionInSameFile()
        {
            //CompletionResults completionResults =
            //    this.languageService.GetCompletionsInFile(
            //        this.LoadScript("CompleteFunctionName.ps1"),


            Assert.False(true);
        }

        [Fact]
        public void LanguageServiceCompletesVariableInSameFile()
        {
            //AstOperations.GetCompletions()
            Assert.False(true);
        }

        private ScriptFile LoadScript(string filePath)
        {
            // Load the file from resources
            TextReader textReader =
                ResourceFileLoader.LoadFileFromResource(
                    filePath);

            // Load the script file and get its syntax tree
            return new ScriptFile(filePath, textReader);
        }
    }
}
