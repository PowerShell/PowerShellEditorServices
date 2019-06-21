//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.ObjectModel;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    using System.Management.Automation;

    public class RunspaceSynchronizerTests
    {
        [Trait("Category", "RunspaceSynchronizer")]
        [Theory]
        // variable test
        [InlineData("$foo = 'foo'", "$foo", "foo")]
        // module functions test
        [InlineData("Import-Module ../../../../PowerShellEditorServices.Test.Shared/RunspaceSynchronizer/testModule.psm1", "Search-Foo", "success")]
        // module aliases test
        [InlineData("Import-Module ../../../../PowerShellEditorServices.Test.Shared/RunspaceSynchronizer/testModule.psm1", "(Get-Alias sfoo).Definition", "Search-Foo")]
        public void TestRunspaceSynchronizerSyncsData(string sourceScript, string targetScript, object expected)
        {
            using (PowerShell pwshSource = PowerShell.Create())
            using (PowerShell pwshTarget = PowerShell.Create())
            {
                RunspaceSynchronizer.InitializeRunspaces(pwshSource.Runspace, pwshTarget.Runspace);
                AssertExpectedIsSynced(pwshSource, pwshTarget, sourceScript, targetScript, expected);
            }
        }

        [Fact]
        public void TestRunspaceSynchronizerOverwritesTypes()
        {
            using (PowerShell pwshSource = PowerShell.Create())
            using (PowerShell pwshTarget = PowerShell.Create())
            {
                RunspaceSynchronizer.InitializeRunspaces(pwshSource.Runspace, pwshTarget.Runspace);
                AssertExpectedIsSynced(pwshSource, pwshTarget, "$foo = 444", "$foo.GetType().Name", "Int32");
                AssertExpectedIsSynced(pwshSource, pwshTarget, "$foo = 'change to string'", "$foo.GetType().Name", "String");
            }
        }

        private static void AssertExpectedIsSynced(
            PowerShell pwshSource,
            PowerShell pwshTarget,
            string sourceScript,
            string targetScript,
            object expected)
        {
            pwshSource.AddScript(sourceScript).Invoke();
            RunspaceSynchronizer.Activate();

            // We need to allow the event some time to fire.
            System.Threading.Thread.Sleep(1000);

            var results = pwshTarget.AddScript(targetScript).Invoke<PSObject>();

            Assert.Single(results);
            Assert.NotNull(results[0].BaseObject);
            Assert.Equal(expected, results[0].BaseObject);
        }
    }
}
