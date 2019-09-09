using System;
using Microsoft.PowerShell.EditorServices.Engine.Services.DebugAdapter;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    public static class LspBreakpointUtils
    {
        public static Breakpoint CreateBreakpoint(
            BreakpointDetails breakpointDetails)
        {
            Validate.IsNotNull(nameof(breakpointDetails), breakpointDetails);

            return new Breakpoint
            {
                Id = breakpointDetails.Id,
                Verified = breakpointDetails.Verified,
                Message = breakpointDetails.Message,
                Source = new Source { Path = breakpointDetails.Source },
                Line = breakpointDetails.LineNumber,
                Column = breakpointDetails.ColumnNumber
            };
        }

        public static Breakpoint CreateBreakpoint(
            CommandBreakpointDetails breakpointDetails)
        {
            Validate.IsNotNull(nameof(breakpointDetails), breakpointDetails);

            return new Breakpoint
            {
                Verified = breakpointDetails.Verified,
                Message = breakpointDetails.Message
            };
        }

        public static Breakpoint CreateBreakpoint(
            SourceBreakpoint sourceBreakpoint,
            string source,
            string message,
            bool verified = false)
        {
            Validate.IsNotNull(nameof(sourceBreakpoint), sourceBreakpoint);
            Validate.IsNotNull(nameof(source), source);
            Validate.IsNotNull(nameof(message), message);

            return new Breakpoint
            {
                Verified = verified,
                Message = message,
                Source = new Source { Path = source },
                Line = sourceBreakpoint.Line,
                Column = sourceBreakpoint.Column
            };
        }
    }
}
