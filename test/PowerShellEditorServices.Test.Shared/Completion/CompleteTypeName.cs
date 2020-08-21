//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    internal class CompleteTypeName
    {
        private static readonly string[] s_systemcollectiontypes = {
            "System.Collections.ArrayList",
            "System.Collections.BitArray",
            "System.Collections.CaseInsensitiveComparer",
            "System.Collections.CaseInsensitiveHashCodeProvider"
        }

        public static readonly scriptregion SourceDetails =
            new scriptregion(
                file: TestUtilities.NormalizePath("Completion/CompletionExamples.psm1"),
                text: string.Empty,
                startLineNumber: 21,
                startColumnNumber: 21,
                startOffset: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                endOffset: 0);

        public static readonly CompletionDetails ExpectedCompletion =
            CompletionDetails.Create(
                "System.Collections.ArrayList",
                CompletionType.Type,
                string.join(Environment.NewLine + Environment.NewLine, s_systemcollectiontypes)
            )
}
