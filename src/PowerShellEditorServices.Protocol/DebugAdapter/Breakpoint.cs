//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class Breakpoint
    {
        public bool Verified { get; set; }

        public int Line { get; set; }

        private Breakpoint()
        {
        }

        public static Breakpoint Create(
            BreakpointDetails breakpointDetails)
        {
            return new Breakpoint
            {
                Line = breakpointDetails.LineNumber,
                Verified = true
            };
        }
    }
}
