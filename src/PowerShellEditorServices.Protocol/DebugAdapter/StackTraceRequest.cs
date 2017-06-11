//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class StackTraceRequest
    {
        public static readonly
            RequestType<StackTraceRequestArguments, StackTraceResponseBody, object, object> Type =
            RequestType<StackTraceRequestArguments, StackTraceResponseBody, object, object>.Create("stackTrace");
    }

    [DebuggerDisplay("ThreadId = {ThreadId}, Levels = {Levels}")]
    public class StackTraceRequestArguments
    {
        /// <summary>
        /// Gets or sets the ThreadId of this stacktrace.
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// Gets or sets the index of the first frame to return. If omitted frames start at 0.
        /// </summary>
        public int? StartFrame { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of frames to return. If levels is not specified or 0, all frames are returned.
        /// </summary>
        public int? Levels { get; set; }

        /// <summary>
        /// Gets or sets the format string that specifies details on how to format the stack frames.
        /// </summary>
        public string Format { get; set; }
    }

    public class StackTraceResponseBody
    {
        /// <summary>
        /// Gets the frames of the stackframe. If the array has length zero, there are no stackframes available.
        /// This means that there is no location information available.
        /// </summary>
        public StackFrame[] StackFrames { get; set; }

        /// <summary>
        /// Gets the total number of frames available.
        /// </summary>
        public int? TotalFrames { get; set; }
    }
}
