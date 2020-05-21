//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Session
{
    public class WorkspaceTests
    {
        private static readonly Version PowerShellVersion = new Version("5.0");

        private static readonly Lazy<string> s_lazyDriveLetter = new Lazy<string>(() => Path.GetFullPath("\\").Substring(0, 1));

        public static string CurrentDriveLetter => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? s_lazyDriveLetter.Value
            : string.Empty;

        [Fact]
        [Trait("Category", "Workspace")]
        public void CanResolveWorkspaceRelativePath()
        {
            string workspacePath = TestUtilities.NormalizePath("c:/Test/Workspace/");
            string testPathInside = TestUtilities.NormalizePath("c:/Test/Workspace/SubFolder/FilePath.ps1");
            string testPathOutside = TestUtilities.NormalizePath("c:/Test/PeerPath/FilePath.ps1");
            string testPathAnotherDrive = TestUtilities.NormalizePath("z:/TryAndFindMe/FilePath.ps1");

            WorkspaceService workspace = new WorkspaceService(NullLoggerFactory.Instance);

            // Test without a workspace path
            Assert.Equal(testPathOutside, workspace.GetRelativePath(testPathOutside));

            string expectedInsidePath = TestUtilities.NormalizePath("SubFolder/FilePath.ps1");
            string expectedOutsidePath = TestUtilities.NormalizePath("../PeerPath/FilePath.ps1");

            // Test with a workspace path
            workspace.WorkspacePath = workspacePath;
            Assert.Equal(expectedInsidePath, workspace.GetRelativePath(testPathInside));
            Assert.Equal(expectedOutsidePath, workspace.GetRelativePath(testPathOutside));
            Assert.Equal(testPathAnotherDrive, workspace.GetRelativePath(testPathAnotherDrive));
        }

        internal static WorkspaceService FixturesWorkspace()
        {
            return new WorkspaceService(NullLoggerFactory.Instance) {
                WorkspacePath = TestUtilities.NormalizePath("Fixtures/Workspace")
            };
        }

        // These are the default values for the EnumeratePSFiles() method
        // in Microsoft.PowerShell.EditorServices.Workspace class
        private static string[] s_defaultExcludeGlobs        = new string[0];
        private static string[] s_defaultIncludeGlobs        = new [] { "**/*" };
        private static int      s_defaultMaxDepth            = 64;
        private static bool     s_defaultIgnoreReparsePoints = false;

        internal static List<string> ExecuteEnumeratePSFiles(
            WorkspaceService workspace,
            string[] excludeGlobs,
            string[] includeGlobs,
            int maxDepth,
            bool ignoreReparsePoints
        )
        {
            var result = workspace.EnumeratePSFiles(
                excludeGlobs: excludeGlobs,
                includeGlobs: includeGlobs,
                maxDepth: maxDepth,
                ignoreReparsePoints: ignoreReparsePoints
            );
            var fileList = new List<string>();
            foreach (string file in result) { fileList.Add(file); }
            // Assume order is not important from EnumeratePSFiles and sort the array so we can use deterministic asserts
            fileList.Sort();

            return fileList;
        }

        [Fact]
        [Trait("Category", "Workspace")]
        public void CanRecurseDirectoryTree()
        {
            var workspace = FixturesWorkspace();
            var fileList = ExecuteEnumeratePSFiles(
                workspace: workspace,
                excludeGlobs: s_defaultExcludeGlobs,
                includeGlobs: s_defaultIncludeGlobs,
                maxDepth: s_defaultMaxDepth,
                ignoreReparsePoints: s_defaultIgnoreReparsePoints
            );

            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Core"))
            {
                // .Net Core doesn't appear to use the same three letter pattern matching rule although the docs
                // suggest it should be find the '.ps1xml' files because we search for the pattern '*.ps1'
                // ref https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.getfiles?view=netcore-2.1#System_IO_Directory_GetFiles_System_String_System_String_System_IO_EnumerationOptions_
                Assert.Equal(4, fileList.Count);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"nested") + "/donotfind.ps1", fileList[0]);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"nested") + "/nestedmodule.psd1", fileList[1]);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"nested") + "/nestedmodule.psm1", fileList[2]);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"rootfile.ps1"), fileList[3]);
            }
            else
            {
                Assert.Equal(5, fileList.Count);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"nested") + "/donotfind.ps1", fileList[0]);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"nested") + "/nestedmodule.psd1", fileList[1]);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"nested") + "/nestedmodule.psm1", fileList[2]);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"other") + "/other.ps1xml", fileList[3]);
                Assert.Equal(Path.Combine(workspace.WorkspacePath,"rootfile.ps1"), fileList[4]);
            }
        }

        [Fact]
        [Trait("Category", "Workspace")]
        public void CanRecurseDirectoryTreeWithLimit()
        {
            var workspace = FixturesWorkspace();
            var fileList = ExecuteEnumeratePSFiles(
                workspace: workspace,
                excludeGlobs: s_defaultExcludeGlobs,
                includeGlobs: s_defaultIncludeGlobs,
                maxDepth: 1,
                ignoreReparsePoints: s_defaultIgnoreReparsePoints
            );

            Assert.Equal(1, fileList.Count);
            Assert.Equal(Path.Combine(workspace.WorkspacePath,"rootfile.ps1"), fileList[0]);
        }

        [Fact]
        [Trait("Category", "Workspace")]
        public void CanRecurseDirectoryTreeWithGlobs()
        {
            var workspace = FixturesWorkspace();
            var fileList = ExecuteEnumeratePSFiles(
                workspace: workspace,
                excludeGlobs: new [] {"**/donotfind*"},         // Exclude any files starting with donotfind
                includeGlobs: new [] {"**/*.ps1", "**/*.psd1"}, // Only include PS1 and PSD1 files
                maxDepth: s_defaultMaxDepth,
                ignoreReparsePoints: s_defaultIgnoreReparsePoints
            );

            Assert.Equal(2, fileList.Count);
            Assert.Equal(Path.Combine(workspace.WorkspacePath,"nested") + "/nestedmodule.psd1", fileList[0]);
            Assert.Equal(Path.Combine(workspace.WorkspacePath,"rootfile.ps1"), fileList[1]);
        }

        [Fact]
        [Trait("Category", "Workspace")]
        public void CanDetermineIsPathInMemory()
        {
            string tempDir = Path.GetTempPath();
            string shortDirPath = Path.Combine(tempDir, "GitHub", "PowerShellEditorServices");
            string shortFilePath = Path.Combine(shortDirPath, "foo.ps1");
            string shortUriForm = "git:/c%3A/Users/Keith/GitHub/dahlbyk/posh-git/src/PoshGitTypes.ps1?%7B%22path%22%3A%22c%3A%5C%5CUsers%5C%5CKeith%5C%5CGitHub%5C%5Cdahlbyk%5C%5Cposh-git%5C%5Csrc%5C%5CPoshGitTypes.ps1%22%2C%22ref%22%3A%22~%22%7D";
            string longUriForm = "gitlens-git:c%3A%5CUsers%5CKeith%5CGitHub%5Cdahlbyk%5Cposh-git%5Csrc%5CPoshGitTypes%3Ae0022701.ps1?%7B%22fileName%22%3A%22src%2FPoshGitTypes.ps1%22%2C%22repoPath%22%3A%22c%3A%2FUsers%2FKeith%2FGitHub%2Fdahlbyk%2Fposh-git%22%2C%22sha%22%3A%22e0022701fa12e0bc22d0458673d6443c942b974a%22%7D";

            var testCases = new[] {
                // Test short file absolute paths
                new { IsInMemory = false, Path = shortDirPath },
                new { IsInMemory = false, Path = shortFilePath },
                new { IsInMemory = false, Path = new Uri(shortDirPath).ToString() },
                new { IsInMemory = false, Path = new Uri(shortFilePath).ToString() },

                // Test short file relative paths - not sure we'll ever get these but just in case
                new { IsInMemory = false, Path = "foo.ps1" },
                new { IsInMemory = false, Path = Path.Combine(new [] { "..", "foo.ps1" }) },

                // Test short non-file paths
                new { IsInMemory = true,  Path = "untitled:untitled-1" },
                new { IsInMemory = true,  Path = shortUriForm },
                new { IsInMemory = true, Path = "inmemory://foo.ps1" },

                // Test long non-file path - known to have crashed PSES
                new { IsInMemory = true,  Path = longUriForm },
            };

            foreach (var testCase in testCases)
            {
                Assert.True(
                    WorkspaceService.IsPathInMemory(testCase.Path) == testCase.IsInMemory,
                    $"Testing path {testCase.Path}");
            }
        }
    }
}
