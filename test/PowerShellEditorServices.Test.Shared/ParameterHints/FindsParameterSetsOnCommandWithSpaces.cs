//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint
{
    public class FindsParameterSetsOnCommandWithSpaces
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = TestUtilities.NormalizePath("ParameterHints/ParamHints.ps1"),
                StartLineNumber = 9,
                StartColumnNumber = 31
            };
    }
}

