// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.Language
{
    [Trait("Category", "Completions")]
    public class CompletionHandlerTests : IAsyncLifetime
    {
        private PsesInternalHost psesHost;
        private WorkspaceService workspace;
        private PsesCompletionHandler completionHandler;

        public async Task InitializeAsync()
        {
            psesHost = await PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
            completionHandler = new PsesCompletionHandler(NullLoggerFactory.Instance, psesHost, psesHost, workspace);
        }

        public async Task DisposeAsync() => await Task.Run(psesHost.StopAsync);

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion) => workspace.GetFile(TestUtilities.GetSharedPath(scriptRegion.File));

        private Task<CompletionResults> GetCompletionResultsAsync(ScriptRegion scriptRegion)
        {
            return completionHandler.GetCompletionsInFileAsync(
                GetScriptFile(scriptRegion),
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber,
                CancellationToken.None);
        }

        [Fact]
        public async Task CompletesCommandInFile()
        {
            (_, IEnumerable<CompletionItem> results) = await GetCompletionResultsAsync(CompleteCommandInFile.SourceDetails);
            CompletionItem actual = Assert.Single(results);
            Assert.Equal(CompleteCommandInFile.ExpectedCompletion, actual);
        }

        [Fact]
        public async Task CompletesCommandFromModule()
        {
            (_, IEnumerable<CompletionItem> results) = await GetCompletionResultsAsync(CompleteCommandFromModule.SourceDetails);
            CompletionItem actual = Assert.Single(results);
            // NOTE: The tooltip varies across PowerShell and OS versions, so we ignore it.
            Assert.Equal(CompleteCommandFromModule.ExpectedCompletion, actual with { Detail = "" });
            Assert.StartsWith(CompleteCommandFromModule.GetRandomDetail, actual.Detail);
        }

        [SkippableFact]
        public async Task CompletesTypeName()
        {
            Skip.If(VersionUtils.PSEdition == "Desktop", "Windows PowerShell has trouble with this test right now.");
            (_, IEnumerable<CompletionItem> results) = await GetCompletionResultsAsync(CompleteTypeName.SourceDetails);
            CompletionItem actual = Assert.Single(results);
            if (VersionUtils.IsNetCore)
            {
                Assert.Equal(CompleteTypeName.ExpectedCompletion, actual);
            }
            else
            {
                // Windows PowerShell shows ArrayList as a Class.
                Assert.Equal(CompleteTypeName.ExpectedCompletion with
                {
                    Kind = CompletionItemKind.Class,
                    Detail = "System.Collections.ArrayList"
                }, actual);
            }
        }

        [SkippableFact]
        public async Task CompletesNamespace()
        {
            Skip.If(VersionUtils.PSEdition == "Desktop", "Windows PowerShell has trouble with this test right now.");
            (_, IEnumerable<CompletionItem> results) = await GetCompletionResultsAsync(CompleteNamespace.SourceDetails);
            CompletionItem actual = Assert.Single(results);
            Assert.Equal(CompleteNamespace.ExpectedCompletion, actual);
        }

        [Fact]
        public async Task CompletesVariableInFile()
        {
            (_, IEnumerable<CompletionItem> results) = await GetCompletionResultsAsync(CompleteVariableInFile.SourceDetails);
            CompletionItem actual = Assert.Single(results);
            Assert.Equal(CompleteVariableInFile.ExpectedCompletion, actual);
        }

        [Fact]
        public async Task CompletesAttributeValue()
        {
            (_, IEnumerable<CompletionItem> results) = await GetCompletionResultsAsync(CompleteAttributeValue.SourceDetails);
            // NOTE: Since the completions come through un-ordered from PowerShell, their SortText
            // (which has an index prepended from the original order) will mis-match our assumed
            // order; hence we ignore it.
            Assert.Collection(results.OrderBy(c => c.Label),
                actual => Assert.Equal(actual with { Data = null, SortText = null }, CompleteAttributeValue.ExpectedCompletion1),
                actual => Assert.Equal(actual with { Data = null, SortText = null }, CompleteAttributeValue.ExpectedCompletion2),
                actual => Assert.Equal(actual with { Data = null, SortText = null }, CompleteAttributeValue.ExpectedCompletion3));
        }

        [Fact]
        public async Task CompletesFilePath()
        {
            (_, IEnumerable<CompletionItem> results) = await GetCompletionResultsAsync(CompleteFilePath.SourceDetails);
            Assert.NotEmpty(results);
            CompletionItem actual = results.First();
            // Paths are system dependent so we ignore the text and just check the type and range.
            Assert.Equal(actual.TextEdit.TextEdit with { NewText = "" }, CompleteFilePath.ExpectedEdit);
            Assert.All(results, r => Assert.True(r.Kind is CompletionItemKind.File or CompletionItemKind.Folder));
        }

        // TODO: These should be an integration tests at a higher level if/when https://github.com/PowerShell/PowerShell/pull/25108 is merged. As of today, we can't actually test this in the PS engine currently.
        [Fact]
        public void CanExtractTypeAndDescriptionFromTooltip()
        {
            string expectedType = "[string]";
            string expectedDescription = "Test String";
            string paramName = "TestParam";
            string testHelp = $"{expectedType} {paramName} - {expectedDescription}";
            Assert.True(PsesCompletionHandler.TryExtractType(testHelp, paramName, out string type, out string description));
            Assert.Equal(expectedType, type);
            Assert.Equal(expectedDescription, description);
        }

        [Fact]
        public void CanExtractTypeFromTooltip()
        {
            string expectedType = "[string]";
            string testHelp = $"{expectedType}";
            Assert.True(PsesCompletionHandler.TryExtractType(testHelp, string.Empty, out string type, out string description));
            Assert.Null(description);
            Assert.Equal(expectedType, type);
        }
    }
}
