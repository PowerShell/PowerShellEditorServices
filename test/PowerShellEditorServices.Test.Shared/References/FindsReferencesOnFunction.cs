//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Test.Shared.References
{
    public class FindsReferencesOnFunction
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = TestUtilities.NormalizePath("References/SimpleFile.ps1"),
                StartLineNumber = 3,
                StartColumnNumber = 8
            };
    }
}

