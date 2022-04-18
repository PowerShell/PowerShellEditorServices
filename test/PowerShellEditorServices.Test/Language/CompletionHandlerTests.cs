// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
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
            psesHost.StopAsync().Wait();
            GC.SuppressFinalize(this);
        }

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion) => workspace.GetFile(TestUtilities.GetSharedPath(scriptRegion.File));

        private Task<IEnumerable<CompletionItem>> GetCompletionResultsAsync(ScriptRegion scriptRegion)
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
            IEnumerable<CompletionItem> results = await GetCompletionResultsAsync(CompleteCommandInFile.SourceDetails).ConfigureAwait(true);
            CompletionItem actual = Assert.Single(results);
            Assert.Equal(CompleteCommandInFile.ExpectedCompletion, actual);
        }

        [Fact]
        public async Task CompletesCommandFromModule()
        {
            IEnumerable<CompletionItem> results = await GetCompletionResultsAsync(CompleteCommandFromModule.SourceDetails).ConfigureAwait(true);
            CompletionItem actual = Assert.Single(results);
            // NOTE: The tooltip varies across PowerShell and OS versions, so we ignore it.
            Assert.Equal(CompleteCommandFromModule.ExpectedCompletion, actual with { Detail = "" });
            Assert.StartsWith(CompleteCommandFromModule.GetRandomDetail, actual.Detail);
        }

        [Fact]
        public async Task CompletesTypeName()
        {
            IEnumerable<CompletionItem> results = await GetCompletionResultsAsync(CompleteTypeName.SourceDetails).ConfigureAwait(true);
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
                    Detail = "Class System.Collections.ArrayList"
                }, actual);
            }
        }

        [Trait("Category", "Completions")]
        [Fact]
        public async Task CompletesNamespace()
        {
            IEnumerable<CompletionItem> results = await GetCompletionResultsAsync(CompleteNamespace.SourceDetails).ConfigureAwait(true);
            CompletionItem actual = Assert.Single(results);
            Assert.Equal(CompleteNamespace.ExpectedCompletion, actual);
        }

        [Fact]
        public async Task CompletesVariableInFile()
        {
            IEnumerable<CompletionItem> results = await GetCompletionResultsAsync(CompleteVariableInFile.SourceDetails).ConfigureAwait(true);
            CompletionItem actual = Assert.Single(results);
            Assert.Equal(CompleteVariableInFile.ExpectedCompletion, actual);
        }

        [Fact]
        public async Task CompletesAttributeValue()
        {
            IEnumerable<CompletionItem> results = await GetCompletionResultsAsync(CompleteAttributeValue.SourceDetails).ConfigureAwait(true);
            Assert.Collection(results.OrderBy(c => c.SortText),
                actual => Assert.Equal(actual, CompleteAttributeValue.ExpectedCompletion1),
                actual => Assert.Equal(actual, CompleteAttributeValue.ExpectedCompletion2),
                actual => Assert.Equal(actual, CompleteAttributeValue.ExpectedCompletion3));
        }

        [Fact]
        public async Task CompletesFilePath()
        {
            IEnumerable<CompletionItem> results = await GetCompletionResultsAsync(CompleteFilePath.SourceDetails).ConfigureAwait(true);
            Assert.NotEmpty(results);
            CompletionItem actual = results.First();
            // Paths are system dependent so we ignore the text and just check the type and range.
            Assert.Equal(actual.TextEdit.TextEdit with { NewText = "" }, CompleteFilePath.ExpectedEdit);
            Assert.All(results, r => Assert.True(r.Kind is CompletionItemKind.File or CompletionItemKind.Folder));
        }
    }
}
