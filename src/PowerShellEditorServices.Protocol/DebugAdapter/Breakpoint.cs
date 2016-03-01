//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class Breakpoint
    {
        /// <summary>
        /// Gets an boolean indicator that if true, breakpoint could be set 
        /// (but not necessarily at the desired location).  
        /// </summary>
        public bool Verified { get; set; }

        /// <summary>
        /// Gets an optional message about the state of the breakpoint. This is shown to the user 
        /// and can be used to explain why a breakpoint could not be verified.
        /// </summary>
        public string Message { get; set; }

        public string Source { get; set; }

        public int? Line { get; set; }

        public int? Column { get; set; }

        private Breakpoint()
        {
        }

        public static Breakpoint Create(
            BreakpointDetails breakpointDetails)
        {
            return new Breakpoint
            {
                Verified = breakpointDetails.Verified,
                Message = breakpointDetails.Message,
                Source = breakpointDetails.Source,
                Line = breakpointDetails.LineNumber,
                Column = breakpointDetails.ColumnNumber
            };
        }

        public static Breakpoint Create(
            FunctionBreakpointDetails breakpointDetails)
        {
            return new Breakpoint {
                Verified = breakpointDetails.Verified,
                Message = breakpointDetails.Message
            };
        }
    }
}
