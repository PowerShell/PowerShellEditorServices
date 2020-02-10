//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.DebugAdapter
{
    /// <summary>
    /// Provides details about a breakpoint that is set in the
    /// PowerShell debugger.
    /// </summary>
    internal abstract class BreakpointDetailsBase
    {
        /// <summary>
        /// Gets or sets a boolean indicator that if true, breakpoint could be set
        /// (but not necessarily at the desired location).
        /// </summary>
        public bool Verified { get; set; }

        /// <summary>
        /// Gets or set an optional message about the state of the breakpoint. This is shown to the user
        /// and can be used to explain why a breakpoint could not be verified.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the breakpoint condition string.
        /// </summary>
        public string Condition { get; protected set; }

        /// <summary>
        /// Gets the breakpoint hit condition string.
        /// </summary>
        public string HitCondition { get; protected set; }
    }
}
