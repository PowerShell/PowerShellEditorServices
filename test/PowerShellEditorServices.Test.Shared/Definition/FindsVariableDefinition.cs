// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Definition
{
    public class FindsVariableDefinition
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion
            {
                File = @"References\SimpleFile.ps1",
                StartLineNumber = 8,
                StartColumnNumber = 3
            };
    }
}
