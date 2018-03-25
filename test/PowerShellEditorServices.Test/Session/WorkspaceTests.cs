﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Test.Session
{
    public class WorkspaceTests
    {
        private static readonly Version PowerShellVersion = new Version("5.0");

        [Fact]
        public void CanResolveWorkspaceRelativePath()
        {
            string workspacePath = @"c:\Test\Workspace\";
            string testPathInside = @"c:\Test\Workspace\SubFolder\FilePath.ps1";
            string testPathOutside = @"c:\Test\PeerPath\FilePath.ps1";
            string testPathAnotherDrive = @"z:\TryAndFindMe\FilePath.ps1";

            Workspace workspace = new Workspace(PowerShellVersion, new NullLogger());

            // Test without a workspace path
            Assert.Equal(testPathOutside, workspace.GetRelativePath(testPathOutside));

            // Test with a workspace path
            workspace.WorkspacePath = workspacePath;
            Assert.Equal(@"SubFolder\FilePath.ps1", workspace.GetRelativePath(testPathInside));
            Assert.Equal(@"..\PeerPath\FilePath.ps1", workspace.GetRelativePath(testPathOutside));
            Assert.Equal(testPathAnotherDrive, workspace.GetRelativePath(testPathAnotherDrive));
        }

        [Fact]
        public void CanDetermineIsPathInMemory()
        {
            var tempDir = Environment.GetEnvironmentVariable("TEMP");

            var shortDirPath = Path.Combine(tempDir, "GitHub", "PowerShellEditorServices");
            var shortFilePath = Path.Combine(shortDirPath, "foo.ps1");
            var shortDirUriForm = new Uri(shortDirPath).ToString();
            var shortFileUriForm = new Uri(shortFilePath).ToString();

            // Test short file absolute paths
            Assert.False(Workspace.IsPathInMemory(shortDirPath));
            Assert.False(Workspace.IsPathInMemory(shortFilePath));
            Assert.False(Workspace.IsPathInMemory(shortDirUriForm));
            Assert.False(Workspace.IsPathInMemory(shortFileUriForm));

            // Test short file relative paths - not sure we'll ever get these but just in case
            Assert.False(Workspace.IsPathInMemory("foo.ps1"));
            Assert.False(Workspace.IsPathInMemory(".." + Path.DirectorySeparatorChar + "foo.ps1"));

            // Test short non-file paths
            Assert.True(Workspace.IsPathInMemory("untitled:untitled-1"));
            var shortUriForm = "git:/c%3A/Users/Keith/GitHub/dahlbyk/posh-git/src/PoshGitTypes.ps1?%7B%22path%22%3A%22c%3A%5C%5CUsers%5C%5CKeith%5C%5CGitHub%5C%5Cdahlbyk%5C%5Cposh-git%5C%5Csrc%5C%5CPoshGitTypes.ps1%22%2C%22ref%22%3A%22~%22%7D";
            Assert.True(Workspace.IsPathInMemory(shortUriForm));

            // Test long non-file path - known to have crashed PSES
            var longUriForm = "gitlens-git:c%3A%5CUsers%5CKeith%5CGitHub%5Cdahlbyk%5Cposh-git%5Csrc%5CPoshGitTypes%3Ae0022701.ps1?%7B%22fileName%22%3A%22src%2FPoshGitTypes.ps1%22%2C%22repoPath%22%3A%22c%3A%2FUsers%2FKeith%2FGitHub%2Fdahlbyk%2Fposh-git%22%2C%22sha%22%3A%22e0022701fa12e0bc22d0458673d6443c942b974a%22%7D";
            Assert.True(Workspace.IsPathInMemory(longUriForm));
        }
    }
}
