// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Utility;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class VersionUtilsTests
    {
        [Trait("Category", "VersionUtils")]
        [Fact]
        public void IsNetCoreTest() =>
#if CoreCLR
            Assert.True(VersionUtils.IsNetCore);
#else
            Assert.False(VersionUtils.IsNetCore);
#endif

    }
}
