//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    public class CompleteAttributeValue
    {
        public static readonly ScriptRegion SourceDetails = 
            new ScriptRegion
            {
                File = @"Completion\CompletionExamples.psm1",
                StartLineNumber = 16,
                StartColumnNumber = 38
            };

        public static readonly BufferRange ExpectedRange =
            new BufferRange(
                new BufferPosition(16, 33),
                new BufferPosition(16, 38));
    }
}

