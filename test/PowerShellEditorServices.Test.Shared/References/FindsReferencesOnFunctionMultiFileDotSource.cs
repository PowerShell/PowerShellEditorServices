// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.References
{
    public class FindsReferencesOnFunctionMultiFileDotSourceFileB
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = @"References\ReferenceFileB.ps1",
                StartLineNumber = 5,
                StartColumnNumber = 8
            };
    }
    public class FindsReferencesOnFunctionMultiFileDotSourceFileC
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = @"References\ReferenceFileC.ps1",
                StartLineNumber = 4,
                StartColumnNumber = 10
            };
    }
}
