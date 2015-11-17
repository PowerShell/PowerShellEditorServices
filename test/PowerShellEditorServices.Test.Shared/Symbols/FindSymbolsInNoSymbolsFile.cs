// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Symbols
{
    public class FindSymbolsInNoSymbolsFile
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion {
                File = @"Symbols\NoSymbols.ps1"
            };
    }
}
