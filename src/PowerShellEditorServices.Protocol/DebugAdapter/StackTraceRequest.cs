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
            RequestType<StackTraceRequestArguments, StackTraceResponseBody> Type =
            RequestType<StackTraceRequestArguments, StackTraceResponseBody>.Create("stackTrace");
    }

    [DebuggerDisplay("ThreadId = {ThreadId}, Levels = {Levels}")]
    public class StackTraceRequestArguments
    {
        public int ThreadId { get; private set; }

        /// <summary>
        /// Gets the maximum number of frames to return. If levels is not specified or 0, all frames are returned.
        /// </summary>
        public int Levels { get; private set; }
    }

    public class StackTraceResponseBody
    {
        public StackFrame[] StackFrames { get; set; }
    }
}
