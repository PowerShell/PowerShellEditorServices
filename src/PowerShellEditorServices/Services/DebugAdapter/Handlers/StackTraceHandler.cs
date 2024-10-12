// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using System.Linq;

namespace Microsoft.PowerShell.EditorServices.Handlers;

internal class StackTraceHandler(DebugService debugService) : IStackTraceHandler
{
    /// <summary>
    /// Because we don't know the size of the stacktrace beforehand, we will tell the client that there are more frames available, this is effectively a paging size, as the client should request this many frames after the first one.
    /// </summary>
    private const int INITIAL_PAGE_SIZE = 20;

    public async Task<StackTraceResponse> Handle(StackTraceArguments request, CancellationToken cancellationToken)
    {
        if (!debugService.IsDebuggerStopped)
        {
            throw new NotSupportedException("Stacktrace was requested while we are not stopped at a breakpoint.");
        }

        // Adapting to int to let us use LINQ, realistically if you have a stacktrace larger than this that the client is requesting, you have bigger problems...
        int skip = Convert.ToInt32(request.StartFrame ?? 0);
        int take = Convert.ToInt32(request.Levels ?? 0);

        // We generate a label for the breakpoint and can return that immediately if the client is supporting DelayedStackTraceLoading.
        InvocationInfo invocationInfo = debugService.CurrentDebuggerStoppedEventArgs?.OriginalEvent?.InvocationInfo
            ?? throw new NotSupportedException("InvocationInfo was not available on CurrentDebuggerStoppedEvent args. This is a bug.");

        StackFrame breakpointLabel = CreateBreakpointLabel(invocationInfo);

        if (skip == 0 && take == 1) // This indicates the client is doing an initial fetch, so we want to return quickly to unblock the UI and wait on the remaining stack frames for the subsequent requests.
        {
            return new StackTraceResponse()
            {
                StackFrames = new StackFrame[] { breakpointLabel },
                TotalFrames = INITIAL_PAGE_SIZE //Indicate to the client that there are more frames available
            };
        }

        // Wait until the stack frames and variables have been fetched.
        await debugService.StackFramesAndVariablesFetched.ConfigureAwait(false);

        StackFrameDetails[] stackFrameDetails = await debugService.GetStackFramesAsync(cancellationToken)
                                                                    .ConfigureAwait(false);

        // Handle a rare race condition where the adapter requests stack frames before they've
        // begun building.
        if (stackFrameDetails is null)
        {
            return new StackTraceResponse
            {
                StackFrames = Array.Empty<StackFrame>(),
                TotalFrames = 0
            };
        }

        List<StackFrame> newStackFrames = new();
        if (skip == 0)
        {
            newStackFrames.Add(breakpointLabel);
        }

        newStackFrames.AddRange(
            stackFrameDetails
            .Skip(skip != 0 ? skip - 1 : skip)
            .Take(take != 0 ? take - 1 : take)
            .Select((frame, index) => CreateStackFrame(frame, index + 1))
        );

        return new StackTraceResponse
        {
            StackFrames = newStackFrames,
            TotalFrames = newStackFrames.Count
        };
    }

    public static StackFrame CreateStackFrame(StackFrameDetails stackFrame, long id)
    {
        SourcePresentationHint sourcePresentationHint =
            stackFrame.IsExternalCode ? SourcePresentationHint.Deemphasize : SourcePresentationHint.Normal;

        // When debugging an interactive session, the ScriptPath is <No File> which is not a valid source file.
        // We need to make sure the user can't open the file associated with this stack frame.
        // It will generate a VSCode error in this case.
        Source? source = null;
        if (!stackFrame.ScriptPath.Contains("<"))
        {
            source = new Source
            {
                Path = stackFrame.ScriptPath,
                PresentationHint = sourcePresentationHint
            };
        }

        return new StackFrame
        {
            Id = id,
            Name = (source is not null) ? stackFrame.FunctionName : "Interactive Session",
            Line = (source is not null) ? stackFrame.StartLineNumber : 0,
            EndLine = stackFrame.EndLineNumber,
            Column = (source is not null) ? stackFrame.StartColumnNumber : 0,
            EndColumn = stackFrame.EndColumnNumber,
            Source = source
        };
    }

    public static StackFrame CreateBreakpointLabel(InvocationInfo invocationInfo, int id = 0) => new()
    {
        Name = "<Breakpoint>",
        Id = id,
        Source = new()
        {
            Path = invocationInfo.ScriptName
        },
        Line = invocationInfo.ScriptLineNumber,
        Column = invocationInfo.OffsetInLine,
        PresentationHint = StackFramePresentationHint.Label
    };

}

