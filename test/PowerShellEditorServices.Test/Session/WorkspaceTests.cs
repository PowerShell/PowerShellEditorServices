// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Xunit;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace PowerShellEditorServices.Test.Session
{
    [Trait("Category", "Workspace")]
    public class WorkspaceTests
    {
        private static readonly Lazy<string> s_lazyDriveLetter = new(() => Path.GetFullPath("\\").Substring(0, 1));

        public static string CurrentDriveLetter => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? s_lazyDriveLetter.Value
            : string.Empty;

        internal static ScriptFile CreateScriptFile(string path) => new(path, "", VersionUtils.PSVersion);


        [Fact]
        public void CanResolveWorkspaceRelativePath()
        {
            string workspacePath = "c:/Test/Workspace/";
            ScriptFile testPathInside = CreateScriptFile("c:/Test/Workspace/SubFolder/FilePath.ps1");
            ScriptFile testPathOutside = CreateScriptFile("c:/Test/PeerPath/FilePath.ps1");
            ScriptFile testPathAnotherDrive = CreateScriptFile("z:/TryAndFindMe/FilePath.ps1");

            WorkspaceService workspace = new(NullLoggerFactory.Instance);

            // Test with zero workspace folders
            Assert.Equal(
                testPathOutside.DocumentUri.ToUri().AbsolutePath,
                workspace.GetRelativePath(testPathOutside));

            string expectedInsidePath = "SubFolder/FilePath.ps1";
            string expectedOutsidePath = "../PeerPath/FilePath.ps1";

            // Test with a single workspace folder
            workspace.WorkspaceFolders.Add(new WorkspaceFolder
            {
                Uri = DocumentUri.FromFileSystemPath(workspacePath)
            });

            Assert.Equal(expectedInsidePath, workspace.GetRelativePath(testPathInside));
            Assert.Equal(expectedOutsidePath, workspace.GetRelativePath(testPathOutside));
            Assert.Equal(
                testPathAnotherDrive.DocumentUri.ToUri().AbsolutePath,
                workspace.GetRelativePath(testPathAnotherDrive));

            // Test with two workspace folders
            string anotherWorkspacePath = "c:/Test/AnotherWorkspace/";
            ScriptFile anotherTestPathInside = CreateScriptFile("c:/Test/AnotherWorkspace/DifferentFolder/FilePath.ps1");
            string anotherExpectedInsidePath = "DifferentFolder/FilePath.ps1";

            workspace.WorkspaceFolders.Add(new WorkspaceFolder
            {
                Uri = DocumentUri.FromFileSystemPath(anotherWorkspacePath)
            });

            Assert.Equal(expectedInsidePath, workspace.GetRelativePath(testPathInside));
            Assert.Equal(anotherExpectedInsidePath, workspace.GetRelativePath(anotherTestPathInside));
        }

        internal static WorkspaceService FixturesWorkspace()
        {
            return new WorkspaceService(NullLoggerFactory.Instance)
            {
                InitialWorkingDirectory = TestUtilities.NormalizePath("Fixtures/Workspace")
            };
        }

        // These are the default values for the EnumeratePSFiles() method
        // in Microsoft.PowerShell.EditorServices.Workspace class
        private static readonly string[] s_defaultExcludeGlobs = Array.Empty<string>();
        private static readonly string[] s_defaultIncludeGlobs = new[] { "**/*" };
        private const int s_defaultMaxDepth = 64;
        private const bool s_defaultIgnoreReparsePoints = false;

        internal static List<string> ExecuteEnumeratePSFiles(
            WorkspaceService workspace,
            string[] excludeGlobs,
            string[] includeGlobs,
            int maxDepth,
            bool ignoreReparsePoints)
        {
            List<string> fileList = new(workspace.EnumeratePSFiles(
                excludeGlobs: excludeGlobs,
                includeGlobs: includeGlobs,
                maxDepth: maxDepth,
                ignoreReparsePoints: ignoreReparsePoints
            ));

            // Assume order is not important from EnumeratePSFiles and sort the array so we can use
            // deterministic asserts
            fileList.Sort();
            return fileList;
        }

        [Fact]
        public void CanRecurseDirectoryTree()
        {
            WorkspaceService workspace = FixturesWorkspace();
            List<string> actual = ExecuteEnumeratePSFiles(
                workspace: workspace,
                excludeGlobs: s_defaultExcludeGlobs,
                includeGlobs: s_defaultIncludeGlobs,
                maxDepth: s_defaultMaxDepth,
                ignoreReparsePoints: s_defaultIgnoreReparsePoints
            );

            List<string> expected = new()
            {
                Path.Combine(workspace.InitialWorkingDirectory, "nested", "donotfind.ps1"),
                Path.Combine(workspace.InitialWorkingDirectory, "nested", "nestedmodule.psd1"),
                Path.Combine(workspace.InitialWorkingDirectory, "nested", "nestedmodule.psm1"),
                Path.Combine(workspace.InitialWorkingDirectory, "rootfile.ps1")
            };

            // .NET Core doesn't appear to use the same three letter pattern matching rule although the docs
            // suggest it should be find the '.ps1xml' files because we search for the pattern '*.ps1'
            // ref https://docs.microsoft.com/en-us/dotnet/api/system.io.directory.getfiles?view=netcore-2.1#System_IO_Directory_GetFiles_System_String_System_String_System_IO_EnumerationOptions_
            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework"))
            {
                expected.Insert(3, Path.Combine(workspace.InitialWorkingDirectory, "other", "other.ps1xml"));
            }

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanRecurseDirectoryTreeWithLimit()
        {
            WorkspaceService workspace = FixturesWorkspace();
            List<string> actual = ExecuteEnumeratePSFiles(
                workspace: workspace,
                excludeGlobs: s_defaultExcludeGlobs,
                includeGlobs: s_defaultIncludeGlobs,
                maxDepth: 1,
                ignoreReparsePoints: s_defaultIgnoreReparsePoints
            );
            Assert.Equal(new[] { Path.Combine(workspace.InitialWorkingDirectory, "rootfile.ps1") }, actual);
        }

        [Fact]
        public void CanRecurseDirectoryTreeWithGlobs()
        {
            WorkspaceService workspace = FixturesWorkspace();
            List<string> actual = ExecuteEnumeratePSFiles(
                workspace: workspace,
                excludeGlobs: new[] { "**/donotfind*" },          // Exclude any files starting with donotfind
                includeGlobs: new[] { "**/*.ps1", "**/*.psd1" }, // Only include PS1 and PSD1 files
                maxDepth: s_defaultMaxDepth,
                ignoreReparsePoints: s_defaultIgnoreReparsePoints
            );

            Assert.Equal(new[] {
                    Path.Combine(workspace.InitialWorkingDirectory, "nested", "nestedmodule.psd1"),
                    Path.Combine(workspace.InitialWorkingDirectory, "rootfile.ps1")
                }, actual);
        }

        [Fact]
        public void CanOpenAndCloseFile()
        {
            WorkspaceService workspace = FixturesWorkspace();
            string filePath = Path.GetFullPath(Path.Combine(workspace.InitialWorkingDirectory, "rootfile.ps1"));

            ScriptFile file = workspace.GetFile(filePath);
            Assert.Equal(workspace.GetOpenedFiles(), new[] { file });

            workspace.CloseFile(file);
            Assert.Equal(workspace.GetOpenedFiles(), Array.Empty<ScriptFile>());
        }
    }
}
