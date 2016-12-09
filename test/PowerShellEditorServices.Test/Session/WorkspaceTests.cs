//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;
using System;
using System.IO;
using System.Linq;
using Xunit;

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

            Workspace workspace = new Workspace(PowerShellVersion);

            // Test without a workspace path
            Assert.Equal(testPathOutside, workspace.GetRelativePath(testPathOutside));

            // Test with a workspace path
            workspace.WorkspacePath = workspacePath;
            Assert.Equal(@"SubFolder\FilePath.ps1", workspace.GetRelativePath(testPathInside));
            Assert.Equal(@"..\PeerPath\FilePath.ps1", workspace.GetRelativePath(testPathOutside));
            Assert.Equal(testPathAnotherDrive, workspace.GetRelativePath(testPathAnotherDrive));
        }
    }
}
