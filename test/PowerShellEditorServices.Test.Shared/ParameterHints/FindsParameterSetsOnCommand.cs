//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint
{
    public class FindsParameterSetsOnCommand
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = @"ParameterHints\ParamHints.ps1",
                StartLineNumber = 1,
                StartColumnNumber = 14
            };
    }
}
