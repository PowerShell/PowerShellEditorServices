//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Test.Shared.Utility;
using System;
using System.Management.Automation.Runspaces;
using System.Threading;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class LanguageServiceTests : IDisposable
    {
        private ResourceFileLoader fileLoader;
        private Runspace languageServiceRunspace;
        private LanguageService languageService;

        public LanguageServiceTests()
        {
            // Load script files from the shared assembly
            this.fileLoader =
                new ResourceFileLoader(
                    typeof(CompleteCommandInFile).Assembly);

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
        public void LanguageServiceCompletesCommandInFile()
        {
            CompletionResults completionResults =
                this.GetCompletionResults(
                    CompleteCommandInFile.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact(Skip = "This test does not run correctly on AppVeyor, need to investigate.")]
        public void LanguageServiceCompletesCommandFromModule()
        {
            CompletionResults completionResults =
                this.GetCompletionResults(
                    CompleteCommandFromModule.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandFromModule.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public void LanguageServiceCompletesVariableInFile()
        {
            CompletionResults completionResults =
                this.GetCompletionResults(
                    CompleteVariableInFile.SourceDetails);

            Assert.Equal(1, completionResults.Completions.Length);
            Assert.Equal(
                CompleteVariableInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        private CompletionResults GetCompletionResults(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile =
                this.fileLoader.LoadFile(
                    scriptRegion.File);

            // Run the completions request
            return
                this.languageService.GetCompletionsInFile(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }
    }
}
