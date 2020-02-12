//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Definition
{
    public class FindsFunctionDefinition
    {
        public static readonly ScriptRegion SourceDetails =
            new ScriptRegion(
                file: TestUtilities.NormalizePath("References/SimpleFile.ps1"),
                text: string.Empty,
                startLineNumber: 3,
                startColumnNumber: 12,
                startOffset: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                endOffset: 0);
    }
}
