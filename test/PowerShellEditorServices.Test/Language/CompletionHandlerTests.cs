// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Utility;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    [Trait("Category", "Completions")]
    public class CompletionHandlerTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;
        private readonly WorkspaceService workspace;
        private readonly PsesCompletionHandler completionHandler;

        public CompletionHandlerTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
            completionHandler = new PsesCompletionHandler(NullLoggerFactory.Instance, psesHost, psesHost, workspace);
        }

        public void Dispose()
        {
            psesHost.StopAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion) => workspace.GetFile(TestUtilities.GetSharedPath(scriptRegion.File));

        private async Task<CompletionResults> GetCompletionResults(ScriptRegion scriptRegion)
        {
            return await completionHandler.GetCompletionsInFileAsync(
                GetScriptFile(scriptRegion),
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber).ConfigureAwait(true);
        }

        [Fact]
        public async Task CompletesCommandInFile()
        {
            CompletionResults completionResults = await GetCompletionResults(CompleteCommandInFile.SourceDetails).ConfigureAwait(true);
            Assert.NotEmpty(completionResults.Completions);
            Assert.Equal(CompleteCommandInFile.ExpectedCompletion, completionResults.Completions[0]);
        }

        [Fact]
        public async Task CompletesCommandFromModule()
        {
            CompletionResults completionResults = await GetCompletionResults(CompleteCommandFromModule.SourceDetails).ConfigureAwait(true);

            Assert.NotEmpty(completionResults.Completions);

            Assert.Equal(
                CompleteCommandFromModule.ExpectedCompletion.CompletionText,
                completionResults.Completions[0].CompletionText);

            Assert.Equal(
                CompleteCommandFromModule.ExpectedCompletion.CompletionType,
                completionResults.Completions[0].CompletionType);

            Assert.NotNull(completionResults.Completions[0].ToolTipText);
        }

        [SkippableFact]
        public async Task CompletesTypeName()
        {
            Skip.If(
                !VersionUtils.IsNetCore,
                "In Windows PowerShell the CommandCompletion fails in the test harness, but works manually.");

            CompletionResults completionResults = await GetCompletionResults(CompleteTypeName.SourceDetails).ConfigureAwait(true);

            Assert.NotEmpty(completionResults.Completions);

            Assert.Equal(
                CompleteTypeName.ExpectedCompletion.CompletionText,
                completionResults.Completions[0].CompletionText);

            Assert.Equal(
                CompleteTypeName.ExpectedCompletion.CompletionType,
                completionResults.Completions[0].CompletionType);

            Assert.NotNull(completionResults.Completions[0].ToolTipText);
        }

        [Trait("Category", "Completions")]
        [SkippableFact]
        public async Task CompletesNamespace()
        {
            Skip.If(
                !VersionUtils.IsNetCore,
                "In Windows PowerShell the CommandCompletion fails in the test harness, but works manually.");

            CompletionResults completionResults = await GetCompletionResults(CompleteNamespace.SourceDetails).ConfigureAwait(true);

            Assert.NotEmpty(completionResults.Completions);

            Assert.Equal(
                CompleteNamespace.ExpectedCompletion.CompletionText,
                completionResults.Completions[0].CompletionText);

            Assert.Equal(
                CompleteNamespace.ExpectedCompletion.CompletionType,
                completionResults.Completions[0].CompletionType);

            Assert.NotNull(completionResults.Completions[0].ToolTipText);
        }

        [Fact]
        public async Task CompletesVariableInFile()
        {
            CompletionResults completionResults = await GetCompletionResults(CompleteVariableInFile.SourceDetails).ConfigureAwait(true);

            Assert.Single(completionResults.Completions);

            Assert.Equal(
                CompleteVariableInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public async Task CompletesAttributeValue()
        {
            CompletionResults completionResults = await GetCompletionResults(CompleteAttributeValue.SourceDetails).ConfigureAwait(true);

            Assert.NotEmpty(completionResults.Completions);

            Assert.Equal(
                CompleteAttributeValue.ExpectedRange,
                completionResults.ReplacedRange);
        }

        [Fact]
        public async Task CompletesFilePath()
        {
            CompletionResults completionResults = await GetCompletionResults(CompleteFilePath.SourceDetails).ConfigureAwait(true);

            Assert.NotEmpty(completionResults.Completions);

            // TODO: Since this is a path completion, this test will need to be
            //       platform specific. Probably something like:
            //         - Windows: C:\Program
            //         - macOS:   /User
            //         - Linux:   /hom
            //Assert.Equal(
            //    CompleteFilePath.ExpectedRange,
            //    completionResults.ReplacedRange);
        }
    }
}
