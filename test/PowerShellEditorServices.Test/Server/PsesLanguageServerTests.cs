// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.PowerShell.EditorServices.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.Server
{
    [Trait("Category", "Server")]
    public class PsesLanguageServerTests
    {
        [Fact]
        public void GetValidWorkspaceFoldersReturnsEmptyWhenNull()
            => Assert.Empty(PsesLanguageServer.GetValidWorkspaceFolders(null));

        [Fact]
        public void GetValidWorkspaceFoldersReturnsEmptyWhenEmpty()
            => Assert.Empty(PsesLanguageServer.GetValidWorkspaceFolders(new Container<WorkspaceFolder>()));

        [Fact]
        public void GetValidWorkspaceFoldersSkipsNullFoldersAndNullUris()
        {
            WorkspaceFolder valid = new()
            {
                Uri = DocumentUri.FromFileSystemPath("/home/runner/work/example"),
                Name = "workspace"
            };

            Container<WorkspaceFolder> folders = new(
                null,
                new WorkspaceFolder { Name = "missing-uri" },
                valid);

            WorkspaceFolder[] result = PsesLanguageServer.GetValidWorkspaceFolders(folders).ToArray();

            Assert.Equal(valid, Assert.Single(result));
        }
    }
}
