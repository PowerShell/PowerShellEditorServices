//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class VersionUtilsTests
    {
        [Trait("Category", "VersionUtils")]
        [Fact]
        public void IsNetCoreTest()
        {
#if CoreCLR
            Assert.True(VersionUtils.IsNetCore);
#else
            Assert.False(VersionUtils.IsNetCore);
#endif
        }
    }
}
