//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Test.Shared.Completion
{
    public class CompleteFilePath
    {
        public static readonly ScriptRegion SourceDetails = 
            new ScriptRegion
            {
                File = @"Completion\CompletionExamples.psm1",
                StartLineNumber = 19,
                StartColumnNumber = 25
            };

        public static readonly BufferRange ExpectedRange =
            new BufferRange(
                new BufferPosition(19, 15),
                new BufferPosition(19, 25));
    }
}

